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
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	[ExportClass("TextEditorScrollView", "NSScrollView", Outlets = "decsPopup lineLabel")]
	internal sealed class TextEditorScrollView : NSScrollView
	{
		public TextEditorScrollView(IntPtr instance) : base(instance)
		{
			m_lineLabel = new IBOutlet<NSButton>(this, "lineLabel");
			m_decPopup = new IBOutlet<NSPopUpButton>(this, "decsPopup");
			
			NSFont font = NSFont.systemFontOfSize(NSFont.smallSystemFontSize());
			m_lineLabel.Value.setFont(font);
			m_decPopup.Value.setFont(font);
			
			ActiveObjects.Add(this);
		}
		
		// This is where the subviews of the scroll view are laid out. We override it
		// to make room for some widgets next to the horz scroller.
		public new void tile()
		{
			SuperCall(NSScrollView.Class, "tile");
			
			NSScroller horzScroller = horizontalScroller();
			NSRect horzFrame = horzScroller.frame();
			
			// Adjust the line label widget.
			NSRect localFrame = m_lineLabel.Value.superview().convertRect_fromView(horzFrame, this);
			localFrame.size.width = m_lineLabel.Value.frame().size.width;
			
			horzFrame.origin.x += localFrame.size.width;
			horzFrame.size.width -= localFrame.size.width;
			
			m_lineLabel.Value.setFrame(localFrame);
			
			// Adjust the declarations popup widget.
			localFrame = m_decPopup.Value.superview().convertRect_fromView(horzFrame, this);
			localFrame.size.width = m_decPopup.Value.frame().size.width;
			
			horzFrame.origin.x += localFrame.size.width;
			horzFrame.size.width -= localFrame.size.width;
			
			m_decPopup.Value.setFrame(localFrame);
			
			// Adjust the horizontal scrollbar.
			horzScroller.setFrame(horzFrame);
		}
		
		#region Fields
		private IBOutlet<NSButton> m_lineLabel;
		private IBOutlet<NSPopUpButton> m_decPopup;
		#endregion
	}
}
