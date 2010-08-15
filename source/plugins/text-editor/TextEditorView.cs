// Copyright (C) 2008-2010 Jesse Jones
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
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TextEditor
{
	[ExportClass("TextEditorView", "NSTextView")]
	internal sealed class TextEditorView : NSTextView
	{
		public TextEditorView(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public void onOpened(TextController controller)
		{
			m_boss = controller.Boss;
			m_autoComplete = m_boss.Get<IAutoComplete>();
			setTypingAttributes(CurrentStyles.DefaultAttributes);
			
			if (m_boss.Has<ITooltip>())
			{
				m_tooltip = m_boss.Get<ITooltip>();
				m_timer = new System.Threading.Timer((object state) =>
				{
					NSApplication.sharedApplication().BeginInvoke(this.DoShowTooltip);
				});
			}
		}
		
		public void onClosing(TextController controller)
		{
			m_autoComplete = null;		// note that these won't be GCed if we don't null them out
			m_boss = null;
		}
		
		public new void mouseMoved(NSEvent e)
		{
			if (m_tooltip != null)
			{
				m_moveIndex = DoMouseEventToIndex(e);
				m_timer.Change(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(-1));
			}
			Unused.Value = SuperCall(NSTextView.Class, "mouseMoved:", e);
		}
		
		public new void mouseExited(NSEvent e)
		{
			if (m_tooltipWindow != null)
			{
				m_tooltipWindow.Close();
				m_tooltipWindow = null;
			}
			
			if (m_timer != null)
				m_timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
			
			Unused.Value = SuperCall(NSTextView.Class, "mouseExited:", e);
		}
		
		public new void drawRect(NSRect dirtyRect)
		{
			SuperCall(NSTextView.Class, "drawRect:", dirtyRect);
			
			if (m_boss != null)
			{
				var overlay = m_boss.Get<ITextOverlay>();
				if (overlay.Text != null)
				{
					NSRect rect = visibleRect();
					DoCacheOverlay(overlay, rect);
					
					if (m_overlay != null)
					{
						NSPoint center = rect.Center;
						NSRect temp = new NSRect(center.x - m_overlaySize.width/2, center.y - m_overlaySize.height/2, m_overlaySize.width, m_overlaySize.height);
						m_overlay.drawInRect(temp);
					}
				}
			}
		}
		
		public new void keyDown(NSEvent evt)
		{
			do
			{
				TextController controller = (TextController) window().windowController();
				if (controller.Language != null && controller.Language.Name == "CsLanguage")
				{
					// Handle auto-complete initiated with '.' or enter.
					if (m_autoComplete.HandleKey(this, evt))
						break;
				}
				
				// Option-tab selects the next identifier.
				if (evt.keyCode() == Constants.TabKey) {
					if ((evt.modifierFlags() & Enums.NSAlternateKeyMask) != 0)
					{
						if ((evt.modifierFlags() & Enums.NSShiftKeyMask) == 0)
						{
							if (DoSelectNextIdentifier(controller))
								break;
						}
						else
						{
							if (DoSelectPreviousIdentifier(controller))
								break;
						}
					}
					else if ((evt.modifierFlags() & Enums.NSCommandKeyMask) != 0)
					{
					}
					else if ((evt.modifierFlags() & Enums.NSControlKeyMask) != 0)
					{
					}
					else if ((evt.modifierFlags() & Enums.NSShiftKeyMask) != 0)
					{
						// unindent the code / shift left
						controller.shiftLeft(this);
						break;
					}
					else
					{
						var selRange = selectedRange();
						if (selRange.location > 0 && selRange.length > 1)
						{
							// indent the code / shift right
							controller.shiftRight(this);
							break;
						}
						else if (selRange.location > 0 && selRange.length == 0)
						{
							if (DoMatchPriorLineTabs(selRange.location))
								break;
						}
					}
				}
				
				// Special case option-shift-arrow because Apple is too lame to call selectionRangeForProposedRange_granularity
				// for us.
				int shiftOption = Enums.NSShiftKeyMask | Enums.NSAlternateKeyMask;
				if (evt.keyCode() == Constants.LeftArrowKey && (evt.modifierFlags() & shiftOption) == shiftOption)
				{
					if (DoExtendSelectionLeft())
						break;
				}
				else if (evt.keyCode() == Constants.RightArrowKey && (evt.modifierFlags() & shiftOption) == shiftOption)
				{
					if (DoExtendSelectionRight())
						break;
				}
				
				// Special case for deleting the new line at the start of a blank line
				// (users don't normally want the whitespace to be appended to the
				// previous line).
				NSRange range = selectedRange();
				if (range.location > 0 && range.length == 0)
				{
					if (evt.keyCode() == Constants.DeleteKey && string_().characterAtIndex((uint) range.location - 1) == '\n')
					{
						int count = DoGetBlankCount(string_(), range.location);
						if (count > 0)
						{
							setSelectedRange(new NSRange(range.location - 1, count + 1));
							delete(this);
							break;
						}
					}
				}
				
				// Default key processing.
				Unused.Value = SuperCall(NSTextView.Class, "keyDown:", evt);
				
				// For up and down arrow in the whitespace at the start of a line
				// we want to set the insertion point to the start of the line (this
				// makes it much nicer to do stuff like manually comment out lines).
				range = selectedRange();
				if (range.length == 0)
				{
					if (evt.keyCode() == Constants.UpArrowKey || evt.keyCode() == Constants.DownArrowKey)
					{
						NSString text = string_();
						int start = DoGetLineStartFromSpaceOrTab(text, range.location);
						if (start >= 0)
						{
							setSelectedRange(new NSRange(start, 0));
						}
					}
				}
			}
			while (false);
		}

#if false
		public new void mouseDown(NSEvent evt)
		{
			bool done = false;
			
			// This is kind of nice, and BBEdit does something similar but it screws
			// up things like drag selections.
			if (evt.modifierFlags_i() == 256)
			{
				int index = DoMouseEventToIndex(evt);
				NSString text = string_();
				
				// If the user clicked in the whitespace at the start of a line then set
				// the insertion point to the start of the line. TODO: may want a pref
				// for this.
				int start = DoGetLineStartFromSpaceOrTab(text, index);
				if (start >= 0)
				{
					setSelectedRange(new NSRange(start, 0));
					done = true;
				}
			}
			
			if (!done)
				SuperCall(NSTextView.Class, "mouseDown:", evt);
		}
#endif
	
		public NSRange selectionRangeForProposedRange_granularity(NSRange proposedSelRange, int granularity)
		{
			NSRange result;
			
			TextController controller = (TextController) window().windowController();
			if (granularity == Enums.NSSelectByWord && controller.Language != null)
			{
				NSString text = string_();
				result = proposedSelRange;
				
				while (DoMatchesWord(text, result.location - 1, result.length + 1, controller.Language.Word))
				{
					--result.location;
					++result.length;
				}
				
				while (DoMatchesWord(text, result.location, result.length + 1, controller.Language.Word))
				{
					++result.length;
				}
			}
			else
			{
				result = SuperCall(NSTextView.Class, "selectionRangeForProposedRange:granularity:", proposedSelRange, granularity).To<NSRange>();
			}
			
			return result;
		}
		
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
		
		public new bool validateUserInterfaceItem(NSObject item)
		{
			Selector sel = (Selector) item.Call("action");
			
			bool valid = false;
			if (sel.Name == "processHandler:")
			{
				int i = item.Call("tag").To<int>();
				item.Call("setState:", m_entries[i].State);
				valid = m_entries[i].Handler != null;
			}
			else if (SuperCall(NSTextView.Class, "respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				valid = SuperCall(NSTextView.Class, "validateUserInterfaceItem:", item).To<bool>();
			}
			
			return valid;
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
			var undoManager = window().windowController().document().undoManager();
			undoManager.registerUndoWithTarget_selector_object(this, "replaceSelection:", oldArgs);
			undoManager.setActionName(args.objectAtIndex(2).To<NSString>());
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
			NSMenu menu = NSMenu.Alloc().initWithTitle(NSString.Empty);
			menu.autorelease();
			
			// ITextContextCommands expect that the main window is the one the user
			// is working with.
			window().makeKeyAndOrderFront(this);
			
			// We don't extend the default menu because it has tons of stuff that we
			// don't really want. But we should add the services...
//			NSMenu menu = SuperCall("menuForEvent:", evt).To<NSMenu>();
//			menu.addItem(NSMenuItem.separatorItem());
			
			try
			{
				// Get the selection.
				int index = DoMouseEventToIndex(evt);
				
				m_range = selectedRange();
				if (m_range.length == 0 && index < string_().length() && string_()[index] == '\n')
				{
					m_selection = null;		// don't extend the selection if the user clicked off to the right side of a line
				}
				else if (index >= string_().length())
				{
					m_selection = null;		// don't extend the selection if the user clicked below the last line of text
				}
				else
				{
					// Extend the selection so that it contains the entire word the user right-clicked on.
					if (m_range.length == 0 || !m_range.Intersects(index))
					{
						m_range = new NSRange(index, 1);
						m_range = selectionRangeForProposedRange_granularity(m_range, Enums.NSSelectByWord);
						setSelectedRange(m_range);
					}
					
					m_selection = null;
					if (m_range.length > 0 && m_range.location + m_range.length <= string_().length())
						string_().getCharacters_range(m_range, out m_selection);
				}
				
				// Get the commands.
				var watch = new Stopwatch();
				watch.Start();
				DoGetEntries(m_selection);
				
				if (m_entries.Count == 0)
					DoGetEntries(null);
				Log.WriteLine("ContextMenu", "took {0:0.000} secs to open the menu", watch.ElapsedMilliseconds/1000.0);
				
				m_entries.Sort();
				
				// Remove duplicate separators and any at the start or end.
				for (int i = m_entries.Count - 1; i > 0; --i)
				{
					if (m_entries[i].Name == null && m_entries[i - 1].Name == null)
						m_entries.RemoveAt(i);
				}
				while (m_entries.Count > 0 && m_entries[0].Name == null)
					m_entries.RemoveAt(0);
				while (m_entries.Count > 0 && m_entries[m_entries.Count - 1].Name == null)
					m_entries.RemoveAt(m_entries.Count - 1);
				
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
					else
					{
						Contract.Assert(m_entries[i].Handler == null, "names is null, but handlers is not");
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
			
			public int State
			{
				get {return m_item.State;}
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
		// DoMatchPriorLineTabs:
		//    If at eol and all preceding chars on current line are tabs (or empty),
		//    then match the leading tabs of the above line and return true.
		// Terms: eol = end of line, bof = beginning of file, etc.
		// Test cases:
		//    * At bol, prev line has >= 1 leading tabs. Press Tab.
		//    * At bol, prev line has 0 leading tabs. Press Tab.
		//    * At eol with one leading tab, prev line has >= 1 leading tabs. Press Tab.
		//    * At eol with one leading tab, prev line has 0 leading tabs. Press Tab.
		//    * Empty file. Press Tab.
		//	  * At bof. Press Tab.
		// To do:
		//    * Doesn't handle when there are nothing but tabs after the current cursor location.
		private bool DoMatchPriorLineTabs(int location) {
			var text = string_();
			if (location >= text.length()) return false;  // out of range
			var thisLineStart = DoGetLineStart(text, location);
			if (thisLineStart == 0) return false;  // there is no previous line to match tab chars with
			if (location != text.length()      // not at end of text
				&& location != text.length()-1 // not at end of text
				&& text[location] != '\n')     // not at end of line
				{ return false; }
			// at this point, there is a previous line and the cursor is at the end of the current line
			// now exit if non-tab chars between the bol and location
			int i;
			for (i = thisLineStart; i < location; i++)
				if (text[i] != '\t') return false;
			var prevLineStart = DoGetLineStart(text, thisLineStart - 2);
			int prevTabCount = DoGetLeadingTabCount(text, prevLineStart);
			if (prevTabCount <= (location - thisLineStart)) return false;
			// add new tabs with undo
			var newTabs = new String('\t', prevTabCount - (location - thisLineStart));
			NSArray args = NSArray.Create(NSValue.valueWithRange(new NSRange(location, 0)), NSString.Create(newTabs), NSString.Create());
			replaceSelection(args);
			setSelectedRange(new NSRange(location + newTabs.Length, 0));
			return true;
		}
		
		private int DoGetLeadingTabCount(NSString text, int i) {
			Contract.Requires(text != null && !text.IsNil());
			Contract.Requires(i >= 0);
			Contract.Requires(i == 0 || text[i-1] == '\n');
			var textLen = text.length();
			var j = i;
			while (j < textLen && text[j] == '\t') j++;
			return j - i;
		}
		
		// Note that NSView has support for tooltips, but it's not designed for the sort of very
		// dynamic tooltips that we need to support.
		private void DoShowTooltip()
		{
			if (m_tooltipWindow != null)
			{
				m_tooltipWindow.Close();
				m_tooltipWindow = null;
			}
			
			string text = m_tooltip.GetTooltip(m_moveIndex);
			if (!string.IsNullOrEmpty(text) && m_boss != null)	// boss will be null if the window closed
			{
				var editor = m_boss.Get<ITextEditor>();
				var range = new NSRange(m_moveIndex, 1);
				m_tooltipWindow = editor.GetAnnotation(range, AnnotationAlignment.Top);
				
				m_tooltipWindow.BackColor = NSColor.colorWithDeviceRed_green_blue_alpha(1.0f, 0.96f, 0.0f, 1.0f);
				m_tooltipWindow.Text= text;
				m_tooltipWindow.Draggable = false;
				m_tooltipWindow.Visible = true;
			}
		}
		
		private bool DoMatchesWord(NSString text, int location, int length, Regex word)
		{
			bool matches = false;
			
			if (location >= 0 && location + length <= text.length())
			{
				string str;
				text.getCharacters_range(new NSRange(location, length), out str);
				Match match = word.Match(str);
				matches = match.Success && match.Length == str.Length;
			}
			
			return matches;
		}
		
		private bool DoExtendSelectionLeft()
		{
			bool extended = false;
			
			TextController controller = (TextController) window().windowController();
			if (controller.Language != null)
			{
				NSString text = string_();
				NSRange range = selectedRange();
				
				int i = range.location;
				while (i > 0 && char.IsWhiteSpace(text.characterAtIndex((uint) (i - 1))))
				{
					--i;
				}
				
				if (i > 0)
				{
					--i;
					
					NSRange temp = new NSRange(i, 1);
					temp = selectionRangeForProposedRange_granularity(temp, Enums.NSSelectByWord);
					
					range = new NSRange(temp.location, range.location + range.length - temp.location);
					setSelectedRange(range);
					scrollRangeToVisible(new NSRange(range.location, 1));
					
					extended = true;
				}
			}
			
			return extended;
		}
		
		private bool DoExtendSelectionRight()
		{
			bool extended = false;
			
			TextController controller = (TextController) window().windowController();
			if (controller.Language != null)
			{
				NSString text = string_();
				NSRange range = selectedRange();
				
				int j = range.location + range.length;
				while (j + 1 < text.length() &&
					char.IsWhiteSpace(text.characterAtIndex((uint) (j + 1))))
				{
					++j;
				}
				
				if (j + 1 < text.length())
				{
					++j;
					
					NSRange temp = new NSRange(j, 1);
					temp = selectionRangeForProposedRange_granularity(temp, Enums.NSSelectByWord);
					
					range = new NSRange(range.location, temp.location + temp.length - range.location);
					setSelectedRange(range);
					scrollRangeToVisible(new NSRange(range.location + range.length - 1, 1));
					
					extended = true;
				}
			}
			
			return extended;
		}
		
		// If the line is blank then this will return the number of blank characters.
		private int DoGetBlankCount(NSString text, int index)
		{
			for (int i = index; i < text.length(); ++i)
			{
				char ch = text.characterAtIndex((uint) i);
				
				if (ch == '\n')
					return i - index;
				
				else if (ch != ' ' && ch != '\t')
					break;
			}
			
			return 0;
		}
		
		// Another possibility here is that after completing a method we could set the
		// find text to a regex which can be used to select the next identifier. This
		// would allow command-G to be used instead of option-tab which might be
		// a bit more natural. Although it would also zap the user's find text and screw
		// up the find history popup.
		private bool DoSelectNextIdentifier(TextController controller)
		{
			NSRange range = selectedRange();
	
			NSRange next;
			if (controller.Language != null && controller.Language.Name == "CsLanguage")
			{
				// TODO: ISearchTokens should probably be moved into the language boss.
				// It might also be worthwhile to split it into multiple interfaces.
				var tokens = m_boss.Get<ISearchTokens>();
				next = tokens.GetNextIdentifier(range.location + range.length);
			}
			else
			{
				// If we're in the middle of an identifier then skip past it.
				next.location = range.location + range.length;
				next.location += DoSkip(next.location, +1, (c) => char.IsLetterOrDigit(c) || c == '_');
				
				// Skip to the start of the next identifier.
				next.location += DoSkip(next.location, +1, (c) => !char.IsLetter(c) && c != '_');
				
				// Get the length of the identifier.
				next.length = DoSkip(next.location, +1, (c) => char.IsLetterOrDigit(c) || c == '_');
			}
			
			if (next.length > 0)
			{
				next = selectionRangeForProposedRange_granularity(next, Enums.NSSelectByWord);
				setSelectedRange(next);
				scrollRangeToVisible(next);
			}
			
			return true;	// if we don't return true then NSTextView adds some weird indent
		}
		
		private bool DoSelectPreviousIdentifier(TextController controller)
		{
			NSRange range = selectedRange();
			
			NSRange previous;
			if (controller.Language != null && controller.Language.Name == "CsLanguage")
			{
				var tokens = m_boss.Get<ISearchTokens>();
				previous = tokens.GetPreviousIdentifier(range.location);
			}
			else
			{
				// If we're in the middle of an identifier then skip past it.
				previous.location = range.location;
				previous.location -= DoSkip(previous.location, -1, (c) => char.IsLetterOrDigit(c) || c == '_');
				
				// Skip to the end of the previous identifier.
				previous.location -= DoSkip(previous.location, -1, (c) => !char.IsLetterOrDigit(c) && c != '_');
				
				// Get the length of the identifier.
				previous.length = DoSkip(previous.location, -1, (c) => char.IsLetterOrDigit(c) || c == '_');	// TODO: this will find numbers
				previous.location -= previous.length - 1;
			}
			
			if (previous.length > 0)
			{
				previous = selectionRangeForProposedRange_granularity(previous, Enums.NSSelectByWord);
				setSelectedRange(previous);
				scrollRangeToVisible(previous);
			}
			
			return true;
		}
		
		private int DoSkip(int start, int delta, Predicate<char> predicate)	
		{
			NSString text = string_();
			
			int i = start;
			while (i >= 0 && i < text.length() && predicate(text.characterAtIndex((uint) i)))
			{
				i += delta;
			}
			
			return Math.Abs(i - start);
		}
		
		private void DoGetEntries(string selection)
		{
			int group = 0;
			
			m_entries.Clear();
			
			Boss dirBoss = ((TextController) window().windowController()).GetDirEditorBoss();
			
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			bool editable = isEditable();
			foreach (ITextContextCommands i in boss.GetRepeated<ITextContextCommands>())
			{
				var items = new List<TextContextItem>();
				i.Get(dirBoss, selection, editable, items);
				
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
		
		// Returns a new index >= 0 and <= text.length()
		// and <= than the original index, unless a negative value was passed in.
		// Returns the same index if the previous character is a newline.
		private int DoGetLineStart(NSString text, int index)
		{
			int len = (int)text.length();
			if (len == 0) return 0;
			if (index > len) index = len - 1;
			else if (index < 0) index = 0;
			if (index > 0 && text[index-1] == '\n') return index;
			while (index > 0 && text[index-1] != '\n') --index;
			if (index < 0) index = 0;
			return index;
		}
		
		private int DoGetLineStartFromSpaceOrTab(NSString text, int index)
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
		
		private void DoCacheOverlay(ITextOverlay overlay, NSRect rect)
		{
			if (overlay.Text != m_overlayText || rect.size != m_overlayRect.size)
			{
				if (m_overlay != null)
				{
					m_overlay.release();
					m_overlay = null;
				}
				
				DoFindLargestFont(overlay, rect);
				m_overlayText = overlay.Text;
				m_overlayRect = rect;
			}
		}
		
		private void DoFindLargestFont(ITextOverlay overlay, NSRect rect)
		{
			float[] candidates = new float[]{12.0f, 14.0f, 18.0f, 24.0f, 36.0f, 48.0f, 64.0f, 72.0f, 06.0f, 144.0f, 288.0f, 0.0f};
			
			int i = 0;
			while (candidates[i + 1] != 0.0f)
			{
				var attrs = NSMutableDictionary.Create();
				NSFont font = NSFont.fontWithName_size(NSString.Create("Verdana"), candidates[i + 1]);
				attrs.setObject_forKey(font, Externs.NSFontAttributeName);
				
				attrs.setObject_forKey(overlay.Color, Externs.NSForegroundColorAttributeName);
				
				NSMutableParagraphStyle style = NSMutableParagraphStyle.Create();
				style.setAlignment(Enums.NSCenterTextAlignment);
				attrs.setObject_forKey(style, Externs.NSParagraphStyleAttributeName);
				
				var candidate = NSAttributedString.Create(overlay.Text, attrs);
				NSSize size = candidate.size();
				if (size.width <= rect.size.width && size.height <= rect.size.height)
				{
					m_overlay = candidate.Retain();
					m_overlaySize = size;
					++i;
				}
				else
				{
					break;
				}
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private NSRange m_range;
		private string m_selection;
		private List<Entry> m_entries = new List<Entry>();
		private IAutoComplete m_autoComplete;
		private ITooltip m_tooltip;
		private int m_moveIndex;
		private System.Threading.Timer m_timer;
		private ITextAnnotation m_tooltipWindow;
		
		private string m_overlayText;
		private NSRect m_overlayRect;
		private NSSize m_overlaySize;
		private NSAttributedString m_overlay;
		#endregion
	}
}
