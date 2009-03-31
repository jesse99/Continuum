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
		private Annotation(IntPtr obj) : base(obj)
		{
			var dict = NSMutableDictionary.Create();
			dict.setObject_forKey(NSColor.blackColor(), Externs.NSForegroundColorAttributeName);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSString fontName = defaults.stringForKey(NSString.Create("text default font name"));
			float ptSize = defaults.floatForKey(NSString.Create("text default font size"));
			if (!NSObject.IsNullOrNil(fontName))
				dict.setObject_forKey(NSFont.fontWithName_size(fontName, ptSize), Externs.NSFontAttributeName);
			else
				dict.setObject_forKey(NSFont.labelFontOfSize(12.0f), Externs.NSFontAttributeName);
			
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
		
		public void Init(ITextEditor editor, NSTextView text, LiveRange range)
		{
			m_editor = editor;
			m_text = text;
			m_range = range;
			m_view = this["view"].To<AnnotateView>();
			
			m_parent = m_text.window();
			m_parent.addChildWindow_ordered(this, Enums.NSWindowAbove);
			
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(
				this, "parentWillClose:", Externs.NSWindowWillCloseNotification, m_parent);
			
			m_text.superview().setPostsBoundsChangedNotifications(true);
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(
				this, "parentBoundsChanged:", Externs.NSViewBoundsDidChangeNotification, m_text.superview());
			
			m_range.Changed += this.DoRangeChanged;
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
			get {return m_parent != null && isVisible();}
			set
			{
				Trace.Assert(m_parent != null || !value, "m_parent is null");
				
				if (m_parent != null)
				{
					if (value != isVisible())
						if (value)
							orderFront(this);
						else
							DoClose();
				}
			}
		}
		
		public void parentWillClose(NSObject data)
		{
			DoClose();
		}
		
		public void parentBoundsChanged(NSObject data)
		{
			NSSize size = frame().size;
			DoAdjustFrame(size);
		}
		
		#region Private Methods
		private void DoRangeChanged(object sender, EventArgs e)
		{
			if (m_range.IsValid)
			{
				NSSize size = frame().size;
				DoAdjustFrame(size);
			}
			else
			{
				DoClose();
			}
		}
		
		private void DoClose()
		{
			if (m_attrs != null)
			{
				if (m_parent != null)
				{
					NSNotificationCenter.defaultCenter().removeObserver_name_object(
						this, Externs.NSWindowWillCloseNotification, m_parent);
					
					NSNotificationCenter.defaultCenter().removeObserver_name_object(
						this, Externs.NSViewFrameDidChangeNotification, m_text.superview());
					
					NSNotificationCenter.defaultCenter().removeObserver_name_object(
						this, Externs.NSViewBoundsDidChangeNotification, m_text.superview());
					
					m_text.window().removeChildWindow(this);	// need to do this or the orderOut/close affects the parent window
					m_parent = null;
				}
				
				m_attrs.release();
				close();
				autorelease();
				m_attrs = null;
			}
		}
		
		private void DoSetString(NSAttributedString value)
		{
			NSSize size = value.size();
			size.width += 2*AnnotateView.LeftMargin;
			
			DoAdjustFrame(size);
			m_view.SetText(value);
			m_view.setNeedsDisplay(true);
		}
		
		private void DoAdjustFrame(NSSize size)
		{
			// The origin is the bottom-left coordinate of the anchor character
			// which should be the top-left of our window.
			NSPoint origin = DoGetOrigin();
			origin.y -= size.height;
			NSRect rect = new NSRect(origin, size);

			// We'll allow the annonation to extend to the left or the right, but if it
			// scrolls too far up or down we'll hide it.			
			NSRect content = m_parent.contentRectForFrameRect(m_parent.frame());
			if (rect.Bottom >= content.Bottom && rect.Top < content.Top)
			{
				setFrame_display(rect, false);			// this is in screen coordinates even though we are a child window
			}
			else
			{
				rect.origin.x -= 8000;
				setFrame_display(rect, false);
			}
		}
		
		private NSPoint DoGetOrigin()
		{
			NSRect bbox = m_editor.GetBoundingBox(new NSRange(m_range.Index, m_range.Length));
			NSPoint origin = m_parent.convertBaseToScreen(bbox.origin);
			
			return origin;
		}
		#endregion
		
		#region Fields
		private ITextEditor m_editor;
		private LiveRange m_range;
		private NSWindow m_parent;
		private NSTextView m_text;
		private AnnotateView m_view;
		private NSDictionary m_attrs;
		#endregion
	}
}
