// Copyright (C) 2010 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Gear;
using MCocoa;
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace Debugger
{
	// This is the class that handles all interaction with the soft debugger.
	internal sealed class Debugger : IObserver
	{
		public Debugger(ProcessStartInfo info, bool breakInMain)
		{
			Contract.Requires(info != null, "info is null");
			
			ActiveObjects.Add(this);
			m_thread = new DebuggerThread(this, breakInMain);
			
			Boss boss = ObjectModel.Create("Application");
			m_transcript = boss.Get<ITranscript>();
			
			StepBy = StepSize.Line;
			var options = new LaunchOptions();
//			options.AgentArgs = "loglevel=1,logfile='/Users/jessejones/Source/Continuum/sdb.log'";
			
			// We do this lame assignment to a static so that OnLaunched can be made a static
			// method. Mono 2.6.7 doesn't GC asynchronously invoked delegates in a timely fashion
			// (tho it does appear to collect them if more than ten stack up).
			ms_debugger = this;
			Unused.Value = VirtualMachineManager.BeginLaunch(info, Debugger.OnLaunched, options);
			
			Broadcaster.Register("added breakpoint", this);
			Broadcaster.Register("removing breakpoint", this);
			Broadcaster.Register("toggled exceptions", this);
			
			ms_running = true;
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "added breakpoint":
					DoAddedBreakpoint((Breakpoint) value);
					break;
				
				case "removing breakpoint":
					DoRemovingBreakpoint((Breakpoint) value);
					break;
				
				case "toggled exceptions":
					m_thread.EnableBreakingOnExceptions((bool) value);
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void Shutdown()
		{
			if (!m_shutDown)
			{
				if (m_vm != null)
				{
					// Tell everyone that the debugger is going away.
					if (ms_running)
						Broadcaster.Invoke("debugger stopped", this);
					Broadcaster.Unregister(this);
					
					// Force the VM to exit (which should kill the debugee).
					try
					{
						Log.WriteLine(TraceLevel.Info, "Debugger", "Exiting VM");
						m_vm.Exit(5);
					}
					catch (System.Net.Sockets.SocketException e)
					{
						Log.WriteLine(TraceLevel.Warning, "Debugger", "Error exiting VM: {0}", e.Message);
					}
					catch (VMDisconnectedException)
					{
					}
					
					// If the debugee did not exit then give it a bit more time and then
					// hit it with a big hammer. (The debuggee will die asynchronously
					// so we'll often land in this code).
					Process process = m_vm.Process;
					if (process != null && !process.HasExited)
					{
						NSApplication.sharedApplication().BeginInvoke(() => DoKillProcess(process), TimeSpan.FromSeconds(3));
					}
				}
				
				m_shutDown = true;
				m_vm = null;
				m_thread = null;
				m_currentThread = null;
				ms_running = false;
			}
		}
		
		public VirtualMachine VM
		{
			get {return m_vm;}
		}
		
		public static bool IsRunning
		{
			get {return ms_running;}
		}
		
		public bool IsPaused
		{
			get {return m_thread.GetState() == State.Paused;}
		}
		
		public static bool IsShuttingDown(Exception e)
		{
			if (e is System.Reflection.TargetInvocationException)
				return IsShuttingDown(e.InnerException);
				
			return (e is VMDisconnectedException) || (e is System.Net.Sockets.SocketException);
		}
		
		// Either start running after connecting or after pausing (e.g. via a breakpoint).
		public void Run()
		{
			Contract.Assert(!m_shutDown);
			
			Log.WriteLine(TraceLevel.Info, "Debugger", "Running");
			Broadcaster.Invoke("debugger resumed", this);
			
//			NSApplication.sharedApplication().deactivate();			// this doesn't seem to do anything...
			m_thread.Resume();
		}
		
		public StepSize StepBy
		{
			get; set;
		}
		
		public void StepOver()
		{
			Contract.Assert(!m_shutDown);
			DoStep(StepDepth.Over);
		}
		
		public void StepIn()
		{
			Contract.Assert(!m_shutDown);
			DoStep(StepDepth.Into);
		}
		
		public void StepOut()
		{
			Contract.Assert(!m_shutDown);
			DoStep(StepDepth.Out);
		}
		
		public void Suspend()
		{
			m_thread.Suspend();
		}
		
		public FieldInfoMirror[] GetStaticFields()
		{
			return m_thread.GetStaticFields();
		}
		
		internal static bool IsTypeLoaded(string fullName)
		{
			return ms_loadedTypes.Contains(fullName);
		}
		
		#region Thread Callbacks
		internal void OnAssemblyLoad(AssemblyLoadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Loaded assembly '{0}'", e.Assembly.GetName().Name);
				
			e.Assembly.Metadata = AssemblyCache.Load(e.Assembly.Location, false);
			
			Broadcaster.Invoke("debugger loaded assembly", e.Assembly);
		}
		
		internal void OnAssemblyUnload(AssemblyUnloadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Unloaded assembly '{0}'", e.Assembly.GetName().Name);
			
			Broadcaster.Invoke("debugger unloaded assembly", e.Assembly);
		}
		
		internal Func<LiveStackFrame, Breakpoint, DebuggerThread.HandlerAction> BreakpointCondition {get; set;}
		
		internal void OnBreakAll()
		{
			IList<ThreadMirror> threads = VM.GetThreads();
			ThreadMirror main = threads.Single(t => t.Id == 1);
			var frame = main.GetFrames()[0];
			var context = new Context(main, frame.Method, frame.ILOffset);
			Broadcaster.Invoke("debugger break all", context);
			
			if (!NSApplication.sharedApplication().isActive())
				NSApplication.sharedApplication().activateIgnoringOtherApps(true);
		}
		
		internal void OnBreakpoint(BreakpointEvent e, ResolvedBreakpoint bp)
		{
			m_currentThread = e.Thread;
			
			var frames = new LiveStack(m_currentThread);
			
			Contract.Assert(BreakpointCondition != null, "BreakpointCondition is null");
			DebuggerThread.HandlerAction action = BreakpointCondition(frames[0], bp.BreakPoint);
			if (action == DebuggerThread.HandlerAction.Suspend)
			{
				if (m_stepRequest != null)
				{
					m_stepRequest.Disable();
					m_stepRequest = null;
				}
				
				Log.WriteLine(TraceLevel.Info, "Debugger", "Hit breakpoint at {0}:{1:X4}", e.Method.FullName, bp.Offset);
				var context = new Context(e.Thread, e.Method, bp.Offset);
				Broadcaster.Invoke("debugger processed breakpoint event", context);
				
				if (!NSApplication.sharedApplication().isActive())
					NSApplication.sharedApplication().activateIgnoringOtherApps(true);
			}
			else
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "ignoring breakpoint at {0}:{1:X4} (condition evaluated to false)", e.Method.FullName, bp.Offset);
				m_thread.Resume();
			}
		}
		
		internal void OnCreatedAppDomain(AppDomainCreateEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Created app domain '{0}'", e.Domain.FriendlyName);
			
			Broadcaster.Invoke("debugger loaded app domain", e.Domain);
		}
		
		internal void OnException(ExceptionEvent e)
		{
			var frames = new LiveStack(e.Thread);
			LiveStackFrame frame = frames[0];
				
			Boss boss = ObjectModel.Create("Application");
			var exceptions = boss.Get<IExceptions>();
			if (!DoIsIgnoredException(e.Exception.Type, exceptions.Ignored, frame.Method.Name))
			{
				m_currentThread = e.Thread;
				
				if (m_stepRequest != null)
				{
					m_stepRequest.Disable();
					m_stepRequest = null;
				}
				
				if (DebuggerWindows.WriteEvents)
					m_transcript.WriteLine(Output.Normal, "{0} exception was thrown at {1}:{2:X4}", e.Exception.Type.FullName, frame.Method.FullName, frame.ILOffset);
				var context = new Context(e.Thread, frame.Method, frame.ILOffset);
				Broadcaster.Invoke("debugger thrown exception", context);
			}
			else
			{
				m_transcript.WriteLine(Output.Normal, "Ignoring {0} in {1}:{2}", e.Exception.Type.FullName, frame.Method.DeclaringType.Name, frame.Method.Name);
				m_thread.Resume();
			}
		}
		
		internal void OnResolvedBreakpoint(ResolvedBreakpoint bp)
		{
			Broadcaster.Invoke("debugger resolved breakpoint", bp);
		}
		
		internal void OnStep(StepEvent e)
		{
			var frame = e.Thread.GetFrames()[0];		// in mono 2.6.7 we could get the location from the event, but that no longer works with mono 2.8
			Location location = frame.Location;
			if (location != null)
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4} in {2}:{3}", e.Method.FullName, frame.ILOffset, location.SourceFile, location.LineNumber);
			else
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4}", e.Method.FullName, frame.ILOffset);
				
			m_currentThread = e.Thread;
			m_stepRequest.Disable();
			m_stepRequest = null;
			
			var context = new Context(e.Thread, e.Method, frame.ILOffset);
			Broadcaster.Invoke("debugger processed step event", context);
		}
		
		internal void OnStateChanged(State newState)
		{
			if (!m_shutDown)
				Broadcaster.Invoke("debugger state changed", newState);
		}
		
		internal void OnThreadDeath(ThreadDeathEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Thread '{0}' died", ThreadsController.GetThreadName(e.Thread));
		}
		
		internal void OnThreadStart(ThreadStartEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Thread '{0}' started", ThreadsController.GetThreadName(e.Thread));
		}
		
		internal void OnTypeLoad(TypeLoadEvent e)
		{
			ms_loadedTypes.Add(e.Type.FullName);
		}
		
		internal void OnUnloadedAppDomain(AppDomainUnloadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Unloaded app domain '{0}'", e.Domain.FriendlyName);
			
			Broadcaster.Invoke("debugger unloaded app domain", e.Domain);
		}
		
		internal void OnUnresolvedBreakpoint(Breakpoint bp)
		{
			Broadcaster.Invoke("debugger unresolved breakpoint", bp);
		}
		
		internal void OnVMDied()
		{
			if (ms_running)
			{
				Broadcaster.Invoke("debugger stopped", this);
				ms_loadedTypes.Clear();
				ms_running = false;
			}
		}
		
		internal void OnVMStarted()
		{
			Broadcaster.Invoke("debugger started", this);
		}
		#endregion
		
		#region Private Methods
		private void DoKillProcess(Process process)
		{
			try
			{
				if (!process.HasExited)
				{
					Log.WriteLine(TraceLevel.Warning, "Debugger", "Force killing process");
					process.Kill();
				}
			}
			catch (Exception e)
			{
				m_transcript.WriteLine(Output.Error, "Error force killing debugee: {0}.", e.Message);
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private static void OnLaunched(IAsyncResult result)
		{
			try
			{
				ms_debugger.m_vm = VirtualMachineManager.EndLaunch(result);
				
				ms_debugger.m_stdoutThread = new Thread(() => ms_debugger.DoOutputThread(ms_debugger.m_vm.StandardOutput, Output.Normal));
				ms_debugger.m_stdoutThread.Name = "Debugger.stdout";
				ms_debugger.m_stdoutThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				ms_debugger.m_stdoutThread.Start();
				
				ms_debugger.m_stderrThread = new Thread(() => ms_debugger.DoOutputThread(ms_debugger.m_vm.StandardError, Output.Error));
				ms_debugger.m_stderrThread.Name = "Debugger.stderr";
				ms_debugger.m_stderrThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				ms_debugger.m_stderrThread.Start();
				
				// Note that we need to be a bit careful about which of these we enable
				// because we keep the VM suspended until we can process the event in
				// the main thread (if we are not careful we can block the debuggee too
				// much).
				ms_debugger.m_vm.EnableEvents(
					EventType.AppDomainCreate,
					EventType.AppDomainUnload,
					EventType.AssemblyLoad,
					EventType.AssemblyUnload,
//					EventType.Exception,
//					EventType.MethodEntry,
//					EventType.MethodExit,
//					EventType.Step,
					EventType.TypeLoad,
					EventType.ThreadStart,
					EventType.ThreadDeath
				);
				
				Log.WriteLine(TraceLevel.Info, "Debugger", "Launched debugger");
				NSApplication.sharedApplication().BeginInvoke(ms_debugger.Run);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", e);
				
				NSString title = NSString.Create("Couldn't launch the debugger.");
				NSString message = NSString.Create(e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => Functions.NSRunAlertPanel(title, message));
				
				ms_debugger.m_vm.Exit(1);
			}
			ms_debugger = null;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoOutputThread(System.IO.StreamReader stream, Output kind)
		{
			try
			{
				while (!stream.EndOfStream)
				{
					string line = stream.ReadLine();
					NSApplication.sharedApplication().BeginInvoke(() => m_transcript.WriteLine(kind, line));
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", e);
			}
		}
		
		private bool DoIsIgnoredException(TypeMirror type, string[] ignored, string method)
		{
			foreach (string candidate in ignored)
			{
				int i = candidate.IndexOf(':');
				if (i > 0)
				{
					string typeName = candidate.Substring(0, i);
					string methodName = candidate.Substring(i + 1);
					if (type.IsType(typeName))
						if (method == methodName)
							return true;
				}
				else
				{
					if (type.IsType(candidate))
						return true;
				}
			}
			
			return false;
		}
		
		private void DoAddedBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				m_thread.AddBreakpoint(bp, method, offset);
			}
		}
		
		private void DoRemovingBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				m_thread.RemoveBreakpoint(bp, method, offset);
			}
		}
		
		// Adding breakpoints after the program is running should be relatively rare so
		// we'll save memory by keeping a table of file paths to types instead of file
		// paths to methods (TypeMirror doesn't store the methods either).
		private bool DoTryGetMethod(Breakpoint bp, out MethodMirror method, out int offset)
		{
			TypeMirror[] types = m_thread.GetTypesDefinedWithin(bp.File);
			if (types.Length > 0)
			{
				foreach (TypeMirror type in types)
				{
					foreach (MethodMirror candidate in type.GetMethods())
					{
						if (candidate.SourceFile == bp.File)
						{
							Location loc = candidate.Locations.FirstOrDefault(l => l.LineNumber == bp.Line);
							if (loc != null)
							{
								Contract.Assert(loc.Method == candidate, "methods don't match");
								Contract.Assert(loc.SourceFile == bp.File, "paths don't match");
								
								method = candidate;
								offset = loc.ILOffset;
								return true;
							}
						}
					}
				}
			}
			
			method = null;
			offset = 0;
			
			return false;
		}
		
		private void DoStep(StepDepth depth)
		{
			Contract.Requires(m_stepRequest == null, "m_stepRequest is not null");
			
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "Stepping {0}", depth);
			m_stepRequest = m_vm.CreateStepRequest(m_currentThread);
			m_stepRequest.Depth = depth;
			m_stepRequest.Size = StepBy;
			m_stepRequest.Enabled = true;
			
			m_thread.Resume();
		}
		#endregion
		
		#region Fields
		private ITranscript m_transcript;
		private VirtualMachine m_vm;
		private bool m_shutDown;
		private DebuggerThread m_thread;
		
		private StepEventRequest m_stepRequest;
		private ThreadMirror m_currentThread;
		
		private Thread m_stdoutThread;
		private Thread m_stderrThread;
		private static bool ms_running;
		private static HashSet<string> ms_loadedTypes = new HashSet<string>();
		
		private static Debugger ms_debugger;
		#endregion
	}
}
