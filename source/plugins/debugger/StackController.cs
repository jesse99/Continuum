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
using Mono.Debugger.Soft;
using Shared;
using System;

namespace Debugger
{
	[ExportClass("StackController", "NSWindowController", Outlets = "table")]
	internal sealed class StackController : NSWindowController, IObserver
	{
		public StackController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger state changed", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger processed breakpoint event":	
				case "debugger thrown exception":	
				case "debugger processed step event":
					var context = (Context) value;
					StackFrame[] stack = context.Thread.GetFrames();
					if (!StackFrameExtensions.Matches(stack, m_stack))
					{
						m_stack = stack;
						m_selected = 0;
						m_table.reloadData();
						m_table.scrollRowToVisible(m_stack.Length - 1);
					}
					break;
				
				case "debugger state changed":
					var state = (State) value;
					if (state != m_state)
					{
						m_state = state;
						if (state != State.Paused && state != State.Running && m_stack != null)
						{
							m_stack = null;
							m_table.reloadData();
						}
					}
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void doubleClicked(NSObject sender)
		{
			int row = m_table.selectedRow();
			row = m_stack.Length - row - 1;		// frames are drawn top down
			
			StackFrame[] stack = m_stack[0].Thread.GetFrames();		// we need to get a fresh copy of the stack frame or the debugger whines that the "the specified stack frame is no longer valid"
			if (stack[row].Matches(m_stack[row]))
			{
				m_selected = row;
				m_table.reloadData();
				Broadcaster.Invoke("changed stack frame", stack[row]);
			}
			else
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.WriteLine(Output.Error, "Current stack doesn't match cached stack.");
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_stack != null ? m_stack.Length : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			if (m_stack == null)
				return NSString.Empty;
			
			row = m_stack.Length - row - 1;		// draw the frames top down
			StackFrame frame = m_stack[row];
			if (col.identifier().ToString() == "0")
				return DoCreateString(System.IO.Path.GetFileName(frame.FileName), row);
			else if (col.identifier().ToString() == "1")
				return DoCreateString(frame.LineNumber.ToString(), row);
			else
				return DoCreateString(frame.Method.FullName, row);
		}
		
		#region Private Methods
		private NSObject DoCreateString(string text, int row)
		{
			NSColor color = null;
			if (m_state == State.Paused)
			{
				if (row == m_selected)
					color = NSColor.blueColor();
			}
			else
			{
				color = NSColor.disabledControlTextColor();
			}
			
			NSObject str;
			if (color != null)
			{
				var attrs = NSMutableDictionary.Create();
				attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
				str = NSAttributedString.Create(text, attrs);
			}
			else
			{
				str = NSString.Create(text);
			}
			
			return str;
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private StackFrame[] m_stack;
		private State m_state;
		private int m_selected;
		#endregion
	}
}
