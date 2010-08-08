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
			Broadcaster.Register("debugger stopped", this);
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger state changed", this);
			Broadcaster.Register("debugger thread died", this);
			Broadcaster.Register("debugger thread started", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":
					m_debugger = (Debugger) value;
					DoRefreshThreads();
					m_table.reloadData();
					break;
					
				case "debugger stopped":
					m_debugger = null;
					m_threads.Clear();
					break;
					
				case "debugger processed breakpoint event":
				case "debugger thrown exception":
				case "debugger processed step event":
					var context = (Context) value;
					DoRefreshThreads();						// need to refresh this each time because thread states may have changed
					m_selected = m_threads.IndexOf(context.Thread);
					m_table.reloadData();
					break;
				
				case "debugger state changed":
					var state = (State) value;
					if (state != m_state)
					{
						m_state = state;
						if (state != State.Paused && state != State.Running && m_threads.Count > 0)
						{
							m_threads.Clear();
							m_table.reloadData();
						}
					}
					break;
				
				case "debugger thread started":
					DoRefreshThreads();
					m_table.reloadData();
					break;
				
				case "debugger thread died":
					var thread = (ThreadMirror) value;
					m_names.Remove(thread.Id);
					DoRefreshThreads();
					m_table.reloadData();
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
				var stack = new LiveStack(m_threads[row]);
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
			return m_threads.Count;
		}
		
		// Developers aren't always nice enough to name threads so we'll let people
		// do it in the debugger.
		public void tableView_setObjectValue_forTableColumn_row(NSTableView table, NSObject value, NSTableColumn col, int row)
		{
			ThreadMirror thread = m_threads[row];
			m_names[thread.Id] = value.description();
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			if (m_threads.Count == 0 || !Debugger.IsRunning)
				return NSString.Empty;
			
			ThreadMirror thread = m_threads[row];
			if (col.identifier().ToString() == "0")
			{
				string name;
				if (m_names.TryGetValue(thread.Id, out name))
					name = string.Format("{0} ({1})", name, thread.Id);
				else
					name = GetThreadName(thread);
				return DoCreateString(name, row);
			}
			else if (col.identifier().ToString() == "1")
			{
				return DoCreateString(thread.ThreadState.ToString(), row);
			}
			else
			{
				return DoCreateString(thread.Domain.FriendlyName, row);
			}
		}
		
		public static string GetThreadName(ThreadMirror thread)
		{
			string name = thread.Name;
			
			if (thread.Id == 1)				// TODO: should we be using ThreadId here?
				name =  "main";
			else if (thread.Id == 2)
				name =  "finalizer";
			
			if (!string.IsNullOrEmpty(name))
				name = string.Format("{0} ({1})", name, thread.Id);
			else
				name = thread.Id.ToString();
				
			return name;
		}
		
		#region Private Methods
		private void DoRefreshThreads()
		{
			m_threads.Clear();
			if (m_debugger != null)
			{
				m_threads.AddRange(m_debugger.VM.GetThreads());
				m_threads.Sort((lhs, rhs) => GetThreadName(lhs).CompareTo(GetThreadName(rhs)));
			}
			
			m_selected = -1;
		}
		
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
		private List<ThreadMirror> m_threads = new List<ThreadMirror>();
		private Dictionary<long, string> m_names = new Dictionary<long, string>();
		private State m_state;
		private int m_selected;
		#endregion
	}
}
