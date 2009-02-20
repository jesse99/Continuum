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
	[ExportClass("TextEditorScrollView", "NSScrollView", Outlets = "lineLabel")]
	internal sealed class TextEditorScrollView : NSScrollView
	{
		public TextEditorScrollView(IntPtr instance) : base(instance)
		{		
			m_lineLabel = new IBOutlet<NSView>(this, "lineLabel");

			ActiveObjects.Add(this);
		}
		  
		// This is where the subviews of the scroll view are laid out. We override it
		// to make room for some widgets next to the horz scroller.
		public new void tile()
		{
			SuperCall("tile");
			
			NSScroller hScroller = horizontalScroller();
			NSRect hFrame = hScroller.frame();
			
			NSRect lFrame = m_lineLabel.Value.superview().convertRect_fromView(hFrame, this);
			lFrame.size.width = m_lineLabel.Value.frame().size.width;
			
			hFrame.origin.x += lFrame.size.width;
			hFrame.size.width -= lFrame.size.width;
			
			m_lineLabel.Value.setFrame(lFrame);
			hScroller.setFrame(hFrame);
		}
				
		#region Fields
		private IBOutlet<NSView> m_lineLabel;
		#endregion
	}
}	