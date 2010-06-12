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
using System.Threading;

namespace Debugger
{
	[ExportClass("VariableController", "NSWindowController", Outlets = "table")]
	internal sealed class VariableController : NSWindowController, IObserver	// Variable instead of Variables because there is already an Objective-C class named VariablesController
	{
		public VariableController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger stopped", this);
			Broadcaster.Register("changed stack frame", this);
			Broadcaster.Register("exiting event loop", this);
			Broadcaster.Register("changed thread", this);
			
			DoLoadPrefs();
			
			Contract.Assert(Instance == null);
			Instance = this;
		}
		
		public static VariableController Instance {get; private set;}
		
		public void Reload()
		{
			if (m_item != null)
				m_table.reloadData();
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger processed breakpoint event":
				case "debugger thrown exception":
				case "debugger processed step event":
					var context = (Context) value;
					var frame = new LiveStackFrame(context.Thread, 0);
					DoReset(frame);
					break;
				
				case "changed thread":
					var stack = (LiveStack) value;
					DoReset(stack[0]);
					break;
				
				case "changed stack frame":
					var frame2 = (LiveStackFrame) value;
					DoReset(frame2);
					break;
				
				case "debugger stopped":
					DoReset(null);
					break;
				
				case "exiting event loop":
					DoReset(null);
					DoSavePrefs();
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
			get {return !ms_hideThousands;}
		}
		
		public static bool ShowUnicode
		{
			get {return !ms_hideUnicode;}
		}
		
		public void toggleHex(NSObject sender)
		{
			ms_showHex = !ms_showHex;
			if (m_item != null)
			{
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
		}
		
		public void toggleThousands(NSObject sender)
		{
			ms_hideThousands = !ms_hideThousands;
			if (m_item != null)
			{
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
		}
		
		public void toggleUnicode(NSObject sender)
		{
			ms_hideUnicode = !ms_hideUnicode;
			if (m_item != null)
			{
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
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
				Unused.Value = sender.Call("setTitle:", NSString.Create("{0} Thousands", ms_hideThousands ? "Show" : "Hide"));
			}
			else if (sel.Name == "toggleUnicode:")
			{
				Unused.Value = sender.Call("setTitle:", NSString.Create("Show Unicode {0}", ms_hideUnicode ? "Characters" : "Code Points"));
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
			try
			{
				string text = value.description();
				Value newValue = ParseValue.Invoke(m_frame.Thread, item, item.Value, text);
				
				SetValue.Invoke(m_frame, item, item.Hint, newValue);
				item.RefreshValue(m_frame.Thread, newValue);
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.Show();
//				transcript.WriteLine(Output.Error, "{0}", e);	
				transcript.WriteLine(Output.Error, "{0}", e.Message);
				if (e.InnerException != null)
					transcript.WriteLine(Output.Error, "   {0}", e.InnerException.Message);
			}
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, VariableItem item)
		{
			if (m_item == null)
				return 0;
			
			return item == null ? m_item.NumberOfChildren : item.NumberOfChildren;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, VariableItem item)
		{
			return item == null ? true : item.NumberOfChildren > 0;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, VariableItem item)
		{
			if (m_item == null)
				return null;
			
			return item == null ? m_item.GetChild(m_frame.Thread, index) : item.GetChild(m_frame.Thread, index);
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, VariableItem item)
		{
			if (m_item == null)
				return NSString.Empty;
			
			if (col.identifier().ToString() == "0")
				return item == null ? m_item.AttributedName : item.AttributedName;
			else if (col.identifier().ToString() == "1")
				return item == null ? m_item.AttributedValue : item.AttributedValue;
			else
				return item == null ? m_item.AttributedType : item.AttributedType;
		}
		
		#region Private Methods
		private void DoReset(LiveStackFrame frame)
		{
			if (m_item != null && ((LiveStackFrame) m_item.Value) == frame)
			{
				m_frame = frame;
				m_item.RefreshValue(m_frame.Thread, frame);
			}
			else
			{
				if (m_item != null)
				{
					m_item.release();
					m_item = null;
				}
				
				if (frame != null)
				{
					m_frame = frame;
					m_item = new VariableItem(m_frame.Thread, "stack frame", null, frame, 0);
				}
			}
			
			m_table.reloadData();
		}
		
		private void DoLoadPrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			// Note that boolForKey returns false if the key does not exist.
			ms_showHex = defaults.boolForKey(NSString.Create("ShowHex Variables"));
			ms_hideThousands = defaults.boolForKey(NSString.Create("HideThousands Variables"));
			ms_hideUnicode = defaults.boolForKey(NSString.Create("HideUnicode Variables"));
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			defaults.setBool_forKey(ms_showHex, NSString.Create("ShowHex Variables"));
			defaults.setBool_forKey(ms_hideThousands, NSString.Create("HideThousands Variables"));
			defaults.setBool_forKey(ms_hideUnicode, NSString.Create("HideUnicode Variables"));
		}
		#endregion
		
		#region Fields
		private NSOutlineView m_table;
		private LiveStackFrame m_frame;
		private VariableItem m_item;
		
		private static bool ms_showHex;
		private static bool ms_hideThousands;
		private static bool ms_hideUnicode;
		#endregion
	}
}
