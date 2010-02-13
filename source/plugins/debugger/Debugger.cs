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

//using Gear;
//using Gear.Helpers;
using MCocoa;
//using MObjc;
using MObjc.Helpers;
//using Mono.Cecil;
//using Mono.Cecil.Binary;
using Mono.Debugger;
using Shared;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
//using System.IO;

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
	internal sealed class Debugger : IDisposable
	{
		public Debugger(ProcessStartInfo info)
		{
			Contract.Requires(info != null, "info is null");
			
			Unused.Value = VirtualMachineManager.BeginLaunch(info, this.OnLaunched);
			
			m_handlers.Add(typeof(AppDomainCreateEvent), (Event e) => DoAppDomainCreateEvent((AppDomainCreateEvent) e));
			m_handlers.Add(typeof(AppDomainUnloadEvent), (Event e) => DoAppDomainUnloadEvent((AppDomainUnloadEvent) e));
			m_handlers.Add(typeof(AssemblyLoadEvent), (Event e) => DoAssemblyLoadEvent((AssemblyLoadEvent) e));
			m_handlers.Add(typeof(BreakpointEvent), (Event e) => DoBreakpointEvent((BreakpointEvent) e));
			m_handlers.Add(typeof(StepEvent), (Event e) => DoStepEvent((StepEvent) e));
			m_handlers.Add(typeof(VMDeathEvent), (Event e) => DoVMDeathEvent((VMDeathEvent) e));
			m_handlers.Add(typeof(VMDisconnectEvent), (Event e) => DoVMDisconnectEvent((VMDisconnectEvent) e));
		}
		
		public State State
		{
			get {return m_state;}
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
						Log.WriteLine(TraceLevel.Verbose, "Debugger", "Dispose");
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
				
				DoTransition(State.Disconnected);
				m_disposed = true;
				m_vm = null;
			}
		}
		
		public event Action<State> StateEvent;
		public event Action<AssemblyMirror> AssemblyLoadedEvent;
		public event Action<Location> BreakpointEvent;
		public event Action<Location> SteppedEvent;
		
		// Either start running after connecting or after pausing (e.g. via a breakpoint).
		public void Run()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused, "state is " + m_state);
			
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "Running");
			DoTransition(State.Running);
			m_vm.Resume();
			
			if (m_thread == null)
			{
				m_thread = new Thread(this.DoDispatchEvents);
				m_thread.Name = "Debugger.DoDispatchEvents";
				m_thread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_thread.Start();
			}
		}
		
		public void StepOver()
		{
			DoStep(StepDepth.Over);
		}
		
		public void StepIn()
		{
			DoStep(StepDepth.Into);
		}
		
		public void StepOut()
		{
			DoStep(StepDepth.Out);
		}
		
		// TODO: Need to use something other than a MethodMirror and offset
		// so that breakpoints can be added before all assemblies are loaded
		// (pending breakpoints can be resolved as types are loaded).
		public void AddBreakpoint(MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused || m_state == State.Running, "state is " + m_state);
			Contract.Requires(method != null, "method is null");
			
			Breakpoint breakpoint = new Breakpoint(method, ilOffset);
			if (!m_breakpoints.ContainsKey(breakpoint))
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "Adding breakpoint to {0}:{1}", method.FullName, ilOffset);
				
				BreakpointEventRequest request = m_vm.CreateBreakpointRequest(method, ilOffset);
				request.Enable();
				m_breakpoints.Add(breakpoint, request);
			}
		}
		
		public void RemoveBreakpoint(MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused || m_state == State.Running, "state is " + m_state);
			Contract.Requires(method != null, "method is null");
			
			Breakpoint breakpoint = new Breakpoint(method, ilOffset);
			
			BreakpointEventRequest request;
			if (m_breakpoints.TryGetValue(breakpoint, out request))
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "Removing breakpoint from {0}:{1}", method.FullName, ilOffset);
				
				request.Disable();
				m_breakpoints.Remove(breakpoint);
			}
		}
		
		#region Event Handlers
		private void DoAppDomainCreateEvent(AppDomainCreateEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Created app domain {0}", e.Domain.FriendlyName);
		}
		
		private void DoAppDomainUnloadEvent(AppDomainUnloadEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Unloaded app domain {0}", e.Domain.FriendlyName);
		}
		
		private void DoAssemblyLoadEvent(AssemblyLoadEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Loaded assembly {0}", e.Assembly.GetName());
			
			if (AssemblyLoadedEvent != null)
				AssemblyLoadedEvent(e.Assembly);
		}
		
		private void DoBreakpointEvent(BreakpointEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Hit breakpoint at {0}", e.Method.FullName);
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			
			if (BreakpointEvent != null)
			{
				KeyValuePair<Breakpoint, BreakpointEventRequest> bp = m_breakpoints.Single(candidate => e.Request == candidate.Value);	// hitting a breakpoint is a fairly rare event so we can get by with a linear search
				Location location = e.Method.LocationAtILOffset((int) bp.Key.Offset);
				if (location != null)
					BreakpointEvent(location);
			}
		}
		
		private void DoStepEvent(StepEvent e)
		{
			Location location = e.Method.LocationAtILOffset((int) e.Location);
			if (location != null)
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1} in {2}:{3}", e.Method.FullName, e.Location, location.SourceFile, location.LineNumber);
			else
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1}", e.Method.FullName, e.Location);
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			m_stepRequest.Disable();
			
			if (SteppedEvent != null && location != null)
			{
				SteppedEvent(location);
			}
		}
		
		private void DoVMDeathEvent(VMDeathEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDeathEvent");
			DoTransition(State.Disconnected);
		}
		
		private void DoVMDisconnectEvent(VMDisconnectEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDisconnectEvent");
			DoTransition(State.Disconnected);
		}
		
		private void DoHandleUnknown(Event e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "{0}", e);
		}
		#endregion
		
		#region Private Methods
		[ThreadModel(ThreadModel.SingleThread)]
		private void OnLaunched(IAsyncResult result)
		{
			try
			{
				m_vm = VirtualMachineManager.EndLaunch(result);
				
				m_vm.EnableEvents(
					EventType.AssemblyLoad,
					EventType.AssemblyUnload,
//					EventType.Exception,
//					EventType.MethodEntry,
//					EventType.MethodExit,
//					EventType.Step,
//					EventType.TypeLoad,
					EventType.ThreadStart,
					EventType.ThreadDeath);
				
				Log.WriteLine(TraceLevel.Info, "Debugger", "Launched debugger");
				NSApplication.sharedApplication().BeginInvoke(() => DoTransition(State.Connected));
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
		
		public void DoStep(StepDepth depth)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Paused, "state is " + m_state);
			
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "Stepping {0}", depth);
			m_stepRequest = m_vm.CreateStepRequest(m_currentThread);
			m_stepRequest.Depth = depth;
			m_stepRequest.Size = StepSize.Line;
			m_stepRequest.Enabled = true;
			
			m_vm.Resume();
		}
		
		private void DoTransition(State newState)
		{
			if (newState != m_state)	
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "Transitioning from {0} to {1}", m_state, newState);
				m_state = newState;
				
				if (StateEvent != null)
				{
					StateEvent(m_state);
				}
			}
		}
		
		private void DoProcessEvent(Event e)
		{
			try
			{
				Action<Event> handler;
				if (m_handlers.TryGetValue(e.GetType(), out handler))
					handler(e);
				else
					DoHandleUnknown(e);
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
		}
		
		private void DoProcessEvents()
		{
			var temp = new List<Event>();
			lock (m_mutex)
			{
				temp.AddRange(m_events);
				m_events.Clear();
			}
			
			foreach (Event e in temp)
			{
				if (m_state != State.Disconnected)
					DoProcessEvent(e);
				else
					Log.WriteLine(TraceLevel.Verbose, "Debugger", "Ignoring {0} (disconnected)", e);
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoDispatchEvents()
		{
			try
			{
				while (true)
				{
					// GetNextEvent can take an arbitrary amount of time to execute (e.g.
					// when single stepping over a method which takes a long time) so we
					// need to get events from a thread.
					Event e = m_vm.GetNextEvent();
					
					// But we don't want to process events from the thread because that
					// complicates our life (for example it means that clients could not
					// know that it was safe to call our methods because VMDeathEvent
					// events can arrive at any time).
					lock (m_mutex)
					{
						m_events.Add(e);
						
						if (m_events.Count == 1)
							NSApplication.sharedApplication().BeginInvoke(this.DoProcessEvents);
					}
					
					// Bail if the VM is died or we've been disconnected.
					if (e is VMDeathEvent || e is VMDisconnectEvent)
						break;
					
					// For most other events we want to resume the VM so that we continue
					// to get new events.
					else if (!(e is BreakpointEvent || e is StepEvent || e is VMStartEvent))
						m_vm.Resume();
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Debugger", "DoDispatchEvents> {0}", e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => DoTransition(State.Disconnected));
			}
		}
		#endregion
		
		#region Private Types
		private struct Breakpoint : IEquatable<Breakpoint>
		{
			public Breakpoint(MethodMirror method, long offset)
			{
				Contract.Requires(method != null, "method is null");
				
				Method = method;
				Offset = offset;
			}
			
			public readonly MethodMirror Method;
			public readonly long Offset;
			
			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				
				if (GetType() != obj.GetType())
					return false;
				
				Breakpoint rhs = (Breakpoint) obj;
				return this == rhs;
			}
			
			public bool Equals(Breakpoint rhs)
			{
				return this == rhs;
			}
			
			public static bool operator==(Breakpoint lhs, Breakpoint rhs)
			{
				if (lhs.Method != rhs.Method)
					return false;
				
				if (lhs.Offset != rhs.Offset)
					return false;
				
				return true;
			}
			
			public static bool operator!=(Breakpoint lhs, Breakpoint rhs)
			{
				return !(lhs == rhs);
			}
			
			public override int GetHashCode()
			{
				int hash = 0;
				
				unchecked
				{
					hash += Method.GetHashCode();
					hash += Offset.GetHashCode();
				}
				
				return hash;
			}
		}
		#endregion
		
		#region Fields
		private VirtualMachine m_vm;
		private bool m_disposed;
		private State m_state;
		private ThreadMirror m_currentThread;
//		private AssemblyMirror m_currentAssembly;
		private Dictionary<Type, Action<Event>> m_handlers = new Dictionary<Type, Action<Event>>();
		
		private StepEventRequest m_stepRequest;
		private Dictionary<Breakpoint, BreakpointEventRequest> m_breakpoints = new Dictionary<Breakpoint, BreakpointEventRequest>();
		
		private Thread m_thread;
		private object m_mutex = new object();
			private List<Event> m_events = new List<Event>();
		#endregion
	}
}
