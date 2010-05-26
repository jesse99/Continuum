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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Mono.Debugger;
using Shared;
using System;
using System.Collections.Generic;

namespace Debugger
{
	[ExportClass("ControlsController", "NSWindowController", Outlets = "label")]
	internal sealed class ControlsController : NSWindowController, IObserver
	{
		public ControlsController(IntPtr instance) : base(instance)
		{
			m_label = new IBOutlet<NSTextField>(this, "label").Value;
			m_label.setStringValue(NSString.Create("Connecting."));
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Register(this, 61, () => m_debugger.Run(), this.DoIsPaused);
			handler.Register(this, 62, () => m_debugger.StepOver(), this.DoIsPaused);
			handler.Register(this, 63, () => m_debugger.StepIn(), this.DoIsPaused);
			handler.Register(this, 64, () => m_debugger.StepOut(), this.DoIsPaused);
			
			Broadcaster.Register("debugger state changed", this);
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger stopped", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger state changed":
					var state = (State) value;
					DoStateChanged(state);
					break;
				
				case "debugger started":
					m_debugger = (Debugger) value;
					break;
				
				case "debugger stopped":
					m_debugger = null;
					m_label.setStringValue(NSString.Create("Disconnected"));
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoStateChanged(State state)
		{
			m_label.setStringValue(NSString.Create(state.ToString()));
		}
		
		private bool DoIsPaused()
		{
			return m_debugger != null && m_debugger.IsPaused;
		}
		#endregion
		
		#region Fields
		private Debugger m_debugger;
		private NSTextField m_label;
		#endregion
	}
}
