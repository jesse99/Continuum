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

using MCocoa;
using MObjc;
using MObjc.Helpers;
using Shared;
using System;

namespace Debugger
{
	[ExportClass("DebuggerWindows", "NSObject", Outlets = "controlsWindow variablesWindow controlsController variablesController")]
	internal sealed class DebuggerWindows : NSObject, IObserver
	{
		public DebuggerWindows() : base(NSObject.AllocAndInitInstance("DebuggerWindows"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("debugger"), this);
			
			m_controlsWindow = new IBOutlet<NSWindow>(this, "controlsWindow").Value;
			m_variablesWindow = new IBOutlet<NSWindow>(this, "variablesWindow").Value;
			
			// Not sure why but we need to do this but if we don't the controllers are not
			// constructed.
			Unused.Value = new IBOutlet<NSWindowController>(this, "controlsController").Value;
			Unused.Value = new IBOutlet<NSWindowController>(this, "variablesController").Value;
			
			// We don't actually close these windows so we need to manage auto-saving ourself.
			m_controlsWindow.setFrameAutosaveName(NSString.Create("debugger controls window"));
			m_variablesWindow.setFrameAutosaveName(NSString.Create("debugger variables window"));
			
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger stopped", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":
					m_controlsWindow.makeKeyAndOrderFront(this);
					m_variablesWindow.makeKeyAndOrderFront(this);
					break;
				
				case "debugger stopped":
					m_controlsWindow.saveFrameUsingName(NSString.Create("debugger controls window"));
					m_variablesWindow.saveFrameUsingName(NSString.Create("debugger variables window"));
					
					m_controlsWindow.orderOut(this);
					m_variablesWindow.orderOut(this);
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		#endregion
		
		#region Fields
		private NSWindow m_controlsWindow;
		private NSWindow m_variablesWindow;
		#endregion
	}
}
