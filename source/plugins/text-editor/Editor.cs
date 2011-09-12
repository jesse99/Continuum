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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TextEditor
{
	internal sealed class Editor : IWindow, ITextEditor, IText, IReload, ITextMetrics
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			m_key = ms_nextKey++;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		// IWindow
		public NSWindow Window
		{
			get {Contract.Requires(m_window != null, "window isn't set"); return m_window;}
			set {Contract.Requires(value != null, "value is null"); Contract.Requires(m_window == null, "window isn't null"); m_window = value;}
		}
		
		// ITextEditor
		public string Path
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				TextController controller = (TextController) m_window.windowController();
				return controller.Path;
			}
		}
		
		public string Language
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				return controller.Language != null ? controller.Language.FriendlyName : null;
			}
		}
		
		public string Key
		{
			get
			{
				return Path ?? ("untitled" + m_key);
			}
		}
		
		public bool Editable
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				return controller.TextView.isEditable();
			}
			set
			{
				TextController controller = (TextController) m_window.windowController();
				controller.TextView.setEditable(value);
			}
		}
		
		public void Save()
		{
			NSDocument doc = m_window.windowController().document();
			if (!NSObject.IsNullOrNil(doc.fileURL()) && doc.isDocumentEdited())
			{
				doc.saveDocument(m_window.windowController());
				
				TextController controller = (TextController) m_window.windowController();
				controller.TextView.breakUndoCoalescing();
			}
		}
		
		public static int GetOffset(string text, int line, int col, int tabWidth)
		{
			Contract.Requires(line >= 1, "line is not positive");
			Contract.Requires(col == -1 || col >= 1, "col is not -1 or a positive number");
			Contract.Requires(tabWidth >= 1, "tabWidth is not positive");
			
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
			Contract.Requires(line >= 1, "line is not positive");
			Contract.Requires(col == -1 || col >= 1, "col is not -1 or a positive number");
			Contract.Requires(tabWidth >= 1, "tabWidth is not positive");
			
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
		
		public void ShowInfo(string text)
		{
			TextController controller = (TextController) m_window.windowController();
			controller.ShowInfo(text);
		}
		
		public void ShowWarning(string text)
		{
			TextController controller = (TextController) m_window.windowController();
			controller.ShowWarning(text);
		}
		
		public LiveRange GetRange(NSRange range)
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			ConcreteLiveRange clr = new ConcreteLiveRange(m_boss, range.location, range.length);
			
			TextController controller = (TextController) m_window.windowController();
			controller.RegisterRange(clr);
			
			return clr;
		}
		
		public ITextAnnotation GetAnnotation(NSRange range, AnnotationAlignment alignment)
		{
			TextController controller = (TextController) m_window.windowController();
			NSTextView view = controller.TextView;
			
			NSMutableDictionary dict = NSMutableDictionary.Create();
			dict.setObject_forKey(view, NSNib.NSNibOwner);
			
			NSMutableArray objects = NSMutableArray.Create();
			dict.setObject_forKey(objects, NSNib.NSNibTopLevelObjects);
			
			bool loaded = NSBundle.mainBundle().loadNibFile_externalNameTable_withZone(
				NSString.Create("Annotation.nib"),
				dict,
				view.zone());
			if (!loaded)
				throw new Exception("Couldn't load Annotation.nib");
			
			Annotation window = null;
			for (int i = 0; i < objects.count() && window == null; ++i)
			{
				window = objects.objectAtIndex((uint) i) as Annotation;	// the window is sometimes at index 0 and sometimes at index 1...
			}
			
			if (window != null)
				window.Init(this, view, GetRange(range), alignment);
			else
				throw new Exception("Couldn't get the Annotation window from the nib.");
			
			return window;
		}
		
		public NSRect GetBoundingBox(NSRange range)
		{
			TextController controller = (TextController) m_window.windowController();
			NSTextView view = controller.TextView;
			NSLayoutManager layout = view.layoutManager();
			
			// Get all of the rectangles in the range.
			IntPtr nr = Marshal.AllocHGlobal(4);
			IntPtr p = layout.rectArrayForCharacterRange_withinSelectedCharacterRange_inTextContainer_rectCount(
				range,													// charRange
				new NSRange(Enums.NSNotFound, 0),		// selCharRange
				view.textContainer(), 								// container
				nr);														// numRects
			uint numRects = (uint) Marshal.PtrToStructure(nr, typeof(uint));
			Marshal.FreeHGlobal(nr);
			
			// Get the union in container coordinates.
			NSRect result = NSRect.Empty;
			for (uint i = 0; i < numRects; ++i)
			{
				IntPtr ptr = new IntPtr(p.ToInt64() + 16*i);
				NSRect r = (NSRect) Marshal.PtrToStructure(ptr, typeof(NSRect));
				
				result = result.Union(r);
			}
			
			// Translate to view coordinates.
			result.origin += view.textContainerOrigin();
			
			// Translate to window coordinates.
			result = view.convertRectToBase(result);
			
			return result;
		}
		
		// IReload
		public void Reload()
		{
			Contract.Assert(!NSObject.IsNullOrNil(m_window.windowController().document()), "doc is null");
			Contract.Assert(m_window.windowController().document() is TextDocument, "doc is a " + m_window.windowController().document().GetType());
			
			var doc = (TextDocument) m_window.windowController().document();
			if (doc.HasChangedOnDisk())
			{
				if (doc.isDocumentEdited())
				{
					// Cocoa will display this sheet, but it does it when the user attempts
					// to save which is much later than we'd like.
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
		
		public int EditCount
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				return controller.EditCount;
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
		
		public void Replace(string replacement)
		{
			TextController controller = (TextController) m_window.windowController();
			NSTextView view = controller.TextView;
			
			view.setString(NSString.Create(replacement));
			
			if (!NSObject.IsNullOrNil(controller.document()))
				controller.OnPathChanged();			// bit of a hack to allow the debugger to switch languages
				
			controller.document().updateChangeCount(Enums.NSChangeCleared);
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
		
		// ITextMetrics
		public int LineCount
		{
			get
			{
				TextController controller = (TextController) m_window.windowController();
				return controller.Metrics.LineCount;
			}
		}
		
		public int GetLine(int offset)
		{
			TextController controller = (TextController) m_window.windowController();
			return controller.Metrics.GetLine(offset);
		}
		
		public int GetLineOffset(int line)
		{
			TextController controller = (TextController) m_window.windowController();
			return controller.Metrics.GetLineOffset(line);
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
		private long m_key;
		private static long ms_nextKey = 1;
		#endregion
	}
}
