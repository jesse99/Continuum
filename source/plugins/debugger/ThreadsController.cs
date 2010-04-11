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
using System.Collections.Generic;
using System.Linq;

namespace Debugger
{
	[ExportClass("ThreadsController", "NSWindowController", Outlets = "table")]
	internal sealed class ThreadsController : NSWindowController, IObserver
	{
		public ThreadsController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger state changed", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":	
					m_debugger = (Debugger) value;
					m_threads = m_debugger.VM.GetThreads();
					m_selected = -1;
					m_table.reloadData();
					break;
					
				case "debugger processed breakpoint event":	
				case "debugger thrown exception":	
				case "debugger processed step event":
					var context = (Context) value;
					m_threads = m_debugger.VM.GetThreads();						// need to refresh this each time because threads may have been created or destroyed
					m_selected = m_threads.IndexOf(context.Thread);
					m_table.reloadData();
					break;
				
				case "debugger state changed":
					var state = (State) value;
					if (state != m_state)
					{
						m_state = state;
						if (state != State.Paused && state != State.Running && m_threads != null)
						{
							m_threads = null;
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
			if (!m_threads[row].IsCollected)
			{
				StackFrame[] stack = m_threads[row].GetFrames();
				if (stack.Length > 0)
				{
					m_selected = row;
					m_table.reloadData();
					
					Broadcaster.Invoke("changed thread", stack);
				}
				else
				{
					Boss boss = ObjectModel.Create("Application");
					var transcript = boss.Get<ITranscript>();
					transcript.Show();
					transcript.WriteLine(Output.Error, "{0}", "The thread has no stack frames.");
				}
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_threads != null ? m_threads.Count : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			if (m_threads == null || m_debugger.State == State.Disconnected)
				return NSString.Empty;
			
			ThreadMirror thread = m_threads[row];
			string name = thread.Id == 1 ? "main" : thread.Name;
			
			if (col.identifier().ToString() == "0")
				if (!string.IsNullOrEmpty(name))
					return DoCreateString(string.Format("{0} ({1})", name, thread.Id), row);
				else
					return DoCreateString(thread.Id.ToString(), row);
			else if (col.identifier().ToString() == "1")
				return DoCreateString(thread.ThreadState.ToString(), row);
			else
				return DoCreateString(thread.Domain.FriendlyName, row);
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
			
			if (m_threads[row].IsCollected)
				color = NSColor.disabledControlTextColor();
			
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
		private Debugger m_debugger;
		private IList<ThreadMirror> m_threads;
		private State m_state;
		private int m_selected;
		#endregion
	}
}
