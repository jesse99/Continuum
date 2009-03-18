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
			
			ActiveObjects.Add(this);
		}
		
		public void Open(NSTextView text, Member[] members, int prefixLen)
		{
			m_text = text;
			m_members = new List<Member>(members);
			m_completed = string.Empty;
			m_prefixLen = prefixLen;
			
			m_members.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
			reloadData();
			DoResetSelection();
		}
		
		public new void keyDown(NSEvent evt)
		{
			NSString chars = evt.characters();
			
			if (chars == "\t" || chars == "\r")
			{
				doubleClicked(null);
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
			int row = selectedRow();
			if (row >= 0)
			{
				string text;
				NSRange range;
				DoGetInsertText(row, out text, out range);
				
				if (m_prefixLen < text.Length)
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
			}
			else
				Functions.NSBeep();
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_members.Count;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			NSObject result;
			
			string name = m_members[row].Text;
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
		private void DoResetSelection()
		{
			if (m_members.Count > 0)
			{
				var indexes = NSIndexSet.indexSetWithIndex(0);
				selectRowIndexes_byExtendingSelection(indexes, false);
				scrollRowToVisible(0);
			}
			else
			{
				deselectAll(this);
			}
		}
		
		private void DoGetInsertText(int row, out string text, out NSRange range)
		{
			var builder = new StringBuilder();
			
			range = m_text.selectedRange();
			range.length = 0;
			
			int i = m_members[row].Text.IndexOf('(');
			if (i > 0)
				builder.Append(m_members[row].Text, 0, i + 1);
			else
				builder.Append(m_members[row].Text);
			
			string[] argNames = m_members[row].ArgNames;
			if (argNames.Length > 0)
			{
				range.location += builder.Length;
				range.length = argNames[0].Length;
				
				for (int j = 0; j < argNames.Length; ++j)
				{
					builder.Append(argNames[j]);
					
					if (j + 1 < argNames.Length)
						builder.Append(", ");
				}
			}
			
			if (i > 0)
				builder.Append(')');
			
			text = builder.ToString();
		}
		
		private int DoMatchName()
		{
			int index = -1;
			int count = 0;
			
			for (int i = 1; i < m_members.Count; ++i)
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
				deselectAll(this);
				
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
		private NSTextView m_text;
		private List<Member> m_members = new List<Member>();
		private string m_completed = string.Empty;
		private int m_prefixLen;
		#endregion
	}
}
