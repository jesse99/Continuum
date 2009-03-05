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

//using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace AutoComplete	
{
	[ExportClass("CompletionsController", "NSWindowController", Outlets = "table label")]
	internal sealed class CompletionsController : NSWindowController
	{
		public CompletionsController() : base(NSObject.AllocNative("CompletionsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("completions"), this);
			
			m_table = new IBOutlet<CompletionsTable>(this, "table");
			m_label = new IBOutlet<NSTextField>(this, "label");
			
//			m_table.Value.setDoubleAction("doubleClicked:");
//			m_table.Value.setTarget(this);
			
			ActiveObjects.Add(this);
		}
		
		public void Show(NSTextView text, string type, string[] names)
		{
			var wind = (CompletionsWindow) window();
			NSPoint loc = DoFindWindowLoc(text);
			wind.SetLoc(loc);
			
			m_label.Value.setStringValue(NSString.Create(type));
			m_table.Value.Open(text, names);
			
			NSApplication.sharedApplication().beginSheet_modalForWindow_modalDelegate_didEndSelector_contextInfo(
				wind, text.window(), null, null, IntPtr.Zero);
		}
		
		public void hide()
		{
			NSApplication.sharedApplication().endSheet(window());
			window().orderOut(this);
		}
				
		#region Private Methods
		// TODO: this isn't quite right. We want the base line of the line the glyph is in
		// not the base line of the glyph itself. There doesn't seem to be a good way to
		// get this though so we may need to do something silly like get the glyphs for
		// several characters and use the one with the smallest bottom edge.
		private NSPoint DoFindWindowLoc(NSTextView text)
		{
			NSRange range = text.selectedRange();
			
			// Get the lower left corner for the character at the selection.
			NSLayoutManager layout = text.layoutManager();
			uint gindex = layout.glyphIndexForCharacterAtIndex((uint) range.location);
			
			NSRange erange;
			NSRect fragmentRect = layout.lineFragmentRectForGlyphAtIndex_effectiveRange(gindex, out erange);
			NSPoint loc = fragmentRect.origin;
			
			// Translate to view coordinates.
			NSPoint origin = layout.locationForGlyphAtIndex(gindex);
			loc.x += origin.x;
			loc.y += origin.y;
			
			// Translate to window coordinates.
			loc = text.convertPointToBase(loc);
			
			return loc;
		}
		#endregion

		#region Fields
		private IBOutlet<CompletionsTable> m_table;
		private IBOutlet<NSTextField> m_label;
		#endregion
	}
}
