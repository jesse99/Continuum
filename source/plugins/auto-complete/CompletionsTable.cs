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
		
		public void Open(NSTextView text, Member[] members, Variable[] vars)
		{
			m_text = text;
			m_members = new List<Member>(members);
			m_variables = new List<Variable>(vars);
			m_completed = string.Empty;
			m_currentArg = -1;
			m_argTypes = new string[0];
			m_argNames = new string[0];
			
			m_members.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
			m_variables.Sort((lhs, rhs) => lhs.Name.CompareTo(rhs.Name));
			reloadData();
			DoResetSelection(null);
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
				string text = DoGetInsertText();
				m_text.insertText(NSString.Create(text));
				
				if (m_currentArg == -1)
				{
					m_argTypes = m_members[row].ArgTypes;
					m_argNames = m_members[row].ArgNames;
					m_completed = string.Empty;
				}
				
				if (m_currentArg + 1 < m_argNames.Length)
				{
					++m_currentArg;
					
					if (m_currentArg == 0)
						reloadData();
					
					DoUpdateLabel();
					DoResetSelection(m_argNames[m_currentArg]);
				}
				else
				{
					m_text = null;
					window().windowController().Call("hide");
				}
			}
			else
				Functions.NSBeep();
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_currentArg < 0 ? m_members.Count : m_variables.Count;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			NSObject result;
			
			string name = m_currentArg < 0 ? m_members[row].Text : m_variables[row].Name;
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
		private void DoUpdateLabel()
		{
			var builder = new StringBuilder();
			NSRange range = NSRange.Empty;
			
			builder.Append('(');
			for (int i = 0; i < m_argNames.Length; ++i)
			{
				builder.Append(m_argTypes[i]);
				builder.Append(' ');
				if (i == m_currentArg)
					range = new NSRange(builder.Length, m_argNames[i].Length);
				builder.Append(m_argNames[i]);
				
				if (i + 1 < m_argNames.Length)
					builder.Append(", ");
			}
			builder.Append(')');
			
			var style = NSMutableParagraphStyle.Create();
			style.setParagraphStyle(NSParagraphStyle.defaultParagraphStyle());
			style.setAlignment(Enums.NSCenterTextAlignment);
			
			var str = NSMutableAttributedString.Create(builder.ToString(), Externs.NSParagraphStyleAttributeName, style);
			var dict = NSDictionary.dictionaryWithObject_forKey(NSNumber.Create(-3.0f), Externs.NSStrokeWidthAttributeName);
			str.addAttributes_range(dict, range);
			
			window().windowController().Call("updateLabel:", str);
		}
		
		private void DoResetSelection(string name)
		{
			int row = -1;
			
			if (name != null)
			{
				for (int i = 0; i < (m_currentArg < 0 ? m_members.Count : m_variables.Count) && row < 0; ++i)
				{
					if (name == (m_currentArg < 0 ? m_members[i].Text : m_variables[i].Name))
						row = i;
				}
			}
			
			if ((m_currentArg < 0 ? m_members.Count : m_variables.Count) > 0)
			{
				row = Math.Max(row, 0);
				var indexes = NSIndexSet.indexSetWithIndex((uint) row);
				selectRowIndexes_byExtendingSelection(indexes, false);
				scrollRowToVisible(row);
			}
			else
			{
				deselectAll(this);
			}
		}
		
		private string DoGetInsertText()
		{
			int row = selectedRow();
			string text = m_currentArg < 0 ? m_members[row].Text : m_variables[row].Name;
			
			if (m_currentArg < 0)
			{
				int i = text.IndexOf('(');
				if (i > 0)
					if (i + 1 < text.Length && text[i + 1] == ')')
						text = text.Substring(0, i + 2);
					else
						text = text.Substring(0, i + 1);
			}
			else if (m_currentArg + 1 < m_argNames.Length)
			{
				text += ", ";
			}
			else if (m_currentArg + 1 == m_argNames.Length)
			{
				text += ")";
			}
			
			return text;
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
		private List<Variable> m_variables = new List<Variable>();
		private string m_completed = string.Empty;
		
		private int m_currentArg;
		private string[] m_argTypes = new string[0];
		private string[] m_argNames = new string[0];
		#endregion
	}
}
