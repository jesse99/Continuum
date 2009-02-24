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
using System.Text.RegularExpressions;

namespace TextEditor
{
	[ExportClass("TextController", "NSWindowController", Outlets = "textView lineLabel decsPopup scrollView")]
	internal sealed class TextController : NSWindowController
	{
		public TextController() : base("TextController", "text-editor")
		{
			m_textView = new IBOutlet<NSTextView>(this, "textView");
			m_lineLabel = new IBOutlet<NSButton>(this, "lineLabel");
			m_decPopup = new IBOutlet<NSPopUpButton>(this, "decsPopup");
			m_scrollView = new IBOutlet<NSScrollView>(this, "scrollView");
			
			m_boss = ObjectModel.Create("TextEditor");
			var wind = m_boss.Get<IWindow>();
			wind.Window = window();
			
			m_styler = m_boss.Get<IStyler>();
			m_applier = new ApplyStyles(this, m_textView.Value, m_scrollView.Value);
			DoSetTextOptions();
			
			Broadcaster.Register("text default color changed", this, this.DoUpdateDefaultColor);	
			DoUpdateDefaultColor(string.Empty, null);
			
			ActiveObjects.Add(this);
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
			// We can't highlight control characters because they have zero width so we'll
			// grow to the left until we find a non-control character.
			string text = Text;
			while (offset > 0 && char.IsControl(text, offset) && text[offset] != '\t')
			{
				--offset;
				++length;
			}
			
			m_applier.HighlightError(offset, length);
		}
		
		public void OnPathChanged()
		{
			if (Path != null)
			{
				DoGetStyler();
				
				Unused.Value = window().setFrameAutosaveName(NSString.Create(Path + "-doc window"));	// TODO: not sure it's good to stick all of these in prefs
				
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
				
				string dir = System.IO.Path.GetDirectoryName(Path);
				m_dir = NSString.Create(dir).stringByStandardizingPath().Retain();
				m_watcher = new DirectoryWatcher(dir, TimeSpan.FromMilliseconds(500));
				m_watcher.Changed += this.DoDirChanged;	
			}
			else
				((DeclarationsPopup) m_decPopup.Value).Init(this, null);			
		}
		
		public string Text
		{
			get
			{
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
					
					m_applier.Reset(value);
					m_textView.Value.setSelectedRange(new NSRange(0, 0));
					if (m_computer != null)
						m_styler.Apply(m_computer, this.DoStylerFinished);
					
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
			window().makeKeyAndOrderFront(this);
			m_textView.Value.layoutManager().setDelegate(this);
		}
		
		public NSTextView TextView
		{
			get {return m_textView.Value;}
		}
		
		public string Path
		{
			get {return !NSObject.IsNullOrNil(document().fileURL()) ? document().fileURL().path().description() : null;}	
		}
		
		public Boss GetDirEditorBoss()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(m_boss);
			
			return boss;
		}
		
		// We can't restore the scoller until the text has been styled (because of things like
		// font size changes) and layout has completed (because the scroller doesn't know
		// how many lines there are until this happens).
		public void layoutManager_didCompleteLayoutForTextContainer_atEnd(NSLayoutManager mgr, NSTextContainer container, bool atEnd)
		{
			if (atEnd && m_finishedStyling && !m_opened && !m_scrolled && !m_closed)
			{
				DoRestoreScrollers();
				m_opened = true;
			}
		}
		
		// Count is used for the find indicator.
		public void ShowLine(int begin, int end, int count)
		{
			m_textView.Value.setSelectedRange(new NSRange(begin, 0));
			m_textView.Value.scrollRangeToVisible(new NSRange(begin, end - begin));
				
			var thread = new System.Threading.Thread(() => DoDeferredFindIndicator(new NSRange(begin, count)));
			thread.Name = "deferred find indicator";
			thread.Start();
			
			m_scrolled = true;
		}
		
		public void ShowSelection()
		{
			NSRange range = m_textView.Value.selectedRange();
			m_textView.Value.scrollRangeToVisible(range);
			
			var thread = new System.Threading.Thread(() => DoDeferredFindIndicator(range));
			thread.Name = "deferred find indicator";
			thread.Start();
			
			m_scrolled = true;
		}
		
		public void windowWillClose(NSObject notification)
		{
			m_closed= true;
			
			if (Path != null)
			{
				NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
				NSPoint origin = m_scrollView.Value.contentView().bounds().origin;
				defaults.setFloat_forKey(origin.x, NSString.Create(Path + "-origin.x"));
				defaults.setFloat_forKey(origin.y, NSString.Create(Path + "-origin.y"));
				
				if (Path.Contains("/var/") && Path.Contains("/-Tmp-/"))		// TODO: seems kind of fragile
					DoDeleteFile(Path);
			}
			
			if (m_watcher != null)
			{
				m_watcher.Dispose();
				m_watcher = null;
			}
			
			Broadcaster.Unregister(this);
			
			if (m_styler != null)					// may be null if ctor threw
				m_styler.Close();
				
			if (m_applier != null)
				m_applier.Stop();
			
			// If the windows are closed very very quickly then if we don't do this
			// we get a crash when Cocoa tries to call our delegate.
			m_textView.Value.layoutManager().setDelegate(null);

			autorelease();
		}
		
		public void openSelection(NSObject sender)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var opener = boss.Get<IOpenSelection>();
			
			var validator = new Regex(@"\S+");
			
			bool valid = false;
			NSRange range = m_textView.Value.selectedRange();
			if (range.length > 0)
			{
				string text = Text;
				
				int loc = range.location, len = range.length;
				string str= text.Substring(loc, len);
				if (validator.IsMatch(str) && !str.Contains('\n'))
				{
					if (opener.Open(text, ref loc, ref len))
						m_textView.Value.setSelectedRange(new NSRange(loc, len));
					else
						Functions.NSBeep();
						
					valid = true;
				}
			}
			
			if (!valid)
			{
				string text = new GetString{Title = "Open Selection", ValidRegex = validator}.Run();
				
				if (text != null)
					if (!opener.Open(text))
						Functions.NSBeep();
			}
		}
		
		public void findGremlins(NSObject sender)
		{
			NSRange range = m_textView.Value.selectedRange();
			string text = Text;
			
			// Find the next non 7-bit ASCII character or control character.
			int index = -1;
			for (int i = range.location + 1; i < text.Length && index < 0; ++i)
			{
				int codePoint = (int) text[i];
				
				if (codePoint < 32)
				{
					if (codePoint != 9 && codePoint != 10 && codePoint != 13)	// tab, new line, and carriage return are OK
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
			}
			else
				Functions.NSBeep();
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
			
			var tab = NSString.Create("\t");
			
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
				
		public void showSpaces(NSObject sender)
		{
			if (m_computer != null)
			{
				Boss boss = ObjectModel.Create("Stylers");
				var white = boss.Get<IWhitespace>();
				white.ShowSpaces = !white.ShowSpaces;
				
				m_styler.Apply(m_computer, this.DoStylerFinished);
			}
		}
		
		public void showTabs(NSObject sender)
		{
			if (m_computer != null)
			{
				Boss boss = ObjectModel.Create("Stylers");
				var white = boss.Get<IWhitespace>();
				white.ShowTabs = !white.ShowTabs;
				
				m_styler.Apply(m_computer, this.DoStylerFinished);
			}
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
		
		public bool StylesWhitespace
		{
			get {return m_computer != null && m_computer.StylesWhitespace;}
		}
		
		public bool validateUserInterfaceItem(NSObject item)
		{
			Selector sel = (Selector) item.Call("action");
			
			bool valid = false;
			if (sel.Name == "shiftLeft:" || sel.Name == "shiftRight:")
			{
				NSRange range = m_textView.Value.selectedRange();
				valid = range.length > 0;
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
			else if (SuperCall("respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				valid = SuperCall("validateUserInterfaceItem:", item).To<bool>();
			}
			
			return valid;
		}
		
		public int EditCount
		{
			get {return m_editCount;}
		}
		
		public void textStorageDidProcessEditing(NSObject notification)
		{
			NSTextStorage storage = m_textView.Value.textStorage();
			if ((storage.editedMask() & Enums.NSTextStorageEditedCharacters) != 0)
			{
				m_editCount = unchecked(m_editCount + 1);
			
				int oldNumLines = m_metrics.LineCount;
				string text = Text;										// TODO: this is slow for very large files
				
				NSRange range = storage.editedRange();
				
				if (m_computer != null)
					m_styler.Queue(m_computer, this.DoStylerFinished);
				
				DoUpdateLineLabel(text);
				m_applier.EditedRange(range);
				
				if (m_userEdit)
				{
					Broadcaster.Invoke("text range changed", Tuple.Make(Path, range, storage.changeInLength()));
					
					// Let people who care know if the number of lines has changed
					// (e.g. so build errors can be fixed up).
					if (Path != null)
					{
						int deltaLines = m_metrics.LineCount - oldNumLines;
						int startLine = m_metrics.GetLine(range.location);
						
						Broadcaster.Invoke("text lines changed", Tuple.Make(Path, startLine, deltaLines));
					}
					
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
							if (ms_warning == null)
								ms_warning = new WarningWindow();
								
							ms_warning.Show(window(), "Unmatched '" + text[range.location] + "'");
						}
					}
					
					// Auto-indent new lines.
					if (range.length == 1 && text[range.location] == '\n' && storage.changeInLength() > 0)
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
			}
		}
		
		public void textViewDidChangeSelection(NSNotification notification)
		{
			string text = Text;			// note that this will ensure m_metrics is up to date
			
			if (m_computer != null)
			{
				int line = -1;
				int offset = 0;
				int length = 0;
				
				// Change the background color of the line the selection is within if the
				// selection is within one line.
				NSRange range = m_textView.Value.selectedRange();
				if (range.length < 200)				// don't search for new lines if the selection is something crazy like the entire document
				{
					if (text.IndexOf('\n', range.location, range.length) < 0)
					{
						line = m_metrics.GetLine(range.location);
						offset = m_metrics.GetLineOffset(line);
						length = m_metrics.GetLineOffset(line + 1) - offset;
					}
				}
				
				m_applier.HighlightLine(offset, length);
			}
			
			DoUpdateLineLabel(text);
			m_decPopup.Value.Call("textSelectionChanged");
		}
		
		#region Private Methods
		private void DoGetStyler()
		{
			string fileName = System.IO.Path.GetFileName(Path);	
			
			IComputeRuns computer = null;
			IDeclarations decs = null;
			Boss boss = ObjectModel.Create("Stylers");
			if (boss.Has<IFindLanguage>())
			{
				var find = boss.Get<IFindLanguage>();
				while (find != null && computer == null)
				{
					Boss language = find.Find(fileName);
					if (language != null)
					{
						computer = language.Get<IComputeRuns>();
						
						if (language.Has<IDeclarations>())
							decs = language.Get<IDeclarations>();
					}
					find = boss.GetNext<IFindLanguage>(find);
				}
			}
			
			((DeclarationsPopup) m_decPopup.Value).Init(this, decs);
			
			if (m_computer == null)
			{
				m_computer = computer;
			}
			else if (m_computer != computer)
			{
				m_computer = computer;
				if (m_computer != null)
					m_styler.Apply(m_computer, this.DoStylerFinished);	// we only want to call this if the document is saved under a new name because the text view starts out with that lame latin
			}
		}
		
		// This is retarded, but showFindIndicatorForRange only works if the window is
		//  already visible and the indicator doesn't always show up if we simply use 
		// BeginInvoke.
		private void DoDeferredFindIndicator(NSRange range)
		{
			System.Threading.Thread.Sleep(200);
			
			NSApplication.sharedApplication().BeginInvoke(() => m_textView.Value.showFindIndicatorForRange(range));
		}
		
		private void DoRestoreScrollers()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			float x = defaults.floatForKey(NSString.Create(Path + "-origin.x"));
			float y = defaults.floatForKey(NSString.Create(Path + "-origin.y"));
			
			if (x != 0.0 || y != 0.0)
				DoRestoreScrollers(x, y);
		}
		
		private void DoRestoreScrollers(float x, float y)
		{
			var clip = m_scrollView.Value.contentView().To<NSClipView>();
			clip.scrollToPoint(new NSPoint(x, y));
			m_scrollView.Value.reflectScrolledClipView(clip);
		}
		
		private void DoStylerFinished()
		{
			if (!m_closed)
			{
				int edit;
				StyleRun[] runs;
				CsGlobalNamespace globals;
				
				var styles = m_boss.Get<IStyles>();
				styles.Get(out edit, out runs, out globals);
				
				if (edit == m_editCount)
				{
					m_decPopup.Value.Call("textWasStyled");
					m_applier.Apply(edit, new List<StyleRun>(runs));	// applier mutates runs...
					m_finishedStyling = true;
				}
			}
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
			// Instead of wrapping use the horizontal scrollbar. TODO: make this a pref?
			m_textView.Value.setAutoresizingMask(Enums.NSViewWidthSizable | Enums.NSViewHeightSizable);
			m_textView.Value.setMaxSize(new NSSize(float.MaxValue, float.MaxValue));
			m_textView.Value.textContainer().setContainerSize(new NSSize(float.MaxValue, float.MaxValue));
			m_textView.Value.textContainer().setWidthTracksTextView(false);
			
			m_textView.Value.textStorage().setDelegate(this);
		}
				
		private void DoUpdateDefaultColor(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create("text default color")).To<NSData>();
			
			if (!NSObject.IsNullOrNil(data))
			{
				var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
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
		
		private void DoDirChanged(object sender, DirectoryWatcherEventArgs e)
		{
			foreach (string path in e.Paths)
			{
				if (Paths.AreEqual(path, m_dir.description()))
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
		private IComputeRuns m_computer;
		private IStyler m_styler;
		private ApplyStyles m_applier;
		private bool m_userEdit = true;
		private TextMetrics m_metrics = new TextMetrics(string.Empty);
		private DirectoryWatcher m_watcher;
		private NSString m_dir;
		private string m_cachedText;
		private int m_cachedEditCount = -1;
		private int m_editCount;
		private bool m_finishedStyling;
		private bool m_opened;
		private bool m_closed;
		private bool m_scrolled;
		
		public static WarningWindow ms_warning;
		#endregion
	}
}
