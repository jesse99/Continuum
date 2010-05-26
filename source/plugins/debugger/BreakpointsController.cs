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
	[ExportClass("BreakpointsController", "NSWindowController", Outlets = "table")]
	internal sealed class BreakpointsController : NSWindowController, IObserver
	{
		public BreakpointsController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			
			Broadcaster.Register("debugger resolved breakpoint", this);
			Broadcaster.Register("debugger unresolved breakpoint", this);
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger state changed", this);
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger stopped", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":
					var d1 = (Debugger) value;
					d1.BreakpointCondition = this.DoEvaluateCondition;
					break;
				
				case "debugger stopped":
					var d2 = (Debugger) value;
					d2.BreakpointCondition = null;
					break;
				
				case "debugger resolved breakpoint":
					var rbp = (ResolvedBreakpoint) value;
					DoAdd(rbp.BreakPoint.File, rbp.BreakPoint.Line, rbp.Method.GetFullerName());
					break;
				
				case "debugger unresolved breakpoint":
					var bp = (Breakpoint) value;
					DoRemove(bp.File, bp.Line);
					m_table.reloadData();
					break;
				
				case "debugger processed breakpoint event":
					var context = (Context) value;
					m_selected = new ConditionalBreakpoint(context.Location.SourceFile, context.Location.LineNumber);
					m_table.reloadData();
					break;
				
				case "debugger state changed":
					var state = (State) value;
					if (state == State.Running)
					{
						m_selected = null;
						m_table.reloadData();
					}
					else if (state == State.Disconnected)
					{
						m_breakpoints.Clear();
						m_selected = null;
						m_table.reloadData();
					}
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public new void keyDown(NSEvent evt)
		{
			if  (evt.keyCode() == Constants.DeleteKey)
			{
				Boss boss = ObjectModel.Create("Application");
				var breakpoints = boss.Get<IBreakpoints>();
				
				var bps = (from row in m_table.selectedRowIndexes() select m_breakpoints[(int) row]).ToArray();	// ToArray so that the expression is not lazy
				foreach (ConditionalBreakpoint bp in bps)
				{
					breakpoints.Remove(bp.File, bp.Line);
					
					if (m_selected == bp)
						m_selected = null;
				}
				m_table.reloadData();
			}
		}
		
		public void doubleClicked(NSObject sender)
		{
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			
			var bps = (from row in m_table.selectedRowIndexes() select m_breakpoints[(int) row]).ToArray();	// ToArray so that the expression is not lazy
			foreach (ConditionalBreakpoint bp in bps)
			{
				launcher.Launch(bp.File, bp.Line, -1, 1);
			}
		}
		
		public void tableView_setObjectValue_forTableColumn_row(NSTableView table, NSObject value, NSTableColumn col, int row)
		{
			try
			{
				string text = value.description();
				var parser = new ExpressionParser();
				Expression expr = parser.Parse(text);
		
				m_breakpoints[row].Condition = expr;
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.Show();
				transcript.WriteLine(Output.Error, "{0}", e.Message);
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_breakpoints.Count;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			ConditionalBreakpoint bp = m_breakpoints[row];
			
			if (col.identifier().ToString() == "0")
				return DoCreateString(bp.FileName, row);
			
			else if (col.identifier().ToString() == "1")
				return DoCreateString(bp.Line.ToString(), row);
			
			else if (col.identifier().ToString() == "2")
				return DoCreateString(bp.Condition.ToString(), row);
			
			else
				return DoCreateString(bp.Method, row);
		}
		
		#region Private Methods
		private DebuggerThread.HandlerAction DoEvaluateCondition(StackFrame frame, Breakpoint bp)
		{
			var result = DebuggerThread.HandlerAction.Suspend;
			
			try
			{
				var key = new ConditionalBreakpoint(bp.File, bp.Line);
				ConditionalBreakpoint cbp = m_breakpoints.First(b => b == key);
				
				ExtendedValue value = cbp.Condition.Evaluate(frame);
				bool stop = value.Get<bool>();
				if (!stop)
					result = DebuggerThread.HandlerAction.Resume;
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.Show();
				transcript.WriteLine(Output.Error, "Couldn't evaluate the breakpoint condition: {0}", e.Message);
			}
			
			return result;
		}
		
		private void DoAdd(string file, int line, string method)
		{
			var bp = new ConditionalBreakpoint(file, line, method);
			if (!m_breakpoints.Contains(bp))
			{
				m_breakpoints.Add(bp);
				m_breakpoints.Sort((lhs, rhs) =>
				{
					int result = lhs.FileName.CompareTo(rhs.FileName);
					
					if (result == 0)
						result = lhs.Line.CompareTo(rhs.Line);
					
					return result;
				});
			}
		}
		
		private void DoRemove(string file, int line)
		{
			var bp = new ConditionalBreakpoint(file, line);
			m_breakpoints.Remove(bp);
		}
		
		private NSObject DoCreateString(string text, int row)
		{
			ConditionalBreakpoint bp = m_breakpoints[row];
			
			NSColor color = null;
			if (bp == m_selected)
				color = NSColor.blueColor();
			
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
		
		#region Private Types
		private sealed class ConditionalBreakpoint : IEquatable<ConditionalBreakpoint>
		{
			public ConditionalBreakpoint(string file, int line) : this(file, line, null)
			{
			}
			
			public ConditionalBreakpoint(string file, int line, string method)
			{
				File = file;
				Line = line;
				Condition = new Literal<bool>(true);
				Method = method;
			}
			
			public string File {get; private set;}
			
			public string FileName
			{
				get {return System.IO.Path.GetFileName(File);}
			}
			
			public int Line {get; private set;}
			
			public Expression Condition {get; set;}
			
			public string Method {get; private set;}
			
			#region Overrides
			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				
				ConditionalBreakpoint rhs = obj as ConditionalBreakpoint;
				return this == rhs;
			}
			
			public bool Equals(ConditionalBreakpoint rhs)
			{
				return this == rhs;
			}
			
			public static bool operator==(ConditionalBreakpoint lhs, ConditionalBreakpoint rhs)
			{
				if (object.ReferenceEquals(lhs, rhs))
					return true;
				
				if ((object) lhs == null || (object) rhs == null)
					return false;
				
				if (lhs.File != rhs.File)
					return false;
				
				if (lhs.Line != rhs.Line)
					return false;
					
				// Note that we don't watch to use method because it isn't always set
				// when we compare breakpoints.
				
				return true;
			}
			
			public static bool operator!=(ConditionalBreakpoint lhs, ConditionalBreakpoint rhs)
			{
				return !(lhs == rhs);
			}
			
			public override int GetHashCode()
			{
				int hash = 0;
				
				unchecked
				{
					hash += File.GetHashCode();
					hash += Line.GetHashCode();
				}
				
				return hash;
			}
			#endregion
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private List<ConditionalBreakpoint> m_breakpoints = new List<ConditionalBreakpoint>();
		private ConditionalBreakpoint m_selected;
		#endregion
	}
}
