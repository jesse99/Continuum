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
using System.Diagnostics;
using System.Collections.Generic;

namespace AutoComplete
{
	[ExportClass("TargetWindow", "NSObject")]
	internal sealed class TargetWindow : NSObject
	{
		~TargetWindow()
		{
			if (m_text != null)
				m_text.release();
				
			if (m_window != null)
				m_window.release();
		}
		
		public TargetWindow(NSTextView text, string type, string[] names) : base(NSObject.AllocNative("TargetWindow"))
		{
			m_names = names;
			m_text = text.Retain();
			
			DoCreateWindow();
			DoCreateViews(type);
			
			ActiveObjects.Add(this);
		}
		
		// TODO: what if the insertion point is at the bottom of the screen?
		public void Show()
		{
			NSApplication.sharedApplication().beginSheet_modalForWindow_modalDelegate_didEndSelector_contextInfo(
				m_window, m_text.window(), null, null, IntPtr.Zero);
		}
		
		// TODO: would be nice to clear the AutoComplete.m_window when this is called
		public void Hide()
		{
			NSApplication.sharedApplication().endSheet(m_window);
			m_window.orderOut(this);
		}
		
		#region Private Methods
		private void DoCreateWindow()
		{
			// Note that Interface Builder won't allow us to create a window with no title bar
			// so we have to do all of this manually.
			NSRect rect = new NSRect(0, 0, 460, 114);
			NSPoint loc = DoFindWindowLoc();
			m_window = CompletionsWindow.Alloc().Init(rect, loc);
			
			m_window.setHidesOnDeactivate(true);
			m_window.setHasShadow(false);
		}
		
		// TODO: this isn't quite right. We want the base line of the line the glyph is in
		// not the base line of the glyph itself. There doesn't seem to be a good way to
		// get this though so we may need to do something silly like get the glyphs for
		// several characters and use the one with the smallest bottom edge.
		private NSPoint DoFindWindowLoc()
		{
			NSRange range = m_text.selectedRange();
			
			// Get the lower left corner for the character at the selection.
			NSLayoutManager layout = m_text.layoutManager();
			uint gindex = layout.glyphIndexForCharacterAtIndex((uint) range.location);
			
			NSRange erange;
			NSRect fragmentRect = layout.lineFragmentRectForGlyphAtIndex_effectiveRange(gindex, out erange);
			NSPoint loc = fragmentRect.origin;
			
			// Translate to view coordinates.
			NSPoint origin = layout.locationForGlyphAtIndex(gindex);
			loc.x += origin.x;
			loc.y += origin.y;
			
			// Translate to window coordinates.
			loc = m_text.convertPointToBase(loc);
			
			return loc;
		}
		
		private void DoCreateViews(string type)
		{
			NSRect rect = m_window.frame();
			rect = m_window.contentRectForFrameRect(rect);
			
			// Create the table view.
			var table = CompletionsTable.Alloc().Init(rect, m_text, type, m_names, this);
			
			var col = NSTableColumn.Alloc().initWithIdentifier(NSString.Create("0")).To<NSTableColumn>();
			col.setEditable(false);
			col.headerCell().Call("setStringValue:", NSString.Create("name"));
			table.addTableColumn(col);
			col.release();
			
			// TODO: look at other table settings
			table.setHeaderView(null);
			
			// Create the scroller.
			var scroller = NSScrollView.Alloc().initWithFrame(rect).To<NSScrollView>();
			scroller.setHasHorizontalScroller(false);
			scroller.setHasVerticalScroller(true);
			scroller.setBorderType(Enums.NSLineBorder);
			scroller.setAutoresizingMask(Enums.NSViewWidthSizable | Enums.NSViewHeightSizable);
			
			// Wire the views together.
			scroller.setDocumentView(table);
			
			m_window.contentView().addSubview(scroller);
			table.sizeToFit();	
			table.release();
		}
		#endregion
		
		#region Fields
		private NSWindow m_window;
		private NSTextView m_text;
		private string[] m_names;
		#endregion
	}
}
