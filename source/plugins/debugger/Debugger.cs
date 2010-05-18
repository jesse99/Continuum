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
	internal enum State
	{
		Starting,				// we're starting up the virtual machine via the soft debugger
		Connected,			// we're ready to run the executable
		Running,				// the executable is executing
		Paused,				// the executable was running but now is not (i.e. it was stopped at a breakpoint or the user is single stepping)
		Disconnected,		// the soft debugger fell over or our connection to the debugger was lost
	}
	
	// This is the class that handles all interaction with the soft debugger.
	internal sealed class Debugger : IDisposable, IObserver
	{
		public Debugger(ProcessStartInfo info)
		{
			Contract.Requires(info != null, "info is null");
			
			Boss boss = ObjectModel.Create("Application");
			m_transcript = boss.Get<ITranscript>();
			
			StepBy = StepSize.Line;
			Unused.Value = VirtualMachineManager.BeginLaunch(info, this.OnLaunched);
			
			m_handlers.Add(typeof(AppDomainCreateEvent), (Event e) => DoAppDomainCreateEvent((AppDomainCreateEvent) e));
			m_handlers.Add(typeof(AppDomainUnloadEvent), (Event e) => DoAppDomainUnloadEvent((AppDomainUnloadEvent) e));
			m_handlers.Add(typeof(AssemblyLoadEvent), (Event e) => DoAssemblyLoadEvent((AssemblyLoadEvent) e));
			m_handlers.Add(typeof(AssemblyUnloadEvent), (Event e) => DoAssemblyUnloadEvent((AssemblyUnloadEvent) e));
			m_handlers.Add(typeof(BreakpointEvent), (Event e) => DoBreakpointEvent((BreakpointEvent) e));
			m_handlers.Add(typeof(ExceptionEvent), (Event e) => DoExceptionEvent((ExceptionEvent) e));
			m_handlers.Add(typeof(StepEvent), (Event e) => DoStepEvent((StepEvent) e));
			m_handlers.Add(typeof(TypeLoadEvent), (Event e) => DoTypeLoadEvent((TypeLoadEvent) e));
			m_handlers.Add(typeof(VMDeathEvent), (Event e) => DoVMDeathEvent((VMDeathEvent) e));
			m_handlers.Add(typeof(VMDisconnectEvent), (Event e) => DoVMDisconnectEvent((VMDisconnectEvent) e));
			m_handlers.Add(typeof(VMStartEvent), (Event e) => DoVMStartEvent((VMStartEvent) e));
			
			Broadcaster.Register("added breakpoint", this);
			Broadcaster.Register("removing breakpoint", this);
			Broadcaster.Register("toggled exceptions", this);
			
			ms_running = true;
		}
		
		public VirtualMachine VM
		{
			get {return m_vm;}
		}
		
		public State State
		{
			get {return m_state;}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "added breakpoint":
					OnAddedBreakpoint((Breakpoint) value);
					break;
				
				case "removing breakpoint":
					OnRemovingBreakpoint((Breakpoint) value);
					break;
				
				case "toggled exceptions":
					if ((bool) value)
						m_exceptionRequest.Enable();
					else
						m_exceptionRequest.Disable();
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
						if (m_state != State.Paused)
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
				DoTransition(State.Disconnected);
			}
		}
		
		public static bool IsRunning
		{
			get {return ms_running;}
		}
		
		// Either start running after connecting or after pausing (e.g. via a breakpoint).
		public void Run()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused, "state is " + m_state);
			
			Log.WriteLine(TraceLevel.Info, "Debugger", "Running");
			DoTransition(State.Running);
			m_vm.Resume();
			
			if (m_eventThread == null)
			{
				m_eventThread = new Thread(this.DoDispatchEvents);
				m_eventThread.Name = "Debugger.DoDispatchEvents";
				m_eventThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_eventThread.Start();
			}
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
			Contract.Requires(m_state == State.Connected || m_state == State.Paused || m_state == State.Running, "state is " + m_state);
			Contract.Requires(method != null, "method is null");
			
			var resolved = new ResolvedBreakpoint(bp, method, ilOffset);
			if (!m_breakpoints.ContainsKey(resolved))
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Adding breakpoint to {0}:{1:X4}", method.FullName, ilOffset);
				
				BreakpointEventRequest request = m_vm.CreateBreakpointRequest(method, ilOffset);
				request.Enable();
				m_breakpoints.Add(resolved, request);
			}
			
			Broadcaster.Invoke("debugger resolved breakpoint", resolved);
		}
		
		public void RemoveBreakpoint(Breakpoint bp, MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused || m_state == State.Running, "state is " + m_state);
			Contract.Requires(method != null, "method is null");
			
			var resolved = new ResolvedBreakpoint(bp, method, ilOffset);
			
			BreakpointEventRequest request;
			if (m_breakpoints.TryGetValue(resolved, out request))
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Removing breakpoint from {0}:{1:X4}", method.FullName, ilOffset);
				
				request.Disable();
				m_breakpoints.Remove(resolved);
			}
			
			Broadcaster.Invoke("debugger unresolved breakpoint", bp);
		}
		
		#region Event Handlers
		private HandlerAction DoAppDomainCreateEvent(AppDomainCreateEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Created app domain '{0}'", e.Domain.FriendlyName);
			
			Broadcaster.Invoke("debugger loaded app domain", e.Domain);
			
			return HandlerAction.Resume;
		}
		
		private HandlerAction DoAppDomainUnloadEvent(AppDomainUnloadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Unloaded app domain '{0}'", e.Domain.FriendlyName);
			
			Broadcaster.Invoke("debugger unloaded app domain", e.Domain);
			
			return HandlerAction.Resume;
		}
		
		private HandlerAction DoAssemblyLoadEvent(AssemblyLoadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Loaded assembly '{0}'", e.Assembly.GetName().Name);
			
			Broadcaster.Invoke("debugger loaded assembly", e.Assembly);
			
			return HandlerAction.Resume;
		}
		
		private HandlerAction DoAssemblyUnloadEvent(AssemblyUnloadEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Unloaded assembly '{0}'", e.Assembly.GetName().Name);
			
			Broadcaster.Invoke("debugger unloaded assembly", e.Assembly);
			
			return HandlerAction.Resume;
		}
		
		internal Func<Mono.Debugger.Soft.StackFrame, Breakpoint, HandlerAction> BreakpointCondition {get; set;}
		
		private HandlerAction DoBreakpointEvent(BreakpointEvent e)
		{
			Contract.Assert(!m_suspended);		// events should not be processed if we are already at a breakpoint
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			
			if (m_stepRequest != null)
			{
				m_stepRequest.Disable();
				m_stepRequest = null;
			}
			
			KeyValuePair<ResolvedBreakpoint, BreakpointEventRequest> bp = m_breakpoints.Single(candidate => e.Request == candidate.Value);	// hitting a breakpoint is a fairly rare event so we can get by with a linear search
			Mono.Debugger.Soft.StackFrame[] frames = m_currentThread.GetFrames();
			
			Contract.Assert(BreakpointCondition != null, "BreakpointCondition is null");
			HandlerAction action = BreakpointCondition(frames[0], bp.Key.BreakPoint);
			if (action == HandlerAction.Suspend)
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Hit breakpoint at {0}:{1:X4}", e.Method.FullName, bp.Key.Offset);
				m_suspended = true;					// note that this has to be set before we allow arbitrary Continuum code to run
				var context = new Context(e.Thread, e.Method, bp.Key.Offset);
				Broadcaster.Invoke("debugger processed breakpoint event", context);
			}
			else
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "ignoring breakpoint at {0}:{1:X4} (condition evaluated to false)", e.Method.FullName, bp.Key.Offset);
			}
			
			return action;
		}
		
		private HandlerAction DoExceptionEvent(ExceptionEvent e)
		{
			Contract.Assert(!m_suspended);		// events should not be processed if we are already at a breakpoint
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			
			if (m_stepRequest != null)
			{
				m_stepRequest.Disable();
				m_stepRequest = null;
			}
			
			m_suspended = true;
			Mono.Debugger.Soft.StackFrame[] frames = e.Thread.GetFrames();
			Mono.Debugger.Soft.StackFrame frame = frames[0];
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "{0} exception was thrown at {1}:{2:X4}", e.Exception.Type.FullName, frame.Method.FullName, frame.ILOffset);
			var context = new Context(e.Thread, frame.Method, frame.ILOffset);
			Broadcaster.Invoke("debugger thrown exception", context);
		
			return HandlerAction.Suspend;
		}
		
		private HandlerAction DoStepEvent(StepEvent e)
		{
			Contract.Assert(!m_suspended);		// events should not be processed if we are already at a breakpoint
			Location location = e.Method.LocationAtILOffset((int) e.Location);
			if (location != null)
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4} in {2}:{3}", e.Method.FullName, e.Location, location.SourceFile, location.LineNumber);
			else
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4}", e.Method.FullName, e.Location);
			DoTransition(State.Paused);
			
			m_suspended = true;
			m_currentThread = e.Thread;
			m_stepRequest.Disable();
			m_stepRequest = null;
			
			var context = new Context(e.Thread, e.Method, e.Location);
			Broadcaster.Invoke("debugger processed step event", context);
			
			return HandlerAction.Suspend;
		}
		
		private void DoThreadDeathEvent(ThreadDeathEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Thread '{0}' died", ThreadsController.GetThreadName(e.Thread));
			
			Broadcaster.Invoke("debugger thread died", e.Thread);
		}
		
		private void DoThreadStartEvent(ThreadStartEvent e)
		{
			if (DebuggerWindows.WriteEvents)
				m_transcript.WriteLine(Output.Normal, "Thread '{0}' started", ThreadsController.GetThreadName(e.Thread));
			
			Broadcaster.Invoke("debugger thread started", e.Thread);
		}
		
		private HandlerAction DoTypeLoadEvent(TypeLoadEvent e)
		{
			foreach (MethodMirror method in e.Type.GetMethods())
			{
				string path = method.SourceFile;
				if (!string.IsNullOrEmpty(path))
				{
					IEnumerable<Breakpoint> bps = Breakpoints.GetBreakpoints(path);		// TODO: could probably make this a bit more efficient by using TypeMirror.GetSourceFiles
					if (bps.Any())
					{
						List<TypeMirror> types;
						if (!m_types.TryGetValue(path, out types))
						{
							types = new List<TypeMirror>();
							m_types.Add(path, types);
						}
						types.Add(e.Type);
						
						foreach (Breakpoint bp in bps)
						{
							Location loc = method.Locations.FirstOrDefault(l => l.LineNumber == bp.Line);
							if (loc != null)
							{
								Contract.Assert(loc.Method == method, "methods don't match");
								Contract.Assert(loc.SourceFile == path, "paths don't match");
								
								AddBreakpoint(bp, method, loc.ILOffset);
							}
						}
					}
				}
			}
			
			return HandlerAction.Resume;
		}
		
		private HandlerAction DoVMDeathEvent(VMDeathEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDeathEvent");
			DoTransition(State.Disconnected);
			DoReset();
			Broadcaster.Invoke("debugger stopped", this);
			
			return HandlerAction.Suspend;
		}
		
		private HandlerAction DoVMDisconnectEvent(VMDisconnectEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDisconnectEvent");
			DoTransition(State.Disconnected);
			DoReset();
			Broadcaster.Invoke("debugger stopped", this);
			
			return HandlerAction.Suspend;
		}
		
		private HandlerAction DoVMStartEvent(VMStartEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMStartEvent");
			Broadcaster.Invoke("debugger started", this);
			
			m_exceptionRequest = m_vm.CreateExceptionRequest(null);
			if (DebuggerWindows.BreakOnExceptions)
				m_exceptionRequest.Enable();
			
			return HandlerAction.Resume;
		}
		
		private HandlerAction DoUnknownEvent(Event e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Unknown: {0}", e);
			
			return HandlerAction.Resume;
		}
		#endregion
		
		#region Private Methods
		private void DoReset()
		{
			m_types.Clear();
			
			foreach (ResolvedBreakpoint resolved in m_breakpoints.Keys)
			{
				Broadcaster.Invoke("debugger unresolved breakpoint", resolved.BreakPoint);
			}
			m_breakpoints.Clear();
		}
		
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
				NSApplication.sharedApplication().BeginInvoke(() => DoTransition(State.Connected));
				NSApplication.sharedApplication().BeginInvoke(() => Broadcaster.Invoke("debugger connected", this));
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", e);
				
				NSString title = NSString.Create("Couldn't launch the debugger.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
				
				NSApplication.sharedApplication().BeginInvoke(() => DoTransition(State.Disconnected));
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
		
		private void OnAddedBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				AddBreakpoint(bp, method, offset);
			}
		}
		
		private void OnRemovingBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				RemoveBreakpoint(bp, method, offset);
			}
		}
		
		// Adding breakpoints after the program is running should be relatively rare so
		// we'll save memory by keeping a table of file paths to types instead of file
		// paths to methods (TypeMirror doesn't store the methods either).
		private bool DoTryGetMethod(Breakpoint bp, out MethodMirror method, out int offset)
		{
			List<TypeMirror> types;
			IEnumerable<Breakpoint> breakpoints = Breakpoints.GetBreakpoints(bp.File);
			if (m_types.TryGetValue(bp.File, out types) && breakpoints.Any())
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
			Contract.Requires(m_state == State.Paused, "state is " + m_state);
			Contract.Requires(m_stepRequest == null, "m_stepRequest is not null");
			
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "Stepping {0}", depth);
			m_stepRequest = m_vm.CreateStepRequest(m_currentThread);
			m_stepRequest.Depth = depth;
			m_stepRequest.Size = StepBy;
			m_stepRequest.Enabled = true;
			
			DoTransition(State.Running);
			m_vm.Resume();
		}
		
		private void DoTransition(State newState)
		{
			if (newState != m_state)
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "Transitioning from {0} to {1}", m_state, newState);
				m_state = newState;
				
				if (!m_disposed)
				{
					Broadcaster.Invoke("debugger state changed", newState);
				}
			}
		}
		
		private HandlerAction DoProcessEvent(Event e)
		{
			HandlerAction action = HandlerAction.Resume;
			
			try
			{
				Func<Event, HandlerAction> handler;
				if (m_handlers.TryGetValue(e.GetType(), out handler))
					action = handler(e);
				else
					action = DoUnknownEvent(e);
			}
			catch (VMDisconnectedException)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "VMDisconnectedException while trying to process {0}", e);
				DoTransition(State.Disconnected);
			}
			catch (Exception ex)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", ex);
				
				NSString title = NSString.Create("Error processing {0}.", ex);
				NSString message = NSString.Create(ex.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			
			return action;
		}
		
		// This is where we execute events that we have received while we have the soft debugger
		// suspended. This can happen when we do things like execute methods to populate the 
		// variables window after hitting a breakpoint. These have to be special cased or we will
		// deadlock by queueing the event up to be processed by the main thread while the main
		// thread is blocked trying to process the original event.
		private void DoDeferredEvents()
		{
			lock (m_mutex)
			{
				while (m_deferredEvents.Count > 0 && m_state != State.Disconnected)
				{
					Event e = m_deferredEvents.Dequeue();
					
					// We want to ignore breakpoints or thrown exceptions when we explicitly
					// call into the VM. Step events should probably not happen but if they
					// somehow do happen we'll just require the user to step again.
					if (e is BreakpointEvent || e is ExceptionEvent || e is StepEvent)
					{
						Log.WriteLine(TraceLevel.Info, "Debugger", "Ignoring {0} (suspended)", e);
					}
					
					// If the VM died we need to just bail.
					else if (e is VMDeathEvent || e is VMDisconnectEvent)
					{
						Unused.Value = DoProcessEvent(e);
						break;
					}
					
					// Otherwise it should be some sort of notification event that we do care
					// about (like type load or thread started), but it should not be one that
					// wants to block the VM because we need to restart it in order to avoid
					// deadlocking.
					else
					{
						HandlerAction action = DoProcessEvent(e);
						Contract.Assert(action == HandlerAction.Resume);		// if this fires then it is probably an event we need to ignore
					}
				}
			}
		}
		
		private void DoProcessEvents()
		{
			bool resume = true;
			
			while (true)
			{
				Event e = null;
				lock (m_mutex)
				{
					if (m_events.Count > 0)
						e = m_events.Dequeue();
					else
						break;
				}
				
				if (m_state != State.Disconnected)
				{
					if (DoProcessEvent(e) == HandlerAction.Suspend)
						resume = false;
				}
				else
				{
					Log.WriteLine(TraceLevel.Info, "Debugger", "Ignoring {0} (disconnected)", e);
				}
			}
			
			if (m_state != State.Disconnected && resume)
			{
				m_suspended = false;
				DoDeferredEvents();
				if (m_state != State.Disconnected)
					m_vm.Resume();
			}
		}
		
		// A couple of things make this tricky:
		// 1) GetNextEvent can return multiple events even after the VM is suspended (and
		// there is no way to know if GetNextEvent will block or not).
		// 2) After getting an event the VM will be suspended, but there is no good way to
		// test to see if the VM is suspended and its an error to resume a VM that is already
		// running.
		// 3) We need to process TypeLoad events while the VM is suspended but they can
		// happen at any time and will normally be batched.
		// 4) TypeLoad and AssemblyLoad can happen even while we're sitting at a breakpoint
		// if the variable or breakpoints window evaluates a property.
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoDispatchEvents()
		{
			try
			{
				Event e = null;
				do
				{
					// GetNextEvent can take an arbitrary amount of time to execute (e.g.
					// when single stepping over a method which takes a long time) so we
					// need to get events from a thread.
					e = m_vm.GetNextEvent();
					
					// Some events need to be resumed immediately or the debugger locks
					// up...
					if (e is ThreadStartEvent)
					{
						var e1 = (ThreadStartEvent) e;
						NSApplication.sharedApplication().BeginInvoke(() => DoThreadStartEvent(e1));
						m_vm.Resume();
					}
					else if (e is ThreadDeathEvent)
					{
						var e2 = (ThreadDeathEvent) e;
						NSApplication.sharedApplication().BeginInvoke(() => DoThreadDeathEvent(e2));
						m_vm.Resume();
					}
					else if (m_suspended)
					{
						lock (m_mutex)
						{
							m_deferredEvents.Enqueue(e);
						}
						m_vm.Resume();
					}
					else
					{
						// Queue the event (and anything else the debugger agent has buffered) 
						// for the main thread to handle. Once the main thread has processed all 
						// the events it will resume the VM.
						lock (m_mutex)
						{
							m_events.Enqueue(e);
							
							if (m_events.Count == 1)
								NSApplication.sharedApplication().BeginInvoke(this.DoProcessEvents);
						}
					}
				}
				while (!(e is VMDeathEvent || e is VMDisconnectEvent));
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "DoDispatchEvents> {0}", e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => DoTransition(State.Disconnected));
			}
		}
		#endregion
		
		#region Internal Types
		internal enum HandlerAction
		{
			Resume,		// resume execution of the VM
			Suspend,		// keep the VM suspended
		}
		#endregion
		
		#region Fields
		private ITranscript m_transcript;
		private VirtualMachine m_vm;
		private bool m_disposed;
		private State m_state;
		private ThreadMirror m_currentThread;
		private Dictionary<Type, Func<Event, HandlerAction>> m_handlers = new Dictionary<Type, Func<Event, HandlerAction>>();
		
		private ExceptionEventRequest m_exceptionRequest;
		private StepEventRequest m_stepRequest;
		private Dictionary<ResolvedBreakpoint, BreakpointEventRequest> m_breakpoints = new Dictionary<ResolvedBreakpoint, BreakpointEventRequest>();
		private Dictionary<string, List<TypeMirror>> m_types = new Dictionary<string, List<TypeMirror>>();
		
		private Thread m_eventThread;
		private Thread m_stdoutThread;
		private Thread m_stderrThread;
		private volatile bool m_suspended;
		private object m_mutex = new object();
			private Queue<Event> m_events = new Queue<Event>();					// normal events from the soft debugger
			private Queue<Event> m_deferredEvents = new Queue<Event>();		// events sent while we've suspended the soft debugger
			
		private static bool ms_running;
		#endregion
	}
}
