// Copyright (C) 2009 Jesse Jones
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
using Gear;
using Gear.Helpers;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AutoComplete
{
	[ExportClass("CompletionsTable", "NSTableView")]
	internal sealed class CompletionsTable : NSTableView, IObserver
	{
		private CompletionsTable(IntPtr instance) : base(instance)
		{
			setDataSource(this);
			
			setDoubleAction("doubleClicked:");
			setTarget(this);
			setDelegate(this);
			
			ActiveObjects.Add(this);
		}
		
		public void Open(ITextEditor editor, NSTextView text, Item[] items, string stem, NSTextField label, string defaultLabel)
		{
			m_editor = editor;
			m_text = text;
			m_label = label;
			m_defaultLabel = defaultLabel;
			
			m_candidates = new List<Item>(items);
			m_completed = stem ?? string.Empty;
			m_stem = stem;
			
			m_filter.Clear();
			IEnumerable<string> filters = (from i in items select i.Filter).Distinct();
			bool isEnum = filters.Any(k => k == "System.Enum") && filters.Count() > 2;	// we want enums not Enum
			foreach (string filter in filters)
			{
				if (isEnum)									// for enums default to showing only the enum values
				{
					if (filter == "System.Enum")
						m_filter[filter] =  true;
					else if (filter == "System.Object")
						m_filter[filter] = true;
					else
						m_filter[filter] = false;
				}
				else
					m_filter[filter] =  false;
			}
			
			DoGetAddSpace();
			DoRebuildItems();
			
			if (items.Length == 1)
			{
				DoComplete(false, 0);
			}
			else
			{
				deselectAll(this);
				NSApplication.sharedApplication().BeginInvoke(() => scrollRowToVisible(0));
				
				Broadcaster.Register("directory prefs changed", this);
			}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "directory prefs changed":
					DoGetAddSpace();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void paste(NSObject sender)
		{
			NSPasteboard board = NSPasteboard.generalPasteboard();
			
			NSArray types = NSArray.Create(NSPasteboard.NSStringPboardType);
			NSString best = board.availableTypeFromArray(types);
			if (!NSObject.IsNullOrNil(best))
			{
				NSString text = board.stringForType(best);
				if (!NSObject.IsNullOrNil(text))
				{
					m_completed += text.description();
					
					DoMatchName();
					reloadData();
				}
			}
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
			NSMenu menu = NSMenu.Alloc().initWithTitle(NSString.Empty);
			menu.autorelease();
			
			var filters = new List<string>(m_filter.Keys);
			if (filters.Count > 1)
			{
				filters.Sort();
				
				string extension = null;
				foreach (string filter in filters)
				{
					if (filter != "extension methods")
					{
						string title = (m_filter[filter] ? "Show " : "Hide ") + filter;
						NSMenuItem item = NSMenuItem.Create(title, "toggleFilter:");
						menu.addItem(item);
					}
					else
						extension = filter;
				}
				
				if (extension != null)
				{
					menu.addItem(NSMenuItem.separatorItem());
					
					string title = (m_filter[extension] ? "Show " : "Hide ") + extension;
					NSMenuItem item = NSMenuItem.Create(title, "toggleFilter:");
					menu.addItem(item);
				}
			}
			
			return menu;
		}
		
		public void toggleFilter(NSMenuItem sender)
		{
			string name = sender.title().description();
			int i = name.IndexOf(' ');
			name = name.Substring(i + 1);
			m_filter[name] = !m_filter[name];
			
			DoRebuildItems();
		}
		
		public new void keyDown(NSEvent evt)
		{
			NSString chars = evt.characters();
			
			if (chars.Equals("\t") || chars.Equals("\r") || chars.Equals(" "))
			{
				DoComplete(false, selectedRow());
			}
			else if (evt.keyCode() == 76)		// enter key
			{
				DoComplete(true, selectedRow());
			}
			else if (chars.Equals(Constants.Escape))
			{
				m_text = null;
				window().windowController().Call("hide");
			}
			else if (chars.length() == 1 && (char.IsLetterOrDigit(chars[0]) || chars[0] == '_'))
			{
				m_completed += chars[0];
				int count = DoMatchName();
				if (count > 0)
				{
					reloadData();
				}
				else if (m_completed.Length == 1)
				{
					// It's rather confusing to have completed text without any indication
					// that there is completed text so if the user's completed text is completely
					// bogus we'll reset it.
					m_completed = string.Empty;
				}
			}
			else if (chars.Equals(Constants.Delete))
			{
				if (m_completed.Length > 0)
				{
					m_completed = m_completed.Remove(m_completed.Length - 1);
					DoMatchName();
					reloadData();
				}
				else
					Functions.NSBeep();
			}
			else if (chars.length() == 1 && chars[0] == ' ')
			{
				DoMatchName();		// just select the best match
			}
			else
			{
				Unused.Value = SuperCall("keyDown:", evt);
			}
		}
		
		public void doubleClicked(NSObject sender)
		{
			DoComplete(false, selectedRow());
		}
		
		public void tableViewSelectionDidChange(NSNotification notification)
		{
			int row = selectedRow();
			if (row >= 0)
			{
				Item item = m_items[row];
				var str = NSMutableAttributedString.Create(item.Label);
				
				MethodItem method = item as MethodItem;
				if (method != null)
				{
					NSRange range = method.GetNameRange();
					str.addAttribute_value_range(Externs.NSStrokeWidthAttributeName, NSNumber.Create(-3.0f), range);
				}
				else if (item.Label.Contains(" "))
				{
					int i = item.Label.LastIndexOf(' ');
					NSRange range = new NSRange(i + 1, item.Label.Length - (i + 1));
					str.addAttribute_value_range(Externs.NSStrokeWidthAttributeName, NSNumber.Create(-3.0f), range);
				}
				
				NSMutableParagraphStyle style = NSMutableParagraphStyle.Create();
				style.setAlignment(Enums.NSCenterTextAlignment);
				str.addAttribute_value_range(Externs.NSParagraphStyleAttributeName, style, new NSRange(0, item.Text.Length));
				
				m_label.setObjectValue(str);
			}
			else
				m_label.setStringValue(NSString.Create(m_defaultLabel));
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_items.Count;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			NSObject result;
			
			string name = m_items[row].Text;
			int n = DoCountMatching(name);
			if (n > 0)
			{
				var str = NSMutableAttributedString.Create(name);
				
				NSRange range = new NSRange(0, n);
				str.addAttribute_value_range(Externs.NSForegroundColorAttributeName, NSColor.greenColor(), range);
				if (n < m_completed.Length)
				{
					range = new NSRange(n, Math.Min(m_completed.Length, name.Length) - n);
					str.addAttribute_value_range(Externs.NSForegroundColorAttributeName, NSColor.redColor(), range);
				}
				
				result = str;
			}
			else
			{
				result = NSString.Create(name);
			}
			
			return result;
		}
		
		#region Private Methods
		private void DoSortItems()
		{
			m_items.Sort((lhs, rhs) =>
			{
				int result = 0;
				
				MethodItem m1 = lhs as MethodItem;
				MethodItem m2 = rhs as MethodItem;
				
				if (m1 != null && m2 != null)
				{
					result = m1.Name.CompareTo(m2.Name);
					
					if (result == 0)
						result = m1.Arity.CompareTo(m2.Arity);
				}
				
				if (result == 0)
					result = lhs.Text.CompareTo(rhs.Text);
				
				return result;
			});
		}
		
		private void DoRebuildItems()
		{
			m_items.Clear();
			
			foreach (Item member in m_candidates)
			{
				if (!m_filter[member.Filter])
					m_items.Add(member);
			}
			
			DoSortItems();
			reloadData();
		}
		
		private void DoComplete(bool onlyTypedText, int row)
		{
			if (row >= 0)
			{
				int firstIndex = m_text.selectedRange().location;
				string text = DoGetInsertText(row, onlyTypedText);
				
				if (m_stem != null)
				{
					// If we have a stem then we also have the first tab so we need to do a replace
					// instead of an insert. 
					NSRange range = new NSRange(firstIndex - 1, 1);
					string name = m_stem.Length > 0 ? string.Format(" {0}{1}{2}", Constants.LeftDoubleQuote, m_stem, Constants.RightDoubleQuote) : string.Empty;
					var suffix = NSString.Create(text.Substring(m_stem.Length));
					
					m_text.shouldChangeTextInRange_replacementString(range, suffix);
					m_text.undoManager().setActionName(NSString.Create("Complete" + name));
					m_text.replaceCharactersInRange_withString(range, suffix);
					m_text.didChangeText();
					
					firstIndex -= m_stem.Length + 1;
				}
				else
				{
					NSAttributedString str = NSAttributedString.Create(text);
					m_text.shouldChangeTextInRange_replacementString(new NSRange(firstIndex, 0), str.string_());
					m_text.undoManager().setActionName(NSString.Create("Complete"));
					m_text.textStorage().insertAttributedString_atIndex(str, (uint) firstIndex);
					m_text.didChangeText();
				}
				
				m_text = null;
				if (window().isVisible())
					window().windowController().Call("hide");
					
				MethodItem method = m_items[row] as MethodItem;
				bool annotate = method != null && (method.Arity > 0 || method.GetArgumentRange(-1).length > 0);
				if (!onlyTypedText && annotate)
				{
					NSRange range = new NSRange(firstIndex, text.Length);
					ITextAnnotation annotation = m_editor.GetAnnotation(range);
					
					IArgsAnnotation args = m_editor.Boss.Get<IArgsAnnotation>();
					string name = method.Name;
					var methods = (from m in m_items where m is MethodItem && m.Text.StartsWith(name) select (MethodItem) m).ToArray();
					int j = Array.FindIndex(methods, m => m.Text == m_items[row].Text);
					args.Open(annotation, methods, j);
				}
			}
			else
				Functions.NSBeep();
		}
		
		private string DoGetInsertText(int row, bool onlyTypedText)
		{
			string text;
			
			if (onlyTypedText)
			{
				text = m_completed;
			}
			else
			{
				MethodItem method = m_items[row] as MethodItem;
				if (method != null)
				{
					text = method.Name;
					
					if (method.GetArgumentRange(-1).length > 0)
						text += "<";
						
					else if (m_addSpace)
						if (method.Arity == 0)
							text += " ()";
						else
							text += " (";
							
					else
						if (method.Arity == 0)
							text += "()";
						else
							text += "(";
				}
				else
					text = m_items[row].Text;
			}
			
			return text;
		}
		
		private int DoMatchName()
		{
			int index = -1;
			int count = 0;
			
			for (int i = 0; i < m_items.Count; ++i)
			{
				int n = DoCountMatching(m_items[i].Text);
				if (n > count)
				{
					index = i;
					count = n;
				}
				else if (count == 0 && m_completed.Length > 0 && char.ToLower(m_items[i].Text[0]) < char.ToLower(m_completed[0]))	// if there's no match we still want to select a name near what the user typed
				{
					index = i;
				}
			}
			
			if (index >= 0)
			{
				var indexes = NSIndexSet.indexSetWithIndex((uint) index);
				selectRowIndexes_byExtendingSelection(indexes, false);
				
				// We want to show the rows around the selected row so we call
				// scrollRowToVisible twice.
				int row = Math.Max(index - 2, 0);
				scrollRowToVisible(row);
				
				row = Math.Min(index + 2, m_items.Count - 1);
				scrollRowToVisible(row);
			}
			else
			{
				deselectAll(this);
			}
			
			return count;
		}
		
		private int DoCountMatching(string name)
		{
			int count = 0;
			
			for (int i = 0; i < Math.Min(name.Length, m_completed.Length) && char.ToLower(name[i]) == char.ToLower(m_completed[i]); ++i)
			{
				++count;
			}
			
			return count;
		}
		
		private void DoGetAddSpace()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(m_editor.Boss);
			
			if (boss != null)
			{
				var editor = boss.Get<IDirectoryEditor>();
				m_addSpace = editor.AddSpace;
			}
		}
		#endregion
		
		#region Fields
		private ITextEditor m_editor;
		private NSTextView m_text;
		private NSTextField m_label;
		private string m_defaultLabel;
		private List<Item> m_candidates = new List<Item>();
		private List<Item> m_items = new List<Item>();
		private string m_completed = string.Empty;
		private string m_stem;
		private Dictionary<string, bool> m_filter = new Dictionary<string, bool>();
		private bool m_addSpace;
		#endregion
	}
}
