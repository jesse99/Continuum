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
		Running,				// the executable is executing
		Paused,				// the executable was running but now is not (i.e. it was stopped at a breakpoint or the user is single stepping)
		Disconnected,		// the soft debugger fell over or our connection to the debugger was lost
	}
	
	// This is the class that processes events sent by the soft debugger.
	internal sealed class DebuggerThread
	{
		public DebuggerThread(Debugger debugger)
		{
			Contract.Requires(debugger != null, "debugger is null");
			
			m_debugger = debugger;
			
			m_handlers.Add(typeof(AppDomainCreateEvent), (Event e) => DoAppDomainCreateEvent((AppDomainCreateEvent) e));
			m_handlers.Add(typeof(AppDomainUnloadEvent), (Event e) => DoAppDomainUnloadEvent((AppDomainUnloadEvent) e));
			m_handlers.Add(typeof(AssemblyLoadEvent), (Event e) => DoAssemblyLoadEvent((AssemblyLoadEvent) e));
			m_handlers.Add(typeof(AssemblyUnloadEvent), (Event e) => DoAssemblyUnloadEvent((AssemblyUnloadEvent) e));
			m_handlers.Add(typeof(BreakpointEvent), (Event e) => DoBreakpointEvent((BreakpointEvent) e));
			m_handlers.Add(typeof(ExceptionEvent), (Event e) => DoExceptionEvent((ExceptionEvent) e));
			m_handlers.Add(typeof(StepEvent), (Event e) => DoStepEvent((StepEvent) e));
			m_handlers.Add(typeof(ThreadDeathEvent), (Event e) => DoThreadDeathEvent((ThreadDeathEvent) e));
			m_handlers.Add(typeof(ThreadStartEvent), (Event e) => DoThreadStartEvent((ThreadStartEvent) e));
			m_handlers.Add(typeof(TypeLoadEvent), (Event e) => DoTypeLoadEvent((TypeLoadEvent) e));
			m_handlers.Add(typeof(VMDeathEvent), (Event e) => DoVMDeathEvent((VMDeathEvent) e));
			m_handlers.Add(typeof(VMDisconnectEvent), (Event e) => DoVMDisconnectEvent((VMDisconnectEvent) e));
			m_handlers.Add(typeof(VMStartEvent), (Event e) => DoVMStartEvent((VMStartEvent) e));
		}
		
		public State GetState()
		{
			lock (m_mutex)
			{
				return m_state;
			}
		}
		
		public void Resume()
		{
			lock (m_mutex)
			{
				DoTransition(State.Running);
			}
			
			m_debugger.VM.Resume();
			
			if (m_eventThread == null)
			{
				m_eventThread = new Thread(this.DoDispatchEvents);
				m_eventThread.Name = "DebuggerThread.DoDispatchEvents";
				m_eventThread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_eventThread.Start();
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public void AddBreakpoint(Breakpoint bp, MethodMirror method, long ilOffset)
		{
			Contract.Requires(method != null, "method is null");
			
			var resolved = new ResolvedBreakpoint(bp, method, ilOffset);
			lock (m_mutex)
			{
				if (!m_breakpoints.ContainsKey(resolved))
				{
					Log.WriteLine(TraceLevel.Info, "Debugger", "Adding breakpoint to {0}:{1:X4}", method.FullName, ilOffset);
					
					BreakpointEventRequest request = m_debugger.VM.CreateBreakpointRequest(method, ilOffset);
					request.Enable();
					m_breakpoints.Add(resolved, request);
				}
			}
			
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnResolvedBreakpoint(resolved));
		}
		
		public void RemoveBreakpoint(Breakpoint bp, MethodMirror method, long ilOffset)
		{
			Contract.Requires(method != null, "method is null");
			
			var resolved = new ResolvedBreakpoint(bp, method, ilOffset);
			lock (m_mutex)
			{
				BreakpointEventRequest request;
				if (m_breakpoints.TryGetValue(resolved, out request))
				{
					Log.WriteLine(TraceLevel.Info, "Debugger", "Removing breakpoint from {0}:{1:X4}", method.FullName, ilOffset);
					
					request.Disable();
					m_breakpoints.Remove(resolved);
				}
			}
			
			m_debugger.OnUnresolvedBreakpoint(bp);
		}
		
		public void EnableBreakingOnExceptions(bool enable)
		{
			lock (m_mutex)
			{
				if (m_exceptionRequest != null)		// if null we'll check to see what it should be when we enable the VM
				{
					if (enable)
						m_exceptionRequest.Enable();
					else
						m_exceptionRequest.Disable();
				}
			}
		}
		
		public TypeMirror[] GetTypesDefinedWithin(string file)
		{
			TypeMirror[] types;
			
			lock (m_mutex)
			{
				List<TypeMirror> temp;
				if (m_types.TryGetValue(file, out temp))
					types = temp.ToArray();			// note that we need to return a new collection to ensure thread safety
				else
					types = new TypeMirror[0];
			}
			
			return types;
		}
		
		public FieldInfoMirror[] GetStaticFields()
		{
			FieldInfoMirror[] fields;
			
			lock (m_mutex)
			{
				fields = m_staticFields.ToArray();
			}
			
			return fields;
		}
		
		#region Event Handlers
		// Note that DoDispatchEvents grabs the lock before calling these.
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoAppDomainCreateEvent(AppDomainCreateEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnCreatedAppDomain(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoAppDomainUnloadEvent(AppDomainUnloadEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnUnloadedAppDomain(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoAssemblyLoadEvent(AssemblyLoadEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnAssemblyLoad(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoAssemblyUnloadEvent(AssemblyUnloadEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnAssemblyUnload(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoBreakpointEvent(BreakpointEvent e)
		{
			DoTransition(State.Paused);
			
			KeyValuePair<ResolvedBreakpoint, BreakpointEventRequest> bp = m_breakpoints.Single(candidate => e.Request == candidate.Value);	// hitting a breakpoint is a fairly rare event so we can get by with a linear search
			
			// If the breakpoint condition is false then we'll be (more or less) immediately resumed.
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnBreakpoint(e, bp.Key));
			
			return HandlerAction.Suspend;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoExceptionEvent(ExceptionEvent e)
		{
			DoTransition(State.Paused);
			
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnException(e));
			
			return HandlerAction.Suspend;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoStepEvent(StepEvent e)
		{
			DoTransition(State.Paused);
			
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnStep(e));
			
			return HandlerAction.Suspend;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoThreadDeathEvent(ThreadDeathEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnThreadDeath(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoThreadStartEvent(ThreadStartEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnThreadStart(e));
			
			return HandlerAction.Resume;
		}
		
		// Note that we can't do this in the main thread because we need to know right away
		// if any breakpoints are defined within the code which was just loaded.
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoTypeLoadEvent(TypeLoadEvent e)
		{
			m_staticFields.AddRange(from f in e.Type.GetFields() where f.IsStatic select f);
			
			foreach (MethodMirror method in e.Type.GetMethods())
			{
				string path = method.SourceFile;
				if (!string.IsNullOrEmpty(path))
				{
					List<TypeMirror> types;
					if (!m_types.TryGetValue(path, out types))
					{
						types = new List<TypeMirror>();
						m_types.Add(path, types);
					}
					types.Add(e.Type);
					
					Breakpoint[] bps = Breakpoints.GetBreakpoints(path);		// TODO: could probably make this a bit more efficient by using TypeMirror.GetSourceFiles
					if (bps.Length > 0)
					{
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
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnTypeLoad(e));
			
			return HandlerAction.Resume;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoVMDeathEvent(VMDeathEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDeathEvent");
			DoTransition(State.Disconnected);
			DoReset();
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnVMDied());
			
			return HandlerAction.Suspend;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoVMDisconnectEvent(VMDisconnectEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDisconnectEvent");
			DoTransition(State.Disconnected);
			DoReset();
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnVMDied());
			
			return HandlerAction.Suspend;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private HandlerAction DoVMStartEvent(VMStartEvent e)
		{
			NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnVMStarted());
			
			m_exceptionRequest = m_debugger.VM.CreateExceptionRequest(null);
			if (DebuggerWindows.BreakOnExceptions)
				m_exceptionRequest.Enable();
			
			return HandlerAction.Suspend;
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
			foreach (ResolvedBreakpoint resolved in m_breakpoints.Keys)
			{
				NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnUnresolvedBreakpoint(resolved.BreakPoint));
			}
			
			m_exceptionRequest = null;
			m_breakpoints.Clear();
			m_types.Clear();
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoTransition(State newState)
		{
			if (newState != m_state)
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "Transitioning from {0} to {1}", m_state, newState);
				m_state = newState;
				
				NSApplication.sharedApplication().BeginInvoke(() => m_debugger.OnStateChanged(newState));
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
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
			catch (Exception ex)
			{
				if (Debugger.IsShuttingDown(ex))
				{
					Log.WriteLine(TraceLevel.Error, "Debugger", "VMDisconnectedException while trying to process {0}", e);
					DoTransition(State.Disconnected);
				}
				else
				{
					Log.WriteLine(TraceLevel.Error, "Debugger", "{0}", ex);
					
					NSString title = NSString.Create("Error processing {0}.", ex);
					NSString message = NSString.Create(ex.Message);
					NSApplication.sharedApplication().BeginInvoke(() => Functions.NSRunAlertPanel(title, message));
				}
			}
			
			return action;
		}
		
		// Ideally we'd get notified of debugger events here and queue them up to be processed
		// by the main thread. This would minimize the amount of threaded code that we have to
		// deal with, but it seems to be really hard to do without introducing deadlocks (because
		// we can get events even while we have paused the debugger) or races resuming the VM
		// (because the main thread needs to resume the VM, but this thread also needs to resume
		// if we have the VM paused and are evaluating a property or something for the variables
		// or immediate windows).
		//
		// So, what we do instead is process events as soon as they arrive and notify the main thread
		// that an event has been processed so that it can update the UI.
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoDispatchEvents()
		{
			try
			{
				while (true)
				{
					VirtualMachine vm = m_debugger.VM;
					if (vm == null)									// debugger may have been disposed 
						break;
						
					Event e = vm.GetNextEvent();
					TypeLoadEvent tl = e as TypeLoadEvent;
					if (tl != null)
						Log.WriteLine(TraceLevel.Verbose, "Debugger", "dispatching {0} ({1})", e, tl.Type.FullName);
					else
						Log.WriteLine(TraceLevel.Verbose, "Debugger", "dispatching {0}", e);
					
					lock (m_mutex)
					{
						HandlerAction action = DoProcessEvent(e);
						if (m_state == State.Disconnected)		// error or VMDeathEvent or VMDisconnectEvent
							break;
							
						if (action == HandlerAction.Resume)
							vm.Resume();
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "DoDispatchEvents> {0}", e.Message);
				DoTransition(State.Disconnected);
			}
			
			m_eventThread = null;
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
		private Debugger m_debugger;
		private Dictionary<Type, Func<Event, HandlerAction>> m_handlers = new Dictionary<Type, Func<Event, HandlerAction>>();
		private Thread m_eventThread;
		
		private object m_mutex = new object();
			private State m_state;
			private ExceptionEventRequest m_exceptionRequest;
			private Dictionary<ResolvedBreakpoint, BreakpointEventRequest> m_breakpoints = new Dictionary<ResolvedBreakpoint, BreakpointEventRequest>();
			private Dictionary<string, List<TypeMirror>> m_types = new Dictionary<string, List<TypeMirror>>();
			private List<FieldInfoMirror> m_staticFields = new List<FieldInfoMirror>();
		#endregion
	}
}
