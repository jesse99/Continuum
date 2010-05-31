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
	internal sealed class Debugger : IDisposable, IObserver
	{
		public Debugger(ProcessStartInfo info)
		{
			Contract.Requires(info != null, "info is null");
			
			m_thread = new DebuggerThread(this);
			
			Boss boss = ObjectModel.Create("Application");
			m_transcript = boss.Get<ITranscript>();
			
			StepBy = StepSize.Line;
			Unused.Value = VirtualMachineManager.BeginLaunch(info, this.OnLaunched);
			
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
		
		public void Dispose()
		{
			if (!m_disposed)
			{
				// Note that we are disposing of a managed object here so we
				// can't also do a Dispose from our finalizer.
				try
				{
					if (m_vm != null)
					{
						Broadcaster.Invoke("debugger stopped", this);
						Broadcaster.Unregister(this);
						
						Log.WriteLine(TraceLevel.Info, "Debugger", "Dispose");
						m_vm.Exit(0);
						m_vm.Dispose();
					}
				}
				catch (VMDisconnectedException)
				{
				}
				catch (Exception e)
				{
					Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", e);
				}
				
				m_disposed = true;
				m_vm = null;
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
			get {Console.WriteLine("m_thread.GetState(): {0}", m_thread.GetState()); Console.Out.Flush(); return m_thread.GetState() == State.Paused;}
			
		}
		
		// Either start running after connecting or after pausing (e.g. via a breakpoint).
		public void Run()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			Log.WriteLine(TraceLevel.Info, "Debugger", "Running");
			Broadcaster.Invoke("debugger resumed", this);
			m_thread.Resume();
		}
		
		public StepSize StepBy
		{
			get; set;
		}
		
		public void StepOver()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			DoStep(StepDepth.Over);
		}
		
		public void StepIn()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			DoStep(StepDepth.Into);
		}
		
		public void StepOut()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			DoStep(StepDepth.Out);
		}
		
		public void AddBreakpoint(Breakpoint bp, MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
				
			m_thread.AddBreakpoint(bp, method, ilOffset);
		}
		
		public void RemoveBreakpoint(Breakpoint bp, MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			m_thread.RemoveBreakpoint(bp, method, ilOffset);
		}
		
		#region Thread Callbacks
		internal void OnAssemblyLoad(AssemblyLoadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Loaded assembly '{0}'", e.Assembly.GetName().Name);
				
			e.Assembly.Metadata = AssemblyCache.Load(e.Assembly.Location, false);
	Console.WriteLine("e.Assembly.Metadata: {0}", e.Assembly.Metadata); Console.Out.Flush();
			
			Broadcaster.Invoke("debugger loaded assembly", e.Assembly);
		}
		
		internal void OnAssemblyUnload(AssemblyUnloadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Unloaded assembly '{0}'", e.Assembly.GetName().Name);
			
			Broadcaster.Invoke("debugger unloaded assembly", e.Assembly);
		}
		
		internal Func<Mono.Debugger.Soft.StackFrame, Breakpoint, DebuggerThread.HandlerAction> BreakpointCondition {get; set;}
		
		internal void OnBreakpoint(BreakpointEvent e, ResolvedBreakpoint bp)
		{
			m_currentThread = e.Thread;
			
			if (m_stepRequest != null)
			{
				m_stepRequest.Disable();
				m_stepRequest = null;
			}
			
			Mono.Debugger.Soft.StackFrame[] frames = m_currentThread.GetFrames();
			
			Contract.Assert(BreakpointCondition != null, "BreakpointCondition is null");
			DebuggerThread.HandlerAction action = BreakpointCondition(frames[0], bp.BreakPoint);
			if (action == DebuggerThread.HandlerAction.Suspend)
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Hit breakpoint at {0}:{1:X4}", e.Method.FullName, bp.Offset);
				var context = new Context(e.Thread, e.Method, bp.Offset);
				Broadcaster.Invoke("debugger processed breakpoint event", context);
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
			m_currentThread = e.Thread;
			
			if (m_stepRequest != null)
			{
				m_stepRequest.Disable();
				m_stepRequest = null;
			}
			
			Mono.Debugger.Soft.StackFrame[] frames = e.Thread.GetFrames();
			Mono.Debugger.Soft.StackFrame frame = frames[0];
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "{0} exception was thrown at {1}:{2:X4}", e.Exception.Type.FullName, frame.Method.FullName, frame.ILOffset);
			var context = new Context(e.Thread, frame.Method, frame.ILOffset);
			Broadcaster.Invoke("debugger thrown exception", context);
		}
		
		internal void OnResolvedBreakpoint(ResolvedBreakpoint bp)
		{
			Broadcaster.Invoke("debugger resolved breakpoint", bp);
		}
		
		internal void OnStep(StepEvent e)
		{
			Location location = e.Method.LocationAtILOffset((int) e.Location);
			if (location != null)
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4} in {2}:{3}", e.Method.FullName, e.Location, location.SourceFile, location.LineNumber);
			else
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4}", e.Method.FullName, e.Location);
			
			m_currentThread = e.Thread;
			m_stepRequest.Disable();
			m_stepRequest = null;
			
			var context = new Context(e.Thread, e.Method, e.Location);
			Broadcaster.Invoke("debugger processed step event", context);
		}
		
		internal void OnStateChanged(State newState)
		{
			if (!m_disposed)
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
			Broadcaster.Invoke("debugger stopped", this);
		}
		
		internal void OnVMStarted()
		{
			Broadcaster.Invoke("debugger started", this);
		}
		#endregion
		
		#region Private Methods
		[ThreadModel(ThreadModel.SingleThread)]
		private void OnLaunched(IAsyncResult result)
		{
			try
			{
				m_vm = VirtualMachineManager.EndLaunch(result);
				
				m_stdoutThread = new Thread(() => DoOutputThread(m_vm.StandardOutput, Output.Normal));
				m_stdoutThread.Name = "Debugger.StdOutThread";
				m_stdoutThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_stdoutThread.Start();
				
				m_stderrThread = new Thread(() => DoOutputThread(m_vm.StandardError, Output.Error));
				m_stderrThread.Name = "Debugger.StdErrThread";
				m_stderrThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_stderrThread.Start();
				
				// Note that we need to be a bit careful about which of these we enable
				// because we keep the VM suspended until we can process the event in
				// the main thread (if we are not careful we can block the debuggee too
				// much).
				m_vm.EnableEvents(
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
				NSApplication.sharedApplication().BeginInvoke(this.Run);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", e);
				
				NSString title = NSString.Create("Couldn't launch the debugger.");
				NSString message = NSString.Create(e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => Functions.NSRunAlertPanel(title, message));
				
				m_vm.Exit(1);
			}
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
			IEnumerable<Breakpoint> breakpoints = Breakpoints.GetBreakpoints(bp.File);
			if (types.Length > 0 && breakpoints.Any())
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
		private bool m_disposed;
		private DebuggerThread m_thread;
		
		private StepEventRequest m_stepRequest;
		private ThreadMirror m_currentThread;
		
		private Thread m_stdoutThread;
		private Thread m_stderrThread;
		private static bool ms_running;
		#endregion
	}
}
