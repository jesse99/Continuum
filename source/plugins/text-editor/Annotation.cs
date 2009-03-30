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
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	[ExportClass("AnnotateView", "NSView")]
	internal sealed class AnnotateView : NSView
	{
		internal const float LeftMargin = 4.0f;
		
		private AnnotateView(IntPtr obj) : base(obj)
		{
		}
		
		public NSAttributedString GetText()
		{
			return m_text;
		}
		
		public void SetText(NSAttributedString text)
		{
			if (!NSObject.IsNullOrNil(m_text))
				m_text.release();
				
			m_text = text;
			
			if (!NSObject.IsNullOrNil(m_text))
				m_text.retain();
		}
		
		public new void drawRect(NSRect dirtyRect)
		{
			NSRect rect = bounds();
			
			NSBezierPath path = NSBezierPath.Create();
			path.appendBezierPathWithRoundedRect_xRadius_yRadius(rect, 7.0f, 7.0f);
			
			NSColor.yellowColor().setFill();
			path.fill();
			
			if (!NSObject.IsNullOrNil(m_text))
				m_text.drawAtPoint(new NSPoint(LeftMargin, 0.0f));
		}
		
		#region Fields
		private NSAttributedString m_text;
		#endregion
	}

	[ExportClass("Annotation", "NSWindow", Outlets = "view")]
	internal sealed class Annotation : NSWindow, ITextAnnotation
	{
		// TODO: finalizer removes reference?
		
		private Annotation(IntPtr obj) : base(obj)
		{
			var dict = NSMutableDictionary.Create();
			dict.setObject_forKey(NSFont.labelFontOfSize(12.0f), Externs.NSFontAttributeName);
			dict.setObject_forKey(NSColor.blackColor(), Externs.NSForegroundColorAttributeName);
			
			m_attrs = dict.Retain();
		}
		
		// We can't make a borderless window in IB so we need to use a subclass
		// so that we can still use IB to set our window up.
		public new NSWindow initWithContentRect_styleMask_backing_defer(NSRect contentRect, uint style, uint backing, bool defer)
		{
			NSWindow result = SuperCall("initWithContentRect:styleMask:backing:defer:",
				NSRect.Empty, (uint) Enums.NSBorderlessWindowMask, (uint) Enums.NSBackingStoreBuffered, false).To<NSWindow>();
			
			result.setBackgroundColor(NSColor.clearColor());
			result.setExcludedFromWindowsMenu(true);
			result.setOpaque(false);
			
			return result;
		}
		
		public void Init(NSTextView text, NSPoint origin)
		{
			m_text = text;
			m_origin = origin;
			m_view = this["view"].To<AnnotateView>();
			
			m_text.window().addChildWindow_ordered(this, Enums.NSWindowAbove);			
		}
		
		public NSColor BackColor
		{
			get {return null;}
			set {}
		}
		
		public string Text
		{
			get {return m_view.GetText().description();}
			set {DoSetString(NSAttributedString.Create(value, m_attrs));}
		}
		
		public NSAttributedString String
		{
			get {return m_view.GetText();}
			set
			{
				NSMutableAttributedString str = NSMutableAttributedString.Create();
				str.appendAttributedString(value);
				str.addAttributes_range(m_attrs, new NSRange(0, (int) str.length()));
				
				DoSetString(str);
			}
		}
		
		public bool Visible
		{
			get {return isVisible();}
			set
			{
				if (value)
				{
					orderFront(this);
				}
				else
				{
					m_text.window().removeChildWindow(this);	// need to do this or the orderOut/close affects the parent window
					m_attrs.release();
					close();
					autorelease();
				}
			}
		}
		
		#region Private Methods
		private void DoSetString(NSAttributedString value)
		{
			DoAdjustFrame(value);
			m_view.SetText(value);
			m_view.setNeedsDisplay(true);
		}
		
		private void DoAdjustFrame(NSAttributedString text)
		{
			NSSize size = text.size();
			size.width += 2*AnnotateView.LeftMargin;
			
			// The origin is the bottom-left coordinate of the anchor character
			// which should be the top-left of our window.
			NSPoint origin = new NSPoint(m_origin.x, m_origin.y - size.height);
			NSRect rect = new NSRect(origin, size);
			
			setFrame_display(rect, false);			// this is in screen coordinates even though we are a child window
		}
		#endregion
		
		#region Fields
		private NSTextView m_text;
		private AnnotateView m_view;
		private NSPoint m_origin;
		private NSDictionary m_attrs;
		#endregion
	}
}
