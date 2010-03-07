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
			
			StepBy = StepSize.Line;
			Unused.Value = VirtualMachineManager.BeginLaunch(info, this.OnLaunched);
			
			m_handlers.Add(typeof(AppDomainCreateEvent), (Event e) => DoAppDomainCreateEvent((AppDomainCreateEvent) e));
			m_handlers.Add(typeof(AppDomainUnloadEvent), (Event e) => DoAppDomainUnloadEvent((AppDomainUnloadEvent) e));
			m_handlers.Add(typeof(AssemblyLoadEvent), (Event e) => DoAssemblyLoadEvent((AssemblyLoadEvent) e));
			m_handlers.Add(typeof(BreakpointEvent), (Event e) => DoBreakpointEvent((BreakpointEvent) e));
			m_handlers.Add(typeof(StepEvent), (Event e) => DoStepEvent((StepEvent) e));
			m_handlers.Add(typeof(TypeLoadEvent), (Event e) => DoTypeLoadEvent((TypeLoadEvent) e));
			m_handlers.Add(typeof(VMDeathEvent), (Event e) => DoVMDeathEvent((VMDeathEvent) e));
			m_handlers.Add(typeof(VMDisconnectEvent), (Event e) => DoVMDisconnectEvent((VMDisconnectEvent) e));
			
			Breakpoints.AddedBreakpoint += this.OnAddedBreakpoint;
			Breakpoints.RemovingBreakpoint += this.OnRemovingBreakpoint;
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
				DoTransition(State.Disconnected);
			}
		}
		
		public event Action<State> StateEvent;
		public event Action<AssemblyMirror> AssemblyLoadedEvent;
		public event Action<Context> BreakpointEvent;
		public event Action<Context> SteppedEvent;
		
		// Either start running after connecting or after pausing (e.g. via a breakpoint).
		public void Run()
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused, "state is " + m_state);
			
			Log.WriteLine(TraceLevel.Info, "Debugger", "Running");
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
		
		public void AddBreakpoint(MethodMirror method, long ilOffset)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			Contract.Requires(m_state == State.Connected || m_state == State.Paused || m_state == State.Running, "state is " + m_state);
			Contract.Requires(method != null, "method is null");
			
			var breakpoint = new ResolvedBreakpoint(method, ilOffset);
			if (!m_breakpoints.ContainsKey(breakpoint))
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Adding breakpoint to {0}:{1:X4}", method.FullName, ilOffset);
				
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
			
			var breakpoint = new ResolvedBreakpoint(method, ilOffset);
			
			BreakpointEventRequest request;
			if (m_breakpoints.TryGetValue(breakpoint, out request))
			{
				Log.WriteLine(TraceLevel.Info, "Debugger", "Removing breakpoint from {0}:{1:X4}", method.FullName, ilOffset);
				
				request.Disable();
				m_breakpoints.Remove(breakpoint);
			}
		}
		
		#region Event Handlers
		private bool DoAppDomainCreateEvent(AppDomainCreateEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Created app domain {0}", e.Domain.FriendlyName);
			
			return true;
		}
		
		private bool DoAppDomainUnloadEvent(AppDomainUnloadEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Unloaded app domain {0}", e.Domain.FriendlyName);
			
			return true;
		}
		
		private bool DoAssemblyLoadEvent(AssemblyLoadEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Loaded assembly {0}", e.Assembly.GetName());
			
			if (AssemblyLoadedEvent != null)
				AssemblyLoadedEvent(e.Assembly);
			
			return true;
		}
		
		private bool DoBreakpointEvent(BreakpointEvent e)
		{
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			
			if (BreakpointEvent != null)
			{
				KeyValuePair<ResolvedBreakpoint, BreakpointEventRequest> bp = m_breakpoints.Single(candidate => e.Request == candidate.Value);	// hitting a breakpoint is a fairly rare event so we can get by with a linear search
				Log.WriteLine(TraceLevel.Info, "Debugger", "Hit breakpoint at {0}:{1:X4}", e.Method.FullName, bp.Key.Offset);
				BreakpointEvent(new Context(e.Thread, e.Method, bp.Key.Offset));
			}
			
			return false;
		}
		
		private bool DoStepEvent(StepEvent e)
		{
			Location location = e.Method.LocationAtILOffset((int) e.Location);
			if (location != null)
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4} in {2}:{3}", e.Method.FullName, e.Location, location.SourceFile, location.LineNumber);
			else
				Log.WriteLine(TraceLevel.Info, "Debugger", "Stepped to {0}:{1:X4}", e.Method.FullName, e.Location);
			DoTransition(State.Paused);
			
			m_currentThread = e.Thread;
			m_stepRequest.Disable();
			
			if (SteppedEvent != null)
			{
				SteppedEvent(new Context(e.Thread, e.Method, e.Location));
			}
			
			return false;
		}
		
		private bool DoTypeLoadEvent(TypeLoadEvent e)
		{
			foreach (MethodMirror method in e.Type.GetMethods())
			{
				string path = method.SourceFile;
				if (!string.IsNullOrEmpty(path))
				{
					IEnumerable<Breakpoint> bps = Breakpoints.GetBreakpoints(path);
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
								
								AddBreakpoint(method, loc.ILOffset);
							}
						}
					}
				}
			}
			
			return true;
		}
		
		private bool DoVMDeathEvent(VMDeathEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDeathEvent");
			DoTransition(State.Disconnected);
			m_types.Clear();
			
			return false;
		}
		
		private bool DoVMDisconnectEvent(VMDisconnectEvent e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "VMDisconnectEvent");
			DoTransition(State.Disconnected);
			m_types.Clear();
			
			return false;
		}
		
		private bool DoUnknownEvent(Event e)
		{
			Log.WriteLine(TraceLevel.Info, "Debugger", "Unknown: {0}", e);
			
			return true;
		}
		#endregion
		
		#region Private Methods
		[ThreadModel(ThreadModel.SingleThread)]
		private void OnLaunched(IAsyncResult result)
		{
			try
			{
				m_vm = VirtualMachineManager.EndLaunch(result);
				
				// Note that we need to be a bit careful about which of these we enable
				// because we keep the VM suspended until we can process the event in
				// the main thread.
				m_vm.EnableEvents(
					EventType.AssemblyLoad,
					EventType.AssemblyUnload,
//					EventType.Exception,
//					EventType.MethodEntry,
//					EventType.MethodExit,
//					EventType.Step,
					EventType.TypeLoad
//					EventType.ThreadStart,
//					EventType.ThreadDeath
				);
				
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
		
		private void OnAddedBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				AddBreakpoint(method, offset);
			}
		}
		
		private void OnRemovingBreakpoint(Breakpoint bp)
		{
			MethodMirror method;
			int offset;
			if (DoTryGetMethod(bp, out method, out offset))
			{
				RemoveBreakpoint(method, offset);
			}
		}
		
		// Adding breakpoints after the program is running should be relatively rare so
		// we'll save memory by keeping a table of file paths to types instead of file
		// paths to methods (TypeMirror doesn't store the methods either).
		private bool DoTryGetMethod(Breakpoint bp, out MethodMirror method, out int offset)
		{
			List<TypeMirror> types;
			IEnumerable<Breakpoint> breakpoints = Breakpoints.GetBreakpoints(bp.File);
			if (m_types.TryGetValue(bp.File, out types) && breakpoints.Any())	// TODO: need to use a removing event instead of removed
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
				
				if (StateEvent != null && !m_disposed)
				{
					StateEvent(m_state);
				}
			}
		}
		
		private bool DoProcessEvent(Event e)
		{
			bool resume = true;
			
			try
			{
				Func<Event, bool> handler;
				if (m_handlers.TryGetValue(e.GetType(), out handler))
					resume = handler(e);
				else
					resume = DoUnknownEvent(e);
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
			
			return resume;
		}
		
		// We might want to BeginInvoke ourselves so we don't lock up the main thread
		// for too long.
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
					if (!DoProcessEvent(e))
						resume = false;
				}
				else
				{
					Log.WriteLine(TraceLevel.Info, "Debugger", "Ignoring {0} (disconnected)", e);
				}
			}
			
			if (m_state != State.Disconnected && resume)
				m_vm.Resume();
		}
		
		// A couple of things make this tricky:
		// 1) GetNextEvent can return multiple events even after the VM is suspended (and
		// there is no way to know if GetNextEvent will block or not).
		// 2) After getting an event the VM will be suspended, but there is no good way to
		// test to see if the VM is suspended and its an error to resume a VM that is already
		// running.
		// 3) We need to process TypeLoad events while the VM is suspended but they can
		// happen at any time and will normally be batched.
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
					
					// Bail if the VM is died or we've been disconnected.
					if (e is VMDeathEvent || e is VMDisconnectEvent)
					{
						break;
					}
					else
					{
						// Otherwise we'll queue it (and anything else the debugger agent has
						// buffered) for the main thread to handle. Once the main thread has
						// processed all the events it will resume the VM.
						lock (m_mutex)
						{
							m_events.Enqueue(e);
							
							if (m_events.Count == 1)
								NSApplication.sharedApplication().BeginInvoke(this.DoProcessEvents);
						}
					}
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
		private struct ResolvedBreakpoint : IEquatable<ResolvedBreakpoint>
		{
			public ResolvedBreakpoint(MethodMirror method, long offset)
			{
				Contract.Requires(method != null, "method is null");
				Contract.Requires(offset >= 0, "offset is negative");
				
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
				
				ResolvedBreakpoint rhs = (ResolvedBreakpoint) obj;
				return this == rhs;
			}
			
			public bool Equals(ResolvedBreakpoint rhs)
			{
				return this == rhs;
			}
			
			public static bool operator==(ResolvedBreakpoint lhs, ResolvedBreakpoint rhs)
			{
				if (lhs.Method != rhs.Method)
					return false;
				
				if (lhs.Offset != rhs.Offset)
					return false;
				
				return true;
			}
			
			public static bool operator!=(ResolvedBreakpoint lhs, ResolvedBreakpoint rhs)
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
		private Dictionary<Type, Func<Event, bool>> m_handlers = new Dictionary<Type, Func<Event, bool>>();
		
		private StepEventRequest m_stepRequest;
		private Dictionary<ResolvedBreakpoint, BreakpointEventRequest> m_breakpoints = new Dictionary<ResolvedBreakpoint, BreakpointEventRequest>();
		private Dictionary<string, List<TypeMirror>> m_types = new Dictionary<string, List<TypeMirror>>();
		
		private Thread m_thread;
		private object m_mutex = new object();
			private Queue<Event> m_events = new Queue<Event>();
		#endregion
	}
}
