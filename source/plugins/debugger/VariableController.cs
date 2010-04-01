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

namespace Debugger
{
	[ExportClass("VariableController", "NSWindowController", Outlets = "table")]
	internal sealed class VariableController : NSWindowController, IObserver	// Variable instead of Variables because there is already an Objective-C class named VariablesController
	{
		public VariableController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger stopped", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger processed breakpoint event":	
					var context = (Context) value;
					StackFrame[] frames = context.Thread.GetFrames();
					DoReset(frames[0]);
					break;
				
				case "debugger processed step event":
					var context2 = (Context) value;
					StackFrame[] frames2 = context2.Thread.GetFrames();
					DoReset(frames2[0]);
					break;
				
				case "debugger stopped":
					DoReset(null);
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public static bool ShowHex
		{
			get {return ms_showHex;}
		}
		
		public static bool ShowThousands
		{
			get {return ms_showThousands;}
		}
		
		public static bool ShowUnicode
		{
			get {return ms_showUnicode;}
		}
		
		public void toggleHex(NSObject sender)
		{
			ms_showHex = !ms_showHex;
			m_method.Refresh(null);
			m_table.reloadData();
		}
		
		public void toggleThousands(NSObject sender)
		{
			ms_showThousands = !ms_showThousands;
			m_method.Refresh(null);
			m_table.reloadData();
		}
		
		public void toggleUnicode(NSObject sender)
		{
			ms_showUnicode = !ms_showUnicode;
			m_method.Refresh(null);
			m_table.reloadData();
		}
		
		public bool validateUserInterfaceItem(NSObject sender)
		{
			bool enabled = true;
			
			Selector sel = (Selector) sender.Call("action");
			
			if (sel.Name == "toggleHex:")
			{
				Unused.Value = sender.Call("setTitle:", NSString.Create("Show {0}", ms_showHex ? "Decimal" : "Hex"));
			}
			else if (sel.Name == "toggleThousands:")
			{
				Unused.Value = sender.Call("setTitle:", NSString.Create("{0} Thousands", ms_showThousands ? "Hide" : "Show"));
			}
			else if (sel.Name == "toggleUnicode:")
			{
				Unused.Value = sender.Call("setTitle:", NSString.Create("Show {0}", ms_showUnicode ? "Code Point" : "Unicode"));
			}
			else if (respondsToSelector(sel))
			{
			}
			else if (SuperCall(NSWindowController.Class, "respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				enabled = SuperCall(NSWindowController.Class, "validateUserInterfaceItem:", sender).To<bool>();
			}
			
			return enabled;
		}
		
		public void outlineView_setObjectValue_forTableColumn_byItem(NSTableView table, NSObject value, NSTableColumn col, VariableItem item)
		{
			string text = value.description();
			item.SetValue(text);
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, VariableItem item)
		{
			if (m_method == null)
				return 0;
			
			return item == null ? m_method.Count : item.Count;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, VariableItem item)
		{
			return item == null ? true : item.IsExpandable;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, VariableItem item)
		{
			if (m_method == null)
				return null;
			
			return item == null ? m_method[index] : item[index];
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, VariableItem item)
		{
			if (m_method == null)
				return NSString.Empty;
			
			if (col.identifier().ToString() == "0")
				return item == null ? m_method.Name : item.Name;
			else if (col.identifier().ToString() == "1")
				return item == null ? m_method.Value : item.Value;
			else
				return item == null ? m_method.TypeName : item.TypeName;
		}
		
		#region Private Methods
		private void DoReset(StackFrame frame)
		{
			if (m_method != null && m_method.Frame.Matches(frame))
			{
				m_method.Refresh(frame);
			}
			else
			{
				if (m_method != null)
				{
					m_method.release();
					m_method = null;
				}
				
				if (frame != null)
					m_method = new MethodValueItem(frame);
			}
			
			m_table.reloadData();
		}
		#endregion
		
		#region Fields
		private NSOutlineView m_table;
		private MethodValueItem m_method;
		
		private static bool ms_showHex;
		private static bool ms_showThousands = true;
		private static bool ms_showUnicode;
		#endregion
	}
}
