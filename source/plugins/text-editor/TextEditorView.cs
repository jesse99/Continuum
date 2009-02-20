// Copyright (C) 2008 Jesse Jones
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
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace TextEditor
{
	[ExportClass("TextEditorView", "NSTextView")]
	internal sealed class TextEditorView : NSTextView
	{
		public TextEditorView(IntPtr instance) : base(instance)
		{		
			ActiveObjects.Add(this);
		}
				
		public new void keyDown(NSEvent evt)
		{
			Unused.Value = SuperCall("keyDown:", evt);

			// If the user is moving up or down within the whitespace at the start
			// of a line then set the insertion point to the start of the line (this
			// makes it much nicer to do stuff like manually comment out lines).
			NSRange range = selectedRange();
			if (range.length == 0)
			{
				NSString chars = evt.characters();
				if (chars.length() == 1 && (chars[0] == Enums.NSDownArrowFunctionKey || chars[0] == Enums.NSUpArrowFunctionKey))
				{
					NSString text = string_();
					int start = DoGetLineStart(text, range.location);
					if (start >= 0)
					{
						setSelectedRange(new NSRange(start, 0));
					}
				}
			}
		}

#if false
		// This is kind of nice, and BBEdit does something similar but it screws
		// up things like drag selections.
		public new void mouseDown(NSEvent evt)
		{
			bool done = false;
			
			if (evt.modifierFlags() == 256)
			{
				int index = DoMouseEventToIndex(evt);
				NSString text = string_();
				
				// If the user clicked in the whitespace at the start of a line then set
				// the insertion point to the start of the line. TODO: may want a pref
				// for this.
				int start = DoGetLineStart(text, index);
				if (start >= 0)
				{
					setSelectedRange(new NSRange(start, 0));
					done = true;
				}
			}
			
			if (!done)
				SuperCall("mouseDown:", evt);
		}
#endif
		
		public void processHandler(NSObject sender)
		{
			int i = sender.Call("tag").To<int>();
			string result = m_entries[i].Handler(m_selection);
			if (result != m_selection)
			{
				NSArray args = NSArray.Create(NSValue.valueWithRange(m_range), NSString.Create(result), NSString.Create(m_entries[i].UndoText));
				replaceSelection(args);
			}
		}
		
		// args[0] = text range, args[1] = text which will replace the range, args[2] = undo text
		public void replaceSelection(NSArray args)
		{
			NSRange oldRange = args.objectAtIndex(0).To<NSValue>().rangeValue();
			
			string oldStr;
			string_().getCharacters_range(oldRange, out oldStr);	// TODO: if we can figure out that the selection is a class or method then we should add that to our url
			NSString oldText = NSString.Create(oldStr);
			
			NSString newText = args.objectAtIndex(1).To<NSString>();
			replaceCharactersInRange_withString(oldRange, newText);
			
			NSRange newRange = new NSRange(oldRange.location, (int) newText.length());
			
			NSArray oldArgs = NSArray.Create(NSValue.valueWithRange(newRange), oldText, args.objectAtIndex(2));
			window().windowController().document().undoManager().registerUndoWithTarget_selector_object(this, "replaceSelection:", oldArgs);
			window().windowController().document().undoManager().setActionName(args.objectAtIndex(2).To<NSString>());
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
			NSMenu menu = NSMenu.Alloc().initWithTitle(NSString.Empty);
			menu.autorelease();
			
			try
			{
				// Get the selection.
				int index = DoMouseEventToIndex(evt);
							
				m_range = selectedRange();
				if (m_range.length == 0 && index < string_().length() && string_()[index] == '\n')
				{
					m_selection = null;		// don't extend the selection if the user clicked off to the right side of a line
				}
				else
				{
					if (m_range.length == 0 || !m_range.Intersects(index))
						m_range = DoExtendSelection(index);
					
					m_selection = null;
					if (m_range.length > 0)
						string_().getCharacters_range(m_range, out m_selection);
				}
							
				// Get the commands.
//		var watch = new Stopwatch();
//		watch.Start();
				DoGetEntries(m_selection);
		
				if (m_entries.Count == 0)
					DoGetEntries(null);
//		Console.WriteLine("secs: {0:0.000}", watch.ElapsedMilliseconds/1000.0);
				
				m_entries.Sort();
				
				// Build the menu.
				for (int i = 0; i < m_entries.Count; ++i)
				{
					NSMenuItem item = null;
			
					if (m_entries[i].Name != null)
					{
						if (m_entries[i].Name != Constants.Ellipsis)
						{
							item = NSMenuItem.Create(m_entries[i].Name, "processHandler:");
							
							if (m_entries[i].Title != null)
								item.setAttributedTitle(m_entries[i].Title);
						}
						else
							item = NSMenuItem.Create(Constants.Ellipsis);
						item.setTag(i);
					}
					else if (i > 0)
					{
						Trace.Assert(m_entries[i].Handler == null, "names is null, but handlers is not");
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
				NSString title = NSString.Create("Error building the menu.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
						
			return menu;
		}
		
		#region Private Types
		private struct Entry : IComparable<Entry>, IEquatable<Entry>
		{
			public Entry(TextContextItem item, int group)
			{
				m_item = item;
				m_group = group;
			}
			
			public string Name 
			{
				get {return m_item.Name;}
			}
			
			public string UndoText 
			{
				get {return m_item.UndoText ?? m_item.Name;}
			}
			
			public NSAttributedString Title 
			{
				get {return m_item.Title;}
			}
			
			public Func<string, string> Handler
			{
				get {return m_item.Handler;}
			}
			
			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				
				if (GetType() != obj.GetType()) 
					return false;
				
				Entry rhs = (Entry) obj;                    
				return CompareTo(rhs) == 0;
			}
			
			public bool Equals(Entry rhs)
			{
				return CompareTo(rhs) == 0;
			}
			
			public int CompareTo(Entry rhs)
			{
				int result = m_item.SortOrder.CompareTo(rhs.m_item.SortOrder);
			
				if (result == 0)
					result = m_group.CompareTo(rhs.m_group);
				
				if (result == 0)
				{
					if (Name == null)
						result = rhs.Name == null ? 0 : -1;
					
					else if (rhs.Name == null)
						result = +1;
						
					else if (Name == Constants.Ellipsis)
						result = rhs.Name == Constants.Ellipsis ? 0 : +1;
						
					else if (rhs.Name == Constants.Ellipsis)
						result = -1;
						
					else
						result = Name.CompareTo(rhs.Name);
				}
				
				return result;
			}
			
			public override int GetHashCode()
			{
				int hash = 0;
				
				unchecked
				{
					hash += m_item.SortOrder.GetHashCode();
					hash += Name.GetHashCode();
					hash += m_group.GetHashCode();
				}
				
				return hash;
			}

			public override string ToString()
			{
				return (Name ?? "separator") + " " + m_group;
			}
			
			private readonly TextContextItem m_item;
			private readonly int m_group;
		}
		#endregion
		
		#region Private Methods
		private void DoGetEntries(string selection)
		{
			int group = 0;

			m_entries.Clear();

			Boss dirBoss = ((TextController) window().windowController()).GetDirEditorBoss();

			Boss boss = ObjectModel.Create("TextEditorPlugin");		
			boss.CallRepeated<ITextContextCommands>(i => 
			{
				var items = new List<TextContextItem>();
				i.Get(dirBoss, selection, items);
				
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
			});
		}
		
		private int DoGetLineStart(NSString text, int index)
		{
			bool within = false;
			
			if (index < text.length() && (text[index] == ' ' || text[index] == '\t'))
			{
				while (index > 0 && (text[index] == ' ' || text[index] == '\t'))
					--index;

				if (index >= 0 && (text[index] == '\n' || text[index] == '\r'))
					within = true;
			}
			
			return within ? index + 1 : -1;
		}
		
		private int DoMouseEventToIndex(NSEvent evt)
		{
			NSPoint baseLoc = evt.locationInWindow();
			NSPoint viewLoc = convertPointFromBase(baseLoc);
			return (int) characterIndexForInsertionAtPoint(viewLoc);
		}
		
		private NSRange DoExtendSelection(int location)
		{
			NSString text = string_();
			int length = 0;
			
			while (location > 0 && DoIsWordChar(text, location - 1))
			{
				--location;
				++length;
			}
			
			while (location + length < text.length() && DoIsWordChar(text, location + length))
			{
				++length;
			}
			
			NSRange range = new NSRange(location, length);
			if (length > 0)
				setSelectedRange(range);
			
			return range;
		}
		
		private bool DoIsWordChar(NSString text, int index)		// TODO: probably should pull this info from a languages.xml file
		{
			char ch = text.characterAtIndex((uint) index);
			
			if (NSCharacterSet.alphanumericCharacterSet().characterIsMember(ch))
				return true;
				
			if (ch == '_' || ch == ':')
				return true;
				
			return false;
		}
		#endregion
		
		#region Fields
		private NSRange m_range;
		private string m_selection;
		private List<Entry> m_entries = new List<Entry>();
		#endregion
	}
}	