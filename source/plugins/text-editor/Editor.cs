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

using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;
using System.Globalization;

namespace TextEditor
{
	internal sealed class Editor : IWindow, ITextEditor, IText, IReload
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		// IWindow
		public NSWindow Window 
		{
			get {Trace.Assert(m_window != null, "window isn't set"); return m_window;}
			set {Trace.Assert(value != null, "value is null"); Trace.Assert(m_window == null, "window isn't null"); m_window = (NSWindow) value;}
		}

		// ITextEditor
		public string Path
		{
			get
			{
				Trace.Assert(m_window != null, "window isn't set");
			
				TextController controller = (TextController) m_window.windowController();
				return controller.Path;
			}
		}
		
		public void Save()
		{
			NSDocument doc = m_window.windowController().document();
			if (!NSObject.IsNullOrNil(doc.fileURL()) && doc.isDocumentEdited())
				doc.saveDocument(m_window.windowController());
		}
		
		public static int GetOffset(string text, int line, int col, int tabWidth)
		{
			Trace.Assert(line >= 1, "line is not positive");
			Trace.Assert(col == -1 || col >= 1, "col is not -1 or a positive number");
			Trace.Assert(tabWidth >= 1, "tabWidth is not positive");
						
			int begin = DoGetOffset(text, line - 1);	
			
			int c = col - 1;
			while (begin < text.Length && c > 0)
			{
				if (text[begin] == '\t')
					c -= tabWidth;
				else
					c -= 1;
				
				++begin;
			}
			
			return begin;
		}
		
		public void ShowLine(int line, int col, int tabWidth)
		{
			Trace.Assert(line >= 1, "line is not positive");
			Trace.Assert(col == -1 || col >= 1, "col is not -1 or a positive number");
			Trace.Assert(tabWidth >= 1, "tabWidth is not positive");
			
			string text = Text;
			
			int begin = GetOffset(text, line, col, tabWidth);	
			int end = DoGetOffset(text, line);
						
			if (begin > end)		// may happen if the line was edited
			{
				end = begin;
				col = -1;
			}
			
			if (begin < text.Length)
			{
				int count = 0;										// it looks kind of stupid to animate the entire line so we find a range of similar text to hilite
				if (col > 0)
				{
					UnicodeCategory cat = DoGetMungedCat(text[begin]);
					while (begin + count < text.Length && DoGetMungedCat(text[begin + count]) == cat)
						++count;
				}
				else
				{
					int i = text.IndexOfAny(new char[]{'\r', '\n'}, begin);
					if (i >= 0)
						count = i - begin;
					else if (begin < text.Length)
						count = 1;
				}
				
				TextController controller = (TextController) m_window.windowController();
				controller.ShowLine(begin, end, count);
			}
		}
		
		// IReload
		public void Reload()
		{
			var doc = (TextDocument) m_window.windowController().document();
			if (doc.HasChangedOnDisk())
			{
				if (doc.isDocumentEdited())
				{
					// Cocoa will display this sheet, but it does it when the user attempts
					// to save which is later than we like.
					var title = NSString.Create("The file for this document has been modified - do you want to revert?");
					var message = NSString.Create("Another application has made changes to the file for this document. You can choose to keep the version in Continuum, or revert to the version on disk. (Reverting will lose any unsaved changes.)");
					
					Functions.NSBeginAlertSheet(
						title,						// title
						NSString.Create("Revert"),	// defaultButton,
						NSString.Create("Keep"),	// alternateButton
						null,						// otherButton
						m_window,					// docWindow
						doc,						// modalDelegate
						"reloadSheetDidEnd:returnCode:contextInfo:",	// didEndSelector
						null,						// didDismissSelector
						IntPtr.Zero,				// contextInfo
						message);					// message
				}
				else
				{
					doc.Reload();
				}
			}
		}

		// IText
		public string Text 
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				return controller.Text;
			}
		}
		
		public NSRange Selection 
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				NSTextView view = controller.TextView;
				return view.selectedRange();
			}
			
			set
			{
				TextController controller = (TextController) m_window.windowController();
				NSTextView view = controller.TextView;
				
				view.setSelectedRange(value);
			}
		}
		
		public void Replace(string replacement, int index, int length, string undoText)
		{
			TextController controller = (TextController) m_window.windowController();
			NSTextView view = controller.TextView;
			
			NSRange range = new NSRange(index, length);
			NSString str = NSString.Create(replacement);
			if (view.shouldChangeTextInRange_replacementString(range, str))
			{
				view.replaceCharactersInRange_withString(range, str);
				
				NSUndoManager undo = view.undoManager();
				undo.setActionName(NSString.Create(undoText));
			}
		}
		
		public void ShowSelection()
		{
			TextController controller = (TextController) m_window.windowController();
			controller.ShowSelection();
		}
		
		#region Private Methods
		private UnicodeCategory DoGetMungedCat(char ch)
		{
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			
			switch (cat)
			{
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.UppercaseLetter:
					return UnicodeCategory.OtherLetter;
					
				default:
					return cat;
			}
		}
		
		// TODO: may want to maintain a line offset table	
		private static int DoGetOffset(string text, int forLine)
		{
			int offset = 0, line = 0;
			
			while (line < forLine && offset < text.Length)
			{
				if (offset + 1 < text.Length && text[offset] == '\r' && text[offset+1] == '\n')
				{
					++offset;
					++line;
				}
				else if (text[offset] == '\r' || text[offset] == '\n')
				{
					++line;
				}
				
				++offset;
			}
			
			return offset;
		}
		#endregion

		#region Fields
		private Boss m_boss; 
		private NSWindow m_window;
		#endregion
	} 
}	