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
using System.Text.RegularExpressions;
using System.Threading;

namespace Debugger
{
	[ExportClass("VariableController", "NSWindowController", Outlets = "table")]
	internal sealed class VariableController : NSWindowController, IObserver	// Variable instead of Variables because there is already an Objective-C class named VariablesController
	{
		public VariableController(IntPtr instance) : base(instance)
		{
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			
			Broadcaster.Register("debugger break all", this);
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger stopped", this);
			Broadcaster.Register("changed stack frame", this);
			Broadcaster.Register("exiting event loop", this);
			Broadcaster.Register("changed thread", this);
			
			DoLoadPrefs();
			
			Contract.Assert(Instance == null);
			Instance = this;
		}
		
		public static VariableController Instance {get; private set;}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":
					m_debugger = (Debugger) value;
					break;
					
				case "debugger processed breakpoint event":
				case "debugger thrown exception":
				case "debugger break all":
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
					m_debugger = null;
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
		
		public string GetValue(string name)
		{
			string value;
			Unused.Value = m_nameTable.TryGetValue(name, out value);
			return value;
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
				if (m_frame != null)
					DoLoadNameTable(m_frame.Thread, m_item);
				
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
		}
		
		public void toggleThousands(NSObject sender)
		{
			ms_hideThousands = !ms_hideThousands;
			if (m_item != null)
			{
				if (m_frame != null)
					DoLoadNameTable(m_frame.Thread, m_item);
				
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
		}
		
		public void toggleUnicode(NSObject sender)
		{
			ms_hideUnicode = !ms_hideUnicode;
			if (m_item != null)
			{
				if (m_frame != null)
					DoLoadNameTable(m_frame.Thread, m_item);
				
				m_item.Refresh(m_frame.Thread);
				m_table.reloadData();
			}
		}
		
		public void showDetails(NSObject sender)
		{
			foreach (uint row in m_table.selectedRowIndexes())
			{
				var item = (VariableItem) (m_table.itemAtRow((int) row));
				DoShowDetails(item.AttributedName.ToString(), item.AttributedType.ToString(), item.Value);
			}
		}
		
		public void showLiveObjects(NSObject sender)
		{
			var getter = new GetString{Title = "Show Live Objects", Label = "Type:", Text = "[\\w.]+"};
			string pattern = getter.Run();
			if (pattern != null)
			{
				try
				{
					var re = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
					Func<string, object, bool> filter = (type, obj) => re.IsMatch(type);
					DoShowRoots(string.Format("({0}) Objects", pattern), filter);			// add parens so GetFileExtension doesn't get confused
				}
				catch (ArgumentException e)
				{
					Boss boss = ObjectModel.Create("Application");
					var transcript = boss.Get<ITranscript>();
					transcript.Show();
					transcript.WriteLine(Output.Error, "{0}", e.Message);
				}
			}
		}
		
		public void showTypeRoots(NSObject sender)
		{
			string type = DoGetSelectedType();
			Func<string, object, bool> filter = (inType, obj) => inType == type;
			DoShowRoots(type + " Roots", filter);
		}
		
		public void showInstanceRoots(NSObject sender)
		{
			VariableItem selected = DoGetSelectedObject();
			var instance = selected.Value as ObjectMirror;
			
			if (instance == null)
			{
				InstanceValue iv = selected.Value as InstanceValue;
				instance = (ObjectMirror) iv.Instance;
			}
			
			Func<string, object, bool> filter = (type, obj) =>
			{
				var value = obj as ObjectMirror;
				if (value != null)
					return value.Address == instance.Address;
				else
					return false;
			};
			DoShowRoots(selected.AttributedName + " Roots", filter);
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
			else if (sel.Name == "showLiveObjects:")
			{
			}
			else if (sel.Name == "showTypeRoots:")
			{
				string type = DoGetSelectedType();
				Unused.Value = sender.Call("setTitle:", NSString.Create("Show Roots for {0}", type));
			}
			else if (sel.Name == "showInstanceRoots:")
			{
				VariableItem instance = DoGetSelectedObject();
				if (instance != null)
				{
					Unused.Value = sender.Call("setTitle:", NSString.Create("Show Roots for {0}", instance.AttributedName.ToString()));
				}
				else
				{
					Unused.Value = sender.Call("setTitle:", NSString.Create("Show Roots for Object"));
					enabled = false;
				}
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
		
		public void copy(NSObject sender)
		{
			m_table.Copy();
		}
		
		public void outlineView_setObjectValue_forTableColumn_byItem(NSTableView table, NSObject value, NSTableColumn col, VariableItem item)
		{
			try
			{
				string text = value.description();
				Value newValue = ParseValue.Invoke(m_frame.Thread, item, item.Value, text);
				
				SetValue.Invoke(m_frame.Thread, item, item.Parent.Value, item.Key, newValue);
				item.RefreshValue(m_frame.Thread, newValue);			// if we're setting a (non-auto) property we might also want to refresh all fields for that type
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
//				transcript.Show();
//				transcript.WriteLine(Output.Error, "{0}", e);
				transcript.WriteLine(Output.Error, "{0}", e.Message);
				if (e.InnerException != null)
					transcript.WriteLine(Output.Error, "   {0}", e.InnerException.Message);
			}
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, VariableItem item)
		{
			int count = 0;
			
			try
			{
				if (m_item != null)
					count = item == null ? m_item.NumberOfChildren : item.NumberOfChildren;
			}
			catch (Exception e)
			{
				if (!Debugger.IsShuttingDown(e))
					throw;
			}
			
			return count;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, VariableItem item)
		{
			bool expandable = false;
			
			try
			{
				expandable = item == null ? true : item.NumberOfChildren > 0;
			}
			catch (Exception e)
			{
				if (!Debugger.IsShuttingDown(e))
					throw;
			}
			
			return expandable;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, VariableItem item)
		{
			NSObject child = null;
			
			try
			{
				if (m_item != null)
					child = item == null ? m_item.GetChild(m_frame.Thread, index) : item.GetChild(m_frame.Thread, index);
			}
			catch (Exception e)
			{
				if (!Debugger.IsShuttingDown(e))
					throw;
			}
			
			return child;
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, VariableItem item)
		{
			NSObject value = null;
			
			try
			{
				if (m_item != null)
				{
					if (col.identifier().ToString() == "0")
						value = item == null ? m_item.AttributedName : item.AttributedName;
					else if (col.identifier().ToString() == "1")
						value = item == null ? m_item.AttributedValue : item.AttributedValue;
					else
						value = item == null ? m_item.AttributedType : item.AttributedType;
				}
			}
			catch (Exception e)
			{
				if (!Debugger.IsShuttingDown(e))
					throw;
			}
			
			return value;
		}
		
		#region Private Methods
		private void DoShowRoots(string title, Func<string, object, bool> filter)
		{
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			string file = fs.GetTempFile(title, ".txt");
			using (var stream = new System.IO.StreamWriter(file))
			{
				IList<ThreadMirror> threads = m_frame.VirtualMachine.GetThreads();
				FieldInfoMirror[] staticFields = m_debugger.GetStaticFields();
				
				var tracer = new TraceRoots(threads, staticFields);
				foreach (Trace trace in tracer.Walk(filter))
				{
					trace.Write(stream, m_frame.Thread, 0);
					stream.WriteLine();
				}
			}
			
			boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(file, -1, -1, 1);
		}
		
		private string DoGetSelectedType()
		{
			string type;
			
			int index = m_table.selectedRow();
			if (index >= 0)
			{
				var item = m_table.itemAtRow(index).To<VariableItem>();
				type = item.AttributedType.ToString();
			}
			else
			{
				type = m_frame.Method.DeclaringType.FullName;
			}
			
			return type;
		}
		
		private VariableItem DoGetSelectedObject()
		{
			VariableItem item = null;
			
			int index = m_table.selectedRow();
			if (index >= 0)
			{
				var candidate = m_table.itemAtRow(index).To<VariableItem>();
				if (candidate.Value is ObjectMirror)
					item = candidate;
				
				InstanceValue instance = candidate.Value as InstanceValue;
				if (instance != null && instance.Instance is ObjectMirror)
					item = candidate;
			}
			
			return item;
		}
		
		private void DoReset(LiveStackFrame frame)
		{
			m_nameTable.Clear();
			
			if (frame != null && m_item != null && ((LiveStackFrame) m_item.Value) == frame)
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
					m_item = new VariableItem(m_frame.Thread, frame);
				}
				else
				{
					m_frame = null;
				}
			}
			
			if (m_frame != null)
				DoLoadNameTable(m_frame.Thread, m_item);
			
			m_table.reloadData();
		}
		
		private void DoLoadNameTable(ThreadMirror thread, VariableItem parent)
		{
			for (int i = 0; i < parent.NumberOfChildren; ++i)
			{
				VariableItem child = parent.GetChild(thread, i);
				
				string value = child.AttributedValue.ToString();
				if (value.Length > 0)
					m_nameTable[child.AttributedName.ToString()] = value;
					
				var iv = child.Value as InstanceValue;
				if (iv != null)
				{
					for (int j = 0; j < iv.Length; ++j)
					{
						VariableItem gchild = iv.GetChild(thread, child, j);
						gchild.autorelease();
						
						value = gchild.AttributedValue.ToString();
						if (value.Length > 0)
							m_nameTable[gchild.AttributedName.ToString()] = value;
					}
				}
					
				var tv = child.Value as TypeValue;
				if (tv != null)
				{
					for (int j = 0; j < tv.Length; ++j)
					{
						VariableItem gchild = tv.GetChild(thread, child, j);
						gchild.autorelease();
						
						value = gchild.AttributedValue.ToString();
						if (value.Length > 0)
							m_nameTable[gchild.AttributedName.ToString()] = value;
					}
				}
			}
		}
		
		private void DoShowDetails(string name, string type, object obj)
		{
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			string file = fs.GetTempFile(name, ".txt");
			using (var writer = new System.IO.StreamWriter(file))
			{
				writer.WriteLine(type);
				writer.WriteLine();
				Details.Write(writer, m_frame.Thread, obj, ShowThousands);
			}
			
			boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(file, -1, -1, 1);
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
		private Debugger m_debugger;
		private NSOutlineView m_table;
		private LiveStackFrame m_frame;
		private VariableItem m_item;
		private Dictionary<string, string> m_nameTable = new Dictionary<string, string>();
		
		private static bool ms_showHex;
		private static bool ms_hideThousands;
		private static bool ms_hideUnicode;
		#endregion
	}
}
