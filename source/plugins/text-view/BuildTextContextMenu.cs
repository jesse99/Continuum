// Copyright (C) 2011 Jesse Jones
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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TextView
{
	internal sealed class BuildTextContextMenu : IBuildTextContextMenu
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void ExtendSelection(NSTextView view, NSEvent evt)
		{
			int index = DoMouseEventToIndex(view, evt);
			
			NSRange range = view.selectedRange();
			if (range.length == 0 && index < view.string_().length() && view.string_()[index] == '\n')
			{
				// don't extend the selection if the user clicked off to the right side of a line
			}
			else if (index >= view.string_().length())
			{
				// don't extend the selection if the user clicked below the last line of text
				view.setSelectedRange(NSRange.Empty);
			}
			else
			{
				// Extend the selection so that it contains the entire word the user right-clicked on.
				if (range.length == 0 || !range.Intersects(index))
				{
					range = new NSRange(index, 1);
					range = view.selectionRangeForProposedRange_granularity(range, Enums.NSSelectByWord);
					view.setSelectedRange(range);
				}
			}
		}
		
		public void Populate(NSMenu menu, NSTextView view, Boss window)
		{
			// ITextContextCommands expect that the main window is the one the user
			// is working with.
			view.window().makeKeyAndOrderFront(view);
			
			// We don't extend the default menu because it has tons of stuff that we
			// don't really want. But we should add the services...
//			NSMenu menu = SuperCall("menuForEvent:", evt).To<NSMenu>();
//			menu.addItem(NSMenuItem.separatorItem());
			
			try
			{
				// Get the selection.
				m_view = view;
				m_range = view.selectedRange();
				m_selection = null;
				if (m_range.length > 0)
					view.string_().getCharacters_range(m_range, out m_selection);
				
				// Get the language.
				string language = null;
				if (window.Has<ITextEditor>())
				{
					var editor = window.Get<ITextEditor>();
					language = editor.Language;
				}
				
				// Get the commands.
				var watch = new Stopwatch();
				watch.Start();
				m_entries.Clear();
				if (window != null)
					DoGetEntries(view, m_selection, language, window);
				DoGetEntries(view, m_selection, language, m_boss);
				
				if (m_entries.Count == 0)
				{
					if (window != null)
						DoGetEntries(view, null, language, window);
					DoGetEntries(view, null, language, m_boss);
				}
				Log.WriteLine("ContextMenu", "took {0:0.000} secs to open the menu", watch.ElapsedMilliseconds/1000.0);
				
				m_entries.Sort(this.DoCompareEntry);
				
				// Remove duplicate separators and any at the start or end.
				for (int i = m_entries.Count - 1; i > 0; --i)
				{
					if (m_entries[i].Command.Name == null && m_entries[i - 1].Command.Name == null)
						m_entries.RemoveAt(i);
				}
				while (m_entries.Count > 0 && m_entries[0].Command.Name == null)
					m_entries.RemoveAt(0);
				while (m_entries.Count > 0 && m_entries[m_entries.Count - 1].Command.Name == null)
					m_entries.RemoveAt(m_entries.Count - 1);
				
				// Build the menu.
				menu.removeAllItems();
				for (int i = 0; i < m_entries.Count; ++i)
				{
					NSMenuItem item = null;
					
					if (m_entries[i].Command.Name != null)
					{
						if (m_entries[i].Command.Name != Constants.Ellipsis)
						{
							item = NSMenuItem.Create(m_entries[i].Command.Name, "dispatchTextContextMenu:");
							
							if (m_entries[i].Command.Title != null)
								item.setAttributedTitle(m_entries[i].Command.Title);
						}
						else
							item = NSMenuItem.Create(Constants.Ellipsis);
						item.setTag(i);
					}
					else
					{
						Contract.Assert(m_entries[i].Command.Handler == null, "names is null, but handlers is not");
						item = NSMenuItem.separatorItem();
					}
					
					if (item != null)
						menu.addItem(item);
				}
			}
			catch (DatabaseLockedException)
			{
				NSString title = NSString.Create("Database was locked.");
				NSString message = NSString.Create("Try again.");
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Error building context menu:");
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
				
				NSString title = NSString.Create("Error building the menu.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		public void Dispatch(NSObject sender)
		{
			int i = sender.Call("tag").To<int>();
			TextContextItem command = m_entries[i].Command;
			
			string result = command.Handler(m_selection);
			if (result != m_selection)
			{
				var range = NSValue.valueWithRange(m_range);
				var newText = NSString.Create(result);
				var undoText = NSString.Create(command.UndoText ?? command.Name);
				
				NSArray args = NSArray.Create(range, newText, undoText);
				m_view.Call("replaceSelection:", args);
			}
			
			m_entries.Clear();			// this has references to objects which we may want to GC
			m_view = null;
		}
		
		public bool Validate(NSObject sender)
		{
			int i = sender.Call("tag").To<int>();
			sender.Call("setState:", m_entries[i].Command.State);
			return m_entries[i].Command.Handler != null;
		}
		
		#region Private Methods
		private int DoMouseEventToIndex(NSTextView view, NSEvent evt)
		{
			NSPoint baseLoc = evt.locationInWindow();
			NSPoint viewLoc = view.convertPointFromBase(baseLoc);
			return (int) view.characterIndexForInsertionAtPoint(viewLoc);
		}
		
		private int DoCompareEntry(Entry lhs, Entry rhs)
		{
			int result = lhs.Command.SortOrder.CompareTo(rhs.Command.SortOrder);
			
			if (result == 0)
				result = lhs.Group.CompareTo(rhs.Group);
			
			if (result == 0)
			{
				if (lhs.Command.Name == null)
					result = rhs.Command.Name == null ? 0 : -1;
				
				else if (rhs.Command.Name == null)
					result = +1;
					
				else if (lhs.Command.Name == Constants.Ellipsis)
					result = rhs.Command.Name == Constants.Ellipsis ? 0 : +1;
					
				else if (rhs.Command.Name == Constants.Ellipsis)
					result = -1;
					
				else
					result = lhs.Command.Name.CompareTo(rhs.Command.Name);
			}
			
			return result;
		}
		
		private void DoGetEntries(NSTextView view, string selection, string language, Boss boss)
		{
			int group = 0;
			
			bool editable = view.isEditable();
			foreach (ITextContextCommands i in boss.GetRepeated<ITextContextCommands>())
			{
				var items = new List<TextContextItem>();
				i.Get(selection, language, editable, items);
				
				if (items.Count > 0)
				{
					if (!items.All(item => item.Name == null))
					{
						for (int j = 0; j < items.Count; ++j)
						{
							m_entries.Add(new Entry(items[j], group));
						}
						
						++group;
					}
				}
			}
		}
		#endregion
		
		#region Private Types
		private struct Entry
		{
			public Entry(TextContextItem command, int group) : this()
			{
				Command = command;
				Group = group;
			}
			
			public TextContextItem Command {get; private set;}
			
			public int Group {get; private set;}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private List<Entry> m_entries = new List<Entry>();
		private NSTextView m_view;
		private NSRange m_range;
		private string m_selection;
		#endregion
	}
}
