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
	internal sealed class CompletionsTable : NSTableView
	{
		private CompletionsTable(IntPtr instance) : base(instance)
		{
			setDataSource(this);
			
			setDoubleAction("doubleClicked:");
			setTarget(this);
			setDelegate(this);
			
			ActiveObjects.Add(this);
		}
		
		public void Open(string type, ITextEditor editor, NSTextView text, Member[] members, int prefixLen, NSTextField label, string defaultLabel)
		{
			m_editor = editor;
			m_text = text;
			m_label = label;
			m_defaultLabel = defaultLabel;
			
			m_candidates = new List<Member>(members);
			m_completed = string.Empty;
			m_prefixLen = prefixLen;
			m_hasExtensions = m_candidates.Exists(m => m.IsExtensionMethod);
			m_showExtensions = true;
			
			m_visibleClasses.Clear();
			string[] classes = DoGetClasses();
			bool isEnum = classes.Any(k => k == "System.Enum");
			foreach (string klass in classes)
			{
				if (isEnum)									// for enums default to showing only the enum values
				{
					if (klass == "System.Enum")
						m_visibleClasses.Add(klass, false);
					else if (klass == "System.Object")
						m_visibleClasses.Add(klass, false);
					else
						m_visibleClasses.Add(klass, true);
				}
				else
					m_visibleClasses.Add(klass, true);
			}
			
			DoRebuildMembers();
			deselectAll(this);
			NSApplication.sharedApplication().BeginInvoke(() => scrollRowToVisible(0));
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
			NSMenu menu = NSMenu.Alloc().initWithTitle(NSString.Empty);
			menu.autorelease();
			
			var classes = new List<string>(m_visibleClasses.Keys);
			classes.Sort();
			
			foreach (string klass in classes)
			{
				string title = (m_visibleClasses[klass] ? "Hide " : "Show ") + klass;
				NSMenuItem item = NSMenuItem.Create(title, "toggleClass:");
				menu.addItem(item);
			}
			
			if (m_hasExtensions)
			{
				menu.addItem(NSMenuItem.separatorItem());
				
				string title = m_showExtensions ? "Hide  Extension Methods" : "Show Extension Methods";
				NSMenuItem item = NSMenuItem.Create(title, "toggleExtensions:");
				menu.addItem(item);
			}
			
			return menu;
		}
		
		public void toggleClass(NSMenuItem sender)
		{
			string name = sender.title().description();
			int i = name.IndexOf(' ');
			name = name.Substring(i + 1);
			m_visibleClasses[name] = !m_visibleClasses[name];
			
			DoRebuildMembers();
		}
		
		public void toggleExtensions(NSMenuItem sender)
		{
			m_showExtensions = !m_showExtensions;
			DoRebuildMembers();
		}
		
		public new void keyDown(NSEvent evt)
		{
			NSString chars = evt.characters();
			
			if (chars == "\t" || chars == "\r")
			{
				DoComplete(false);
			}
			else if (evt.keyCode() == 76)		// enter key
			{
				DoComplete(true);
			}
			else if (chars == Constants.Escape)
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
			else if (chars == Constants.Delete)
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
			DoComplete(false);
		}
		
		public void tableViewSelectionDidChange(NSNotification notification)
		{
			int row = selectedRow();
			if (row >= 0)
			{
				Member member = m_members[row];

				int i = member.Text.IndexOf('(');
				if (i < 0)
					i = member.Text.Length;
				string text = member.Type + " " + member.Text.Replace(";", ", ");
				var str = NSMutableAttributedString.Create(text);
				
				NSRange range = new NSRange(member.Type.Length + 1, i);
				str.addAttribute_value_range(Externs.NSStrokeWidthAttributeName, NSNumber.Create(-3.0f), range);
				
				NSMutableParagraphStyle style = NSMutableParagraphStyle.Create();
				style.setAlignment(Enums.NSCenterTextAlignment);
				str.addAttribute_value_range(Externs.NSParagraphStyleAttributeName, style, new NSRange(0, text.Length));
				
				m_label.setObjectValue(str);
			}
			else
				m_label.setStringValue(NSString.Create(m_defaultLabel));
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_members.Count;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			NSObject result;
			
			string name = m_members[row].Text.Replace(";", ", ");
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
		private void DoSortMembers()
		{
			m_members.Sort((lhs, rhs) =>
			{
				int result = lhs.Name.CompareTo(rhs.Name);
				
				if (result == 0)
					result = lhs.Arity.CompareTo(rhs.Arity);
					
				if (result == 0)
					result = lhs.Text.CompareTo(rhs.Text);
				
				return result;
			});
		}
		
		private void DoRebuildMembers()
		{
			m_members.Clear();
			
			foreach (Member member in m_candidates)
			{
				if (member.IsExtensionMethod)
				{
					if (m_showExtensions)
						m_members.Add(member);
				}
				else
				{
					if (member.DeclaringType == null)
						m_members.Add(member);
					else if (m_visibleClasses[member.DeclaringType])
						m_members.Add(member);
				}
			}
			
			DoSortMembers();
			reloadData();
		}
		
		private string[] DoGetClasses()
		{
			var classes = new List<string>();
			
			foreach (Member member in m_candidates)
			{
				if (member.DeclaringType != null && !member.DeclaringType.Contains("-"))
					classes.AddIfMissing(member.DeclaringType);
			}
			
			return classes.ToArray();
		}
		
		private void DoComplete(bool prefixOnly)
		{
			int row = selectedRow();
			if (row >= 0)
			{
				string text;
				NSRange range;
				DoGetInsertText(row, prefixOnly, out text, out range);
				
				if (m_prefixLen <= text.Length)
				{
					m_text.delete(this);
					m_text.insertText(NSString.Create(text.Substring(m_prefixLen)));
					
					if (range.length > 0)
						m_text.setSelectedRange(range);
					else
						m_text.setSelectedRange(new NSRange(range.location + text.Length - m_prefixLen, 0));
				}
				
				m_text = null;
				window().windowController().Call("hide");
				
				if (!prefixOnly && m_members[row].Arity > 0)
				{
					range.length = text.Length;
					ITextAnnotation annotation = m_editor.GetAnnotation(range);
					
					IArgsAnnotation args = m_editor.Boss.Get<IArgsAnnotation>();
					string name = m_members[row].Name;
					var members = (from m in m_members where m.Text.StartsWith(name) select m).ToArray();
					int j = Array.FindIndex(members, m => m.Text == m_members[row].Text);
					args.Open(annotation, members, j);
				}
			}
			else
				Functions.NSBeep();
		}
		
		private void DoGetInsertText(int row, bool prefixOnly, out string text, out NSRange range)
		{
			range = m_text.selectedRange();
			range.length = 0;
			
			if (prefixOnly)
			{
				text = m_completed;
			}
			else
			{
				int i = m_members[row].Text.IndexOf('(');
				if (i > 0)
					if (m_members[row].Arity > 0)
						text = m_members[row].Text.Substring(0, i + 1);
					else
						text = m_members[row].Text.Substring(0, i + 2);
				else
					text = m_members[row].Text;
			}
		}
		
		private int DoMatchName()
		{
			int index = -1;
			int count = 0;
			
			for (int i = 0; i < m_members.Count; ++i)
			{
				int n = DoCountMatching(m_members[i].Text);
				if (n > count)
				{
					index = i;
					count = n;
				}
				else if (count == 0 && m_completed.Length > 0 && char.ToLower(m_members[i].Text[0]) < char.ToLower(m_completed[0]))	// if there's no match we still want to select a name near what the user typed
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
				
				row = Math.Min(index + 2, m_members.Count - 1);
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
		#endregion
		
		#region Fields
		private ITextEditor m_editor;
		private NSTextView m_text;
		private NSTextField m_label;
		private string m_defaultLabel;
		private List<Member> m_candidates = new List<Member>();
		private List<Member> m_members = new List<Member>();
		private string m_completed = string.Empty;
		private int m_prefixLen;
		private Dictionary<string, bool> m_visibleClasses = new Dictionary<string, bool>();
		private bool m_hasExtensions;
		private bool m_showExtensions;
		#endregion
	}
}
