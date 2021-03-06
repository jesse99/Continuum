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
	[ExportClass("TextController", "NSWindowController", Outlets = "textView lineLabel decsPopup scrollView")]
	internal sealed class TextController : NSWindowController, IObserver
	{
		public TextController(string bossName) : base(NSObject.AllocAndInitInstance("TextController"))
		{
			m_boss = ObjectModel.Create(bossName);
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("text-editor"), this);
			
			m_textView = new IBOutlet<NSTextView>(this, "textView");
			m_lineLabel = new IBOutlet<NSButton>(this, "lineLabel");
			m_decPopup = new IBOutlet<NSPopUpButton>(this, "decsPopup");
			m_scrollView = new IBOutlet<NSScrollView>(this, "scrollView");
			m_restorer = new RestoreViewState(this);
			
			var wind = m_boss.Get<IWindow>();
			wind.Window = window();
			
			m_applier = new ApplyStyles(this, m_textView.Value);
			DoSetTextOptions();
			
			Broadcaster.Register("text default color changed", this);
			Broadcaster.Register("languages changed", this);
			Broadcaster.Register("directory prefs changed", this);
			DoUpdateDefaultColor(string.Empty, null);
			
			m_textView.Value.Call("onOpened:", this);
			
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "text default color changed":
					DoUpdateDefaultColor(name, value);
					break;
					
				case "languages changed":
					if (Path != null || m_boss.Has<IDocumentExtension>())
						Language = DoFindLanguage();
					
					var edit = new TextEdit{
						Boss = m_boss,
						Language = m_language,
						UserEdit = true,
						EditedRange = NSRange.Empty,
						ChangeInLength = 0,
						ChangeInLines = 0,
						StartLine = 1};
					DoSetTabSettings();
					Broadcaster.Invoke("text changed", edit);
					break;
					
				case "directory prefs changed":
					DoSetTabSettings();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void ClearError()
		{
			m_applier.HighlightError(0, 0);
		}
		
		public void HighlightError(int offset, int length)
		{
			NSRange range = CurrentStyles.AdjustRangeForZeroWidthChars(Text, new NSRange(offset, length));
			m_applier.HighlightError(range.location, range.length);
		}
		
		public string Path
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				return !NSObject.IsNullOrNil(document().fileURL()) ? document().fileURL().path().description() : null;
			}	
		}
		
		public void OnPathChanged()
		{
			if (Path != null || m_boss.Has<IDocumentExtension>())
				Language = DoFindLanguage();
			
			if (Path != null)
			{
				NSRect frame = WindowDatabase.GetFrame(Path);
				if (frame != NSRect.Empty)
					window().setFrame_display(frame, true);		// note that Cocoa will ensure that windows with title bars are not moved off screen
				
				if (m_watcher != null)
				{
					m_watcher.Dispose();
					m_watcher = null;
				}
				
				if ((object) m_dir != null)
				{
					m_dir.release();
					m_dir = null;
				}
				
				var complete = m_boss.Get<IAutoComplete>();
				complete.OnPathChanged();
				
				string dir = System.IO.Path.GetDirectoryName(Path);
				m_dir = NSString.Create(dir).stringByResolvingSymlinksInPath().Retain();
				m_watcher = new DirectoryWatcher(m_dir.description(), TimeSpan.FromMilliseconds(250));
				m_watcher.Changed += this.DoDirChanged;
				
				if (m_restorer != null)
					m_restorer.SetPath(Path);
			}
			else
				((DeclarationsPopup) m_decPopup.Value).Init(this);
		}
		
		public string Text
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				if (m_editCount != m_cachedEditCount)
				{
					m_cachedText = m_textView.Value.textStorage().ToString();
					m_cachedEditCount = m_editCount;
					m_metrics.Reset(m_cachedText);
				}
				
				return m_cachedText;
			}
			set
			{
				try
				{
					m_userEdit = false;
					m_editCount = unchecked(m_editCount + 1);
					
					var text = NSAttributedString.Create(value);
					m_textView.Value.textStorage().setAttributedString(text);
					m_textView.Value.setSelectedRange(new NSRange(0, 0));
					
					if (Language == null)
						Language = DoFindLanguage();
					m_applier.Reset();
					
					DoUpdateLineLabel(Text);			// use Text so metrics are up to date
				}
				finally
				{
					m_userEdit = true;
				}
			}
		}
		
		public NSAttributedString RichText
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				return m_textView.Value.textStorage();
			}
			set
			{
				try
				{
					m_userEdit = false;
					m_editCount = unchecked(m_editCount + 1);
					
					m_textView.Value.textStorage().setAttributedString(value);
					m_textView.Value.setSelectedRange(new NSRange(0, 0));
					
					if (Language == null)
						Language = DoFindLanguage();
					m_applier.Reset();
					
					DoUpdateLineLabel(Text);			// use Text so metrics are up to date
				}
				finally
				{
					m_userEdit = true;
				}
			}
		}
		
		public void Open()
		{
			Broadcaster.Invoke("opening document window", m_boss);
			
			window().makeKeyAndOrderFront(this);
			m_textView.Value.layoutManager().setDelegate(this);
			
			Broadcaster.Invoke("opened document window", m_boss);
			synchronizeWindowTitleWithDocumentName();		// bit of a hack, but we need something like this for IDocumentWindowTitle to work
			
			DoSetTabSettings();
		}
		
		public NSTextView TextView
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				return m_textView.Value;
			}
		}
		
		public ILanguage Language
		{
			get {return m_language;}
			set
			{
				if (value != m_language)
				{
					if (value != null && value.FriendlyName != "plain-text")
						m_language = value;
					else
						m_language = null;
					
					((DeclarationsPopup) m_decPopup.Value).Init(this);
					m_applier.ResetTabs();
					m_applier.ClearStyles();
					
					if (m_restorer == null)
						m_restorer = new RestoreViewState(this);
					
					// force runs to be rebuilt
					m_editCount = unchecked(m_editCount + 1);
					m_applier.EditedRange(NSRange.Empty);
					
					var edit = new TextEdit{
						Boss = m_boss,
						Language = m_language,
						UserEdit = true,
						EditedRange = NSRange.Empty,
						ChangeInLength = 0,
						ChangeInLines = 0,
						StartLine = 1};
					Broadcaster.Invoke("text changed", edit);
				}
			}
		}
		
		public int[] TabStops
		{
			get {return m_language != null ? m_language.TabStops : new int[0];}
		}
		
		public bool UsesTabs {get; private set;}
		
		public NSString SpacesText {get; private set;}
		
		internal NSScrollView ScrollView
		{
			get {return m_scrollView.Value;}
		}
		
		public void ShowInfo(string text)
		{
			if (ms_warning == null)
				ms_warning = new WarningWindow();
				
			ms_warning.Show(window(), text, 135, 206, 250);
		}
		
		public void ShowWarning(string text)
		{
			if (ms_warning == null)
				ms_warning = new WarningWindow();
				
			ms_warning.Show(window(), text, 250, 128, 114);
		}
		
		public Boss GetDirEditorBoss()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(m_boss);
			
			return boss;
		}
		
		// We can't restore the scoller until layout has completed (because the scroller 
		// doesn't know how many lines there are until this happens).
		public void layoutManager_didCompleteLayoutForTextContainer_atEnd(NSLayoutManager layout, NSTextContainer container, bool atEnd)
		{
			if (!m_closed)
			{
				if (m_restorer != null && (m_applier.Applied || m_language == null))
					if (m_restorer.OnCompletedLayout(layout, atEnd))
						m_restorer = null;
				
				if (atEnd)
				{
					Broadcaster.Invoke("layout completed", m_boss);
					DoPruneRanges();
				}
			}
		}
		
		// Count is used for the find indicator.
		public void ShowLine(int begin, int end, int count)
		{
			if (m_restorer != null)
			{
				m_restorer.ShowLine(begin, end, count);
			}
			else
			{
				m_textView.Value.setSelectedRange(new NSRange(begin, 0));
				m_textView.Value.scrollRangeToVisible(new NSRange(begin, end - begin));
				
				var thread = new System.Threading.Thread(() => DoDeferredFindIndicator(new NSRange(begin, count)));
				thread.Name = "deferred find indicator";
				thread.Start();
			}
		}
		
		public void ShowSelection()
		{
			NSRange range = m_textView.Value.selectedRange();
			if (m_restorer != null)
			{
				m_restorer.ShowSelection(range);
			}
			else
			{
				m_textView.Value.scrollRangeToVisible(range);
				
				var thread = new System.Threading.Thread(() => DoDeferredFindIndicator(range));
				thread.Name = "deferred find indicator";
				thread.Start();
			}
		}
		
		public NSRect window_willPositionSheet_usingRect(NSWindow window, NSWindow sheet, NSRect usingRect)
		{
			if (sheet.respondsToSelector("positionSheet:"))
				return sheet.Call("positionSheet:", usingRect).To<NSRect>();
			else
				return usingRect;
		}
		
		public new NSString windowTitleForDocumentDisplayName(NSString displayName)
		{
			NSString result = displayName;
			
			if (Path != null)
			{
				var defaults = NSUserDefaults.standardUserDefaults();
				if (defaults.boolForKey(NSString.Create("reverse window paths")))
				{
					result = NSString.Create(Path.ReversePath());
				}
			}
			
			if (m_boss.Has<IDocumentWindowTitle>())
			{
				var title = m_boss.Get<IDocumentWindowTitle>();
				result = NSString.Create(title.GetTitle(displayName.ToString()));
			}
			
			return result;
		}
		
		public void windowWillClose(NSObject notification)
		{
			Broadcaster.Invoke("closing document window", m_boss);
			m_closed= true;
			
			if (Path != null)
			{
				// If the document has changes but is not being saved then we don't
				// want to persist this information because it will likely be wrong when
				// the old text is loaded.
				if (!document().isDocumentEdited())
				{
					NSRect frame = window().frame();
					int length = (int) m_textView.Value.string_().length();
					NSPoint scrollers = m_scrollView.Value.contentView().bounds().origin;
					NSRange selection = m_textView.Value.selectedRange();
					WindowDatabase.Set(Path, frame, length, scrollers, selection, m_wordWrap);
				}
				
				if (Path.Contains("/var/") && Path.Contains("/-Tmp-/"))		// TODO: seems kind of fragile
					DoDeleteFile(Path);
			}
			
			var complete = m_boss.Get<IAutoComplete>();
			complete.Close();
			
			if (m_watcher != null)
			{
				m_watcher.Dispose();
				m_watcher.Changed -= this.DoDirChanged;
				m_watcher = null;
			}
			
			Broadcaster.Unregister(this);
			
			if (m_applier != null)
			{
				m_applier.Stop();
			}
			((DeclarationsPopup) m_decPopup.Value).Stop();
			
			m_textView.Value.Call("onClosing:", this);

			// If the windows are closed very very quickly then if we don't do this
			// we get a crash when Cocoa tries to call our delegate.
			m_textView.Value.layoutManager().setDelegate(null);
			
			window().autorelease();
			NSApplication.sharedApplication().BeginInvoke(	// we only want to broadcast this once the window actually closed, but we don't get a notification for that...
				() => Broadcaster.Invoke("closed document window", m_boss), TimeSpan.FromMilliseconds(250));
//			m_boss.Free();
		}
		
		public void getInfo(NSObject sender)
		{
			document().Call("getInfo");
		}
		
		public void openSelection(NSObject sender)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var opener = boss.Get<IOpenSelection>();
			
			bool valid = false;
			NSRange range = m_textView.Value.selectedRange();
			if (range.length > 0)
			{
				string text = Text;
				
				int loc = range.location, len = range.length;
				string str= text.Substring(loc, len);
				if (opener.IsValid(str))
				{
					if (opener.Open(text, ref loc, ref len))
						m_textView.Value.setSelectedRange(new NSRange(loc, len));
					else
						Functions.NSBeep();
						
					valid = true;
				}
			}
			
			if (!valid)
				opener.Open();
		}
		
		public void openDeclarationsMenu(NSObject sender)
		{
			m_decPopup.Value.performClick(this);
		}

		public void findGremlins(NSObject sender)
		{
			NSRange range = m_textView.Value.selectedRange();
			string text = Text;
			
			// Find the next non 7-bit ASCII character or control character.
			int index = -1;
			for (int offset = 0; offset < text.Length && index < 0; ++offset)
			{
				int i = (range.location + offset + 1) % text.Length;
				int codePoint = (int) text[i];
				
				if (codePoint < 32)
				{
					if (codePoint != '\t' && codePoint != '\n')	// note that carriage return is not OK because documents are supposed to use new lines in memory
					{
						index = i;
					}
				}
				else if (codePoint > 126)			// 127 is DEL so we want to count that
				{
					index = i;
				}
			}
			
			if (index >= 0)
			{
				// If we found one then select it and,
				int findIndex = m_oldFindIndex;
				range = new NSRange(index, 1);
				m_textView.Value.setSelectedRange(range);
				m_textView.Value.scrollRangeToVisible(range);
				m_textView.Value.showFindIndicatorForRange(range);
				
				// write its name to the transcript window.
				Boss boss = ObjectModel.Create("TextEditorPlugin");
				var names = boss.Get<IUnicodeName>();
				string name = names.GetName(text[index]);
				if (name != null)
				{
					boss = ObjectModel.Create("Application");
					var transcript = boss.Get<ITranscript>();
					transcript.WriteLine(Output.Normal, name);
					
					if (!transcript.Visible)
					{
						transcript.Show();
						window().makeKeyAndOrderFront(null);
					}
				}
				
				// Tell the user if we've wrapped all the way around.
				m_oldFindIndex = findIndex;
				if (m_oldFindIndex < 0)
				{
					m_oldFindIndex = index;
				}
				else if (index == m_oldFindIndex)
				{
					var editor = m_boss.Get<ITextEditor>();
					editor.ShowInfo("Reached Start");
				}
			}
			else
				Functions.NSBeep();
		}
		
		public void findSelection(NSObject sender)
		{
			NSRange range = m_textView.Value.selectedRange();
			
			m_textView.Value.scrollRangeToVisible(range);
			m_textView.Value.showFindIndicatorForRange(range);
		}
		
		public void balance(NSObject sender)
		{
			NSRange originalRange = m_textView.Value.selectedRange();
			string text = Text;
			
			NSRange range = m_metrics.Balance(text, originalRange);
			
			// If we get the same range back then try for a larger range.
			if (range.length > 2 && range.location + 1 == originalRange.location && range.length - 2 == originalRange.length)
				range = m_metrics.Balance(text, range);
			
			if (range.length > 2)
				m_textView.Value.setSelectedRange(new NSRange(range.location + 1, range.length - 2));
			else if (range.length > 0)
				m_textView.Value.setSelectedRange(range);
			else
				Functions.NSBeep();
		}
		
		public void shiftLeft(NSObject sender)
		{
			Unused.Value = Text;		// make sure m_metrics is up to date
			
			NSRange range = m_textView.Value.selectedRange();
			int firstLine = m_metrics.GetLine(range.location);
			int lastLine = m_metrics.GetLine(range.location + range.length - 1);
			
			var args = NSArray.Create(
				NSNumber.Create(firstLine),
				NSNumber.Create(lastLine),
				NSNumber.Create(-1));
			shiftLines(args);
		}
		
		public void shiftRight(NSObject sender)
		{
			Unused.Value = Text;		// make sure m_metrics is up to date
			
			NSRange range = m_textView.Value.selectedRange();
			int firstLine = m_metrics.GetLine(range.location);
			int lastLine = m_metrics.GetLine(range.location + range.length - 1);
			
			var args = NSArray.Create(
				NSNumber.Create(firstLine),
				NSNumber.Create(lastLine),
				NSNumber.Create(+1));
			shiftLines(args);
		}
		
		public void shiftLines(NSArray args)
		{
			int firstLine = args.objectAtIndex(0).To<NSNumber>().intValue();
			int lastLine = args.objectAtIndex(1).To<NSNumber>().intValue();
			int delta = args.objectAtIndex(2).To<NSNumber>().intValue();
			
			NSString tab;
			if (Language != null && !UsesTabs && !NSObject.IsNullOrNil(SpacesText))
				tab = SpacesText;
			else
				tab = NSString.Create("\t");
			
			NSTextStorage storage = m_textView.Value.textStorage();
			storage.beginEditing();
			try
			{
				Unused.Value = Text;		// make sure m_metrics is up to date
				
				for (int line = lastLine; line >= firstLine; --line)			// backwards so metrics doesn't get confused by our edits (it won't sync up until we call endEditing)
				{
					int offset = m_metrics.GetLineOffset(line);
					if (delta > 0)
					{
						storage.replaceCharactersInRange_withString(new NSRange(offset, 0), tab);
					}
					else
					{
						char ch = storage.string_().characterAtIndex((uint) offset);
						if (ch == '\t' || ch == ' ')								// need to stop shifting lines left when there is no more whitespace
							storage.deleteCharactersInRange(new NSRange(offset, 1));
					}
				}
			}
			finally
			{
				storage.endEditing();
			}
			
			int firstOffset = m_metrics.GetLineOffset(firstLine);
			int lastOffset = m_metrics.GetLineOffset(lastLine + 1);
			m_textView.Value.setSelectedRange(new NSRange(firstOffset, lastOffset - firstOffset));
			
			NSArray oldArgs = NSArray.Create(args.objectAtIndex(0), args.objectAtIndex(1), NSNumber.Create(-delta));
			window().windowController().document().undoManager().registerUndoWithTarget_selector_object(this, "shiftLines:", oldArgs);
			window().windowController().document().undoManager().setActionName(NSString.Create("Shift"));
		}
		
		public void toggleComment(NSObject sender)
		{
			Unused.Value = Text;		// make sure m_metrics is up to date
			
			NSRange range = m_textView.Value.selectedRange();
			int firstLine = m_metrics.GetLine(range.location);
			int lastLine = m_metrics.GetLine(range.location + range.length - 1);
			NSString comment = NSString.Create(m_language.LineComment);
			
			int offset = m_metrics.GetLineOffset(firstLine);
			bool add = !DoLineStartsWith(offset, comment);
			
			var args = NSArray.Create(
				NSNumber.Create(firstLine),
				NSNumber.Create(lastLine),
				comment,
				NSNumber.Create(add));
			toggleComments(args);
		}
		
		public void toggleComments(NSArray args)
		{
			int firstLine = args.objectAtIndex(0).To<NSNumber>().intValue();
			int lastLine = args.objectAtIndex(1).To<NSNumber>().intValue();
			NSString comment = args.objectAtIndex(2).To<NSString>();
			bool add = args.objectAtIndex(3).To<NSNumber>().boolValue();
			
			NSTextStorage storage = m_textView.Value.textStorage();
			storage.beginEditing();
			try
			{
				Unused.Value = Text;		// make sure m_metrics is up to date
				
				for (int line = lastLine; line >= firstLine; --line)			// backwards so metrics doesn't get confused by our edits (it won't sync up until we call endEditing)
				{
					int offset = m_metrics.GetLineOffset(line);
					if (add)
					{
						if (!DoLineStartsWith(offset, comment))
							storage.replaceCharactersInRange_withString(new NSRange(offset, 0), comment);
					}
					else
					{
						if (DoLineStartsWith(offset, comment))
							storage.deleteCharactersInRange(new NSRange(offset, (int) comment.length()));
					}
				}
			}
			finally
			{
				storage.endEditing();
			}
			
			int firstOffset = m_metrics.GetLineOffset(firstLine);
			int lastOffset = m_metrics.GetLineOffset(lastLine + 1);
			m_textView.Value.setSelectedRange(new NSRange(firstOffset, lastOffset - firstOffset));
			
			NSArray oldArgs = NSArray.Create(args.objectAtIndex(0), args.objectAtIndex(1), args.objectAtIndex(2), NSNumber.Create(!add));
			window().windowController().document().undoManager().registerUndoWithTarget_selector_object(this, "toggleComments:", oldArgs);
			window().windowController().document().undoManager().setActionName(NSString.Create("Toggle Comment"));
		}
		
		public void showSpaces(NSObject sender)
		{
			Boss boss = ObjectModel.Create("Stylers");
			var white = boss.Get<IWhitespace>();
			white.ShowSpaces = !white.ShowSpaces;
			
			m_editCount = unchecked(m_editCount + 1);
			m_applier.EditedRange(NSRange.Empty);
			
			var edit = new TextEdit{
				Boss = m_boss,
				Language = m_language,
				UserEdit = true,
				EditedRange = NSRange.Empty,
				ChangeInLength = 0,
				ChangeInLines = 0,
				StartLine = 1};
			Broadcaster.Invoke("text changed", edit);
		}
		
		public void showTabs(NSObject sender)
		{
			Boss boss = ObjectModel.Create("Stylers");
			var white = boss.Get<IWhitespace>();
			white.ShowTabs = !white.ShowTabs;
			
			m_editCount = unchecked(m_editCount + 1);
			m_applier.EditedRange(NSRange.Empty);
			
			var edit = new TextEdit{
				Boss = m_boss,
				Language = m_language,
				UserEdit = true,
				EditedRange = NSRange.Empty,
				ChangeInLength = 0,
				ChangeInLines = 0,
				StartLine = 1};
			Broadcaster.Invoke("text changed", edit);
		}
		
		public void findLine(NSObject sender)
		{
			NSRange range = m_textView.Value.selectedRange();
			int line = m_metrics.GetLine(range.location);
			
			var getter = new GetString{Title = "Find Line", Label = "Line:", Text = line.ToString(), ValidText = @"\d+"};
			string text = getter.Run();
			if (text != null)
			{
				line = int.Parse(text);
				
				int firstOffset = m_metrics.GetLineOffset(line);
				int lastOffset = m_metrics.GetLineOffset(line + 1);
				range = new NSRange(firstOffset, lastOffset - firstOffset);
				
				m_textView.Value.setSelectedRange(range);
				m_textView.Value.scrollRangeToVisible(range);
				m_textView.Value.showFindIndicatorForRange(range);
			}
		}
		
		public void dirHandler(NSObject sender)
		{
			if (Path != null)
			{
				NSWindow window = DoGetDirEditor();
				if (window != null)
					Unused.Value = window.windowController().Call("dirHandler:", sender);
			}
		}
		
		public void toggleWordWrap(NSObject sender)
		{
			m_wordWrap = !m_wordWrap;
			DoResetWordWrap();
		}
		
		public void lookUpInDict(NSObject sender)
		{
			NSRange range = m_textView.Value.selectedRange();
			string selection = Text.Substring(range.location, range.length);
			selection = selection.Replace(" ", "%20");
			
			NSURL url = NSURL.URLWithString(NSString.Create("dict:///" + selection));
			NSWorkspace.sharedWorkspace().openURL(url);
		}
		
		public bool StylesWhitespace
		{
			get {return m_language != null && m_language.StylesWhitespace;}
		}
		
		public bool WrapsWords
		{
			get {return m_wordWrap;}
		}
		
		public void textHandler(NSObject sender)
		{
			int tag = (int) sender.Call("tag");
			
			var handler = m_boss.Get<IMenuHandler>();
			handler.Handle(tag);
		}
		
		public bool validateUserInterfaceItem(NSObject item)
		{
			Selector sel = (Selector) item.Call("action");
			
			bool valid = false;
			if (sel.Name == "textHandler:")
			{
				int tag = (int) item.Call("tag");
				
				var handler = m_boss.Get<IMenuHandler>();
				MenuState state = handler.GetState(tag);
				valid = (state & MenuState.Enabled) == MenuState.Enabled;
				item.Call("setState:", (state & MenuState.Checked) == MenuState.Checked ? 1 : 0);
			}
			else if (sel.Name == "shiftLeft:" || sel.Name == "shiftRight:")
			{
				NSRange range = m_textView.Value.selectedRange();
				valid = range.length > 0 && m_textView.Value.isEditable();
			}
			else if (sel.Name == "lookUpInDict:")
			{
				NSRange range = m_textView.Value.selectedRange();
				NSString text = m_textView.Value.textStorage().string_().substringWithRange(range);
				valid = NSApplication.sharedApplication().Call("canLookupInDictionary:", text).To<bool>();
			}
			else if (sel.Name == "showSpaces:")
			{
				if (StylesWhitespace)
				{
					Boss boss = ObjectModel.Create("Stylers");
					var white = boss.Get<IWhitespace>();
					
					Unused.Value = item.Call("setTitle:", white.ShowSpaces ? NSString.Create("Hide Spaces") : NSString.Create("Show Spaces"));
					valid = true;
				}
			}
			else if (sel.Name == "showTabs:")
			{
				if (StylesWhitespace)
				{
					Boss boss = ObjectModel.Create("Stylers");
					var white = boss.Get<IWhitespace>();
					
					Unused.Value = item.Call("setTitle:", white.ShowTabs ? NSString.Create("Hide Tabs") : NSString.Create("Show Tabs"));
					valid = true;
				}
			}
			else if (sel.Name == "toggleComment:")
			{
				valid = m_language != null && m_language.LineComment != null;
			}
			else if (sel.Name == "toggleWordWrap:")
			{
				Unused.Value = item.Call("setTitle:", m_wordWrap ? NSString.Create("Don't Wrap Lines") : NSString.Create("Wrap Lines"));
				valid = true;
			}
			else if (sel.Name == "dirHandler:")
			{
				if (Path != null)
				{
					NSWindow window = DoGetDirEditor();
					if (window != null)
						valid = window.windowController().Call("validateUserInterfaceItem:", item).To<bool>();
					else
						valid = false;
				}
			}
			else if (respondsToSelector(sel))
			{
				valid = true;
			}
			else if (SuperCall(NSWindowController.Class, "respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				valid = SuperCall(NSWindowController.Class, "validateUserInterfaceItem:", item).To<bool>();
			}
			
			return valid;
		}
		
		public int EditCount
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				return m_editCount;
			}
		}
		
		public void RegisterRange(ConcreteLiveRange range)
		{
			m_ranges.Add(new WeakReference(range));
		}
		
		// editedRange is the range of the new text. For example if a character
		// is typed it will be the range of the new character, if text is pasted it
		// will be the range of the inserted text.
		//
		// changeInLength is the difference in length between the old selection
		// and the new text.
		//
		// TODO: cStringUsingEncoding might allow us to skip a copy when getting
		// the text (might also help keyDown)
		public void textStorageDidProcessEditing(NSObject notification)
		{
			NSTextStorage storage = m_textView.Value.textStorage();
			if ((storage.editedMask() & Enums.NSTextStorageEditedCharacters) != 0)
			{
				m_editCount = unchecked(m_editCount + 1);
				m_oldFindIndex = -1;
				
				int oldNumLines = m_metrics.LineCount;
				string text = Text;										// TODO: this is slow for very large files
				
				NSRange range = storage.editedRange();
				int lengthChange = storage.changeInLength();
				
				DoUpdateLineLabel(text);
				DoUpdateRanges(range, lengthChange);
				m_applier.EditedRange(range);
				
				if (m_userEdit)
				{
					document().updateChangeCount(Enums.NSChangeDone);
					
					// If the user typed a closing brace and it is balanced,
					int left = m_metrics.BalanceLeft(text, range.location + range.length - 1);
					if (left != -2)
					{
						// then highlight the open brace.
						if (left >= 0)
						{
							NSRange openRange = new NSRange(left, 1);
							NSApplication.sharedApplication().BeginInvoke(() => DoShowOpenBrace(openRange, range));	// can't do a show if we're in the middle of an edit...
						}
						else if (range.location >= 0 && range.location < text.Length)
						{
							// Otherwise pop up a translucent warning window for a second.
							ShowWarning("Unmatched '" + text[range.location] + "'");
						}
					}
					
					// Auto-indent new lines.
					if (range.length == 1 && text[range.location] == '\n' && lengthChange > 0)
					{
						int i = range.location - 1;
						while (i > 0 && text[i] != '\n')
							--i;
						
						++i;
						int count = 0;
						while (i + count < range.location && char.IsWhiteSpace(text[i + count]))
							++count;
						
						if (count > 0)
						{
							string padding = text.Substring(i, count);
							NSApplication.sharedApplication().BeginInvoke(() => m_textView.Value.insertText(NSString.Create(padding)));
						}
					}
				}
				
				var edit = new TextEdit{
					Boss = m_boss,
					Language = m_language,
					UserEdit = m_userEdit,
					EditedRange = range,
					ChangeInLength = lengthChange,
					ChangeInLines = m_metrics.LineCount - oldNumLines,
					StartLine = m_metrics.GetLine(range.location)};
				Broadcaster.Invoke("text changed", edit);
			}
		}
		
		internal void ResetStyles()
		{
			if (m_language == null && Path == null)
			{
				m_applier.ClearStyles();
			}
		}
		
		public void textViewDidChangeSelection(NSNotification notification)
		{
			string text = Text;			// note that this will ensure m_metrics is up to date
			
			m_oldFindIndex = -1;
			
//			if (m_language != null)
			{
				int line = -1;
				int offset = -1;
				int length = 0;
				
				// Change the background color of the line the selection is within if the
				// selection is within one line.
				NSRange range = m_textView.Value.selectedRange();
				if (range.length < 200)				// don't search for new lines if the selection is something crazy like the entire document
				{
					if (range.location < text.Length && text.IndexOf('\n', range.location, range.length) < 0)
					{
						line = m_metrics.GetLine(range.location);
						offset = m_metrics.GetLineOffset(line);
						length = m_metrics.GetLineOffset(line + 1) - offset;
					}
				}
				
				if (offset >= 0)
					m_applier.HighlightLine(offset, length);
			}
			
			DoUpdateLineLabel(text);
			m_decPopup.Value.Call("textSelectionChanged");
		}
		
		public TextMetrics Metrics
		{
			get {return m_metrics;}
		}
		
		#region Private Methods
		private ILanguage DoFindLanguage()
		{
			// If there is a IDocumentExtension use that in place of the real file name.
			string fileName = null;
			
			if (m_boss.Has<IDocumentExtension>())
			{
				var ext = m_boss.Get<IDocumentExtension>();
				string extension = ext.GetExtension();
				if (extension != null)
					fileName = "foo" + extension;
			}
			
			if (fileName == null)
				fileName = System.IO.Path.GetFileName(Path);
			
			// First check to see if the document was opened as binary.
			if (document().respondsToSelector("isBinary") && document().Call("isBinary").To<bool>())
				fileName = "foo.bin";
			
			// Then see if a language can be inferred from the file name
			// (usually this will be based on the extension but sometimes it is
			// the file name itself, eg "Makefile").
			ILanguage language = null;
			Boss boss = ObjectModel.Create("Stylers");
			if (fileName != null)
			{
				foreach (IFindLanguage find in boss.GetRepeated<IFindLanguage>())
				{
					language = find.FindByExtension(fileName);
					if (language != null)
						return language;
				}
			}
			
			// Finally see if the file has a shebang.
			NSString str = m_textView.Value.string_();
			if (str.length() > 4)
			{
				if (str.characterAtIndex(0) == '#' && str.characterAtIndex(1) == '!' && str.characterAtIndex(2) == '/')
				{
					int i = Text.IndexOfAny(new char[]{' ', '\t', '\r', '\n'});
					if (i > 0)
					{
						string path = Text.Substring(2, i - 2);
						string bang = System.IO.Path.GetFileName(path);
						foreach (IFindLanguage find in boss.GetRepeated<IFindLanguage>())
						{
							language = find.FindByShebang(bang);
							if (language != null)
								return language;
						}
					}
				}
			}
			
			return null;
		}
		
		private void DoSetTabSettings()
		{
			UsesTabs = true;
			int numSpaces = 4;
			
			Boss boss = GetDirEditorBoss();
			if (boss != null)
			{
				var editor = boss.Get<IDirectoryEditor>();
				UsesTabs = editor.UseTabs;
				numSpaces = editor.NumSpaces;
			}
			
			// Language setting overrides the directory pref.
			if (m_language != null && m_language.UseTabs.HasValue)
			{
				UsesTabs = m_language.UseTabs.Value;
			}
			
			if (SpacesText != null)
				SpacesText.release();
			SpacesText = NSString.Create(new string(' ', numSpaces));
			SpacesText.retain();
		}
		
		// This is retarded, but showFindIndicatorForRange only works if the window is
		// already visible and the indicator doesn't always show up if we simply use 
		// BeginInvoke.
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoDeferredFindIndicator(NSRange range)
		{
			System.Threading.Thread.Sleep(200);
			
			NSApplication.sharedApplication().BeginInvoke(() => m_textView.Value.showFindIndicatorForRange(range));
		}
		
		public void DoResetWordWrap()
		{
			// This code is a bit weird and rather delicate:
			// 1) The container needs to be sized to the scroll view not the text view.
			// 2) sizeToFit must be called for some reason.
			// If this is not done the text (usually) does not wrap.
			if (m_wordWrap)
			{
				NSSize contentSize = m_scrollView.Value.contentView().bounds().size;
				m_textView.Value.textContainer().setContainerSize(new NSSize(contentSize.width, float.MaxValue));
				m_textView.Value.textContainer().setWidthTracksTextView(true);
			}
			else
			{
				m_textView.Value.textContainer().setContainerSize(new NSSize(float.MaxValue, float.MaxValue));
				m_textView.Value.textContainer().setWidthTracksTextView(false);
			}
			
			m_textView.Value.sizeToFit();
		}
		
		private void DoShowOpenBrace(NSRange openRange, NSRange closeRange)
		{
			m_textView.Value.scrollRangeToVisible(openRange);
			m_textView.Value.showFindIndicatorForRange(openRange);
			
			NSApplication.sharedApplication().BeginInvoke(() => DoScrollBack(closeRange), TimeSpan.FromMilliseconds(333));	
		}
		
		private void DoScrollBack(NSRange closeRange)
		{
			m_textView.Value.scrollRangeToVisible(closeRange);
		}
		
		private NSWindow DoGetDirEditor()
		{
			Boss boss = GetDirEditorBoss();
			if (boss != null)
			{
				var wind = boss.Get<IWindow>();
				return wind.Window;
			}
			
			return null;
		}
		
		private void DoSetTextOptions()
		{
			// Disable word wrap by default (OnCompletedLayout will enable it if needed).
			m_textView.Value.setAutoresizingMask(Enums.NSViewWidthSizable | Enums.NSViewHeightSizable);
			m_textView.Value.setMaxSize(new NSSize(float.MaxValue, float.MaxValue));
			
			m_textView.Value.textContainer().setContainerSize(new NSSize(float.MaxValue, float.MaxValue));
			m_textView.Value.textContainer().setWidthTracksTextView(false);
			
			// Set the text delegate.
			m_textView.Value.textStorage().setDelegate(this);
		}
		
		private void DoUpdateDefaultColor(string name, object value)
		{
			NSColor color = null;
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create("text default color")).To<NSData>();
			
			if (!NSObject.IsNullOrNil(data))
			{
				color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
				m_textView.Value.setBackgroundColor(color);
			}
		}
		
		private void DoUpdateLineLabel(string text)
		{
			string label = string.Empty;
			
			NSRange range = m_textView.Value.selectedRange();
			int firstLine = m_metrics.GetLine(range.location);
			
			if (range.length > 0)
			{
				int lastLine = m_metrics.GetLine(range.location + range.length - 1);
				label = string.Format("{0}-{1}", firstLine, lastLine);
			}
			else
			{
				int col = m_metrics.GetCol(range.location);
				label = string.Format("{0}:{1}", firstLine, col);
			}
			
			m_lineLabel.Value.setTitle(NSString.Create(label));
		}
		
		private void DoUpdateRanges(NSRange edited, int lengthChange)
		{
			NSRange affectedRange = new NSRange(edited.location, edited.length - lengthChange);
			for (int i = m_ranges.Count - 1; i >= 0; --i)
			{
				ConcreteLiveRange range = m_ranges[i].Target as ConcreteLiveRange;
				if (range != null)
				{
					if (affectedRange.location + affectedRange.length < range.Index)
					{
						range.Reset(range.Index + lengthChange);
					}
					else if (affectedRange.Intersects(new NSRange(range.Index, range.Length)))
					{
						range.Reset(-1);
					}
				}
				else
				{
					m_ranges.RemoveAt(i);
				}
			}
		}
		
		private void DoPruneRanges()
		{
			for (int i = m_ranges.Count - 1; i >= 0; --i)
			{
				ConcreteLiveRange range = m_ranges[i].Target as ConcreteLiveRange;
				if (range == null)
					m_ranges.RemoveAt(i);
			}
		}
		
		private bool DoLineStartsWith(int offset, NSString text)
		{
			bool match = false;
			
			NSString buffer =  m_textView.Value.textStorage().string_();
			if (offset + text.length() <= buffer.length())
			{
				int result = buffer.compare_options_range(text, 0, new NSRange(offset, (int) text.length()));
				match = result == Enums.NSOrderedSame;
//				Console.WriteLine("'{0}...' == '{1}' = {2}", buffer.ToString().Substring(offset, 12).EscapeAll(), text.ToString(), match);
			}
			
			return match;
		}
		
		private void DoDirChanged(object sender, DirectoryWatcherEventArgs e)
		{
			foreach (string p in e.Paths)
			{
				NSString path = NSString.Create(p).stringByResolvingSymlinksInPath();
				
				if (Paths.AreEqual(path.description(), m_dir.description()))
				{
					var reload = m_boss.Get<IReload>();
					reload.Reload();
					break;
				}
			}
		}
		
		// Commands like show short form or show derived create temporary files to show
		// their results. This isn't a huge problem because they are deleted on restarts, but
		// it does clutter both the file system and the open recent menu so we'll delete them
		// once we're done with them.
		private void DoDeleteFile(string path)
		{
			try
			{
				System.IO.File.Delete(path);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "Coudn't delete '{0}'", path);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
		}
		#endregion
		
		#region Fields
		private IBOutlet<NSTextView> m_textView;
		private IBOutlet<NSButton> m_lineLabel;
		private IBOutlet<NSPopUpButton> m_decPopup;
		private IBOutlet<NSScrollView> m_scrollView;
		private Boss m_boss;
		private ILanguage m_language;
		private ApplyStyles m_applier;
		private bool m_userEdit = true;
		private TextMetrics m_metrics = new TextMetrics(string.Empty);
		private DirectoryWatcher m_watcher;
		private NSString m_dir;
		private string m_cachedText;
		private int m_cachedEditCount = -1;
		private int m_editCount;
		private bool m_closed;
		private RestoreViewState m_restorer;
		private bool m_wordWrap;
		private List<WeakReference> m_ranges = new List<WeakReference>();
		private int m_oldFindIndex = -1;
		
		public static WarningWindow ms_warning;
		#endregion
	}
}
