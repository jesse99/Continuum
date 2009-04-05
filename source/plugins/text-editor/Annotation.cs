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
		
		~AnnotateView()
		{
			if (!NSObject.IsNullOrNil(m_text))
				m_text.release();
			
			if (!NSObject.IsNullOrNil(m_color))
				m_color.release();
		}
		
		private AnnotateView(IntPtr obj) : base(obj)
		{
			m_color = NSColor.whiteColor().Retain();
		}
		
		public NSAttributedString GetText()
		{
			return m_text;
		}
		
		public void SetText(NSAttributedString text)
		{
			if (text != m_text)
			{
				if (!NSObject.IsNullOrNil(m_text))
					m_text.release();
					
				m_text = text;
				
				if (!NSObject.IsNullOrNil(m_text))
					m_text.retain();
			}
		}
		
		public NSColor BackColor
		{
			get {return m_color;}
			set
			{
				Trace.Assert(!NSObject.IsNullOrNil(value), "value is null or nil");
				
				if (value != m_color)
				{
					m_color.release();
					m_color = value;
					m_color.retain();
				}
			}
		}
		
		public new void drawRect(NSRect dirtyRect)
		{
			NSRect rect = bounds();
			
			NSBezierPath path = NSBezierPath.Create();
			path.appendBezierPathWithRoundedRect_xRadius_yRadius(rect, 7.0f, 7.0f);
			
			m_color.setFill();
			path.fill();
			
			if (!NSObject.IsNullOrNil(m_text))
			{
				// TODO: may need to use NSLineBreakByWordWrapping once we support
				// multi-line annotations
				int options = Enums.NSStringDrawingUsesLineFragmentOrigin | Enums.NSStringDrawingDisableScreenFontSubstitution;
				NSRect r = new NSRect(rect.Left + LeftMargin, rect.Bottom + LeftMargin, rect.size.width - 2*LeftMargin, rect.size.height);
				m_text.drawWithRect_options(r, options);
			}
		}
		
		#region Fields
		private NSAttributedString m_text;
		private NSColor m_color;
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
			NSFont font;
			if (!NSObject.IsNullOrNil(fontName))
				font = NSFont.fontWithName_size(fontName, ptSize);
			else
				font = NSFont.labelFontOfSize(12.0f);
			dict.setObject_forKey(font, Externs.NSFontAttributeName);
			
			m_fontHeight = font.ascender() + font.descender();
			
			m_attrs = dict.Retain();
		}
		
		// We can't make a borderless window in IB so we need to use a subclass
		// so that we can still use IB to set our window up.
		public new NSWindow initWithContentRect_styleMask_backing_defer(NSRect contentRect, uint style, uint backing, bool defer)
		{
			NSWindow result = SuperCall("initWithContentRect:styleMask:backing:defer:",
				NSRect.Empty, (uint) Enums.NSBorderlessWindowMask, (uint) Enums.NSBackingStoreBuffered, false).To<NSWindow>();
			
			result.setAcceptsMouseMovedEvents(true);
			result.setBackgroundColor(NSColor.clearColor());
			result.setDelegate(result);
			result.setExcludedFromWindowsMenu(true);
			result.setOpaque(false);
			result.setMovableByWindowBackground(true);
			
			return result;
		}
		
		public void Init(ITextEditor editor, NSTextView text, LiveRange range)
		{
			m_editor = editor;
			m_text = text;
			m_range = range;
			m_view = this["view"].To<AnnotateView>();
			
			m_parent = m_text.window();
			
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(
				this, "parentWillClose:", Externs.NSWindowWillCloseNotification, m_parent);
			
			m_text.superview().setPostsBoundsChangedNotifications(true);
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(
				this, "parentBoundsChanged:", Externs.NSViewBoundsDidChangeNotification, m_text.superview());
			
			m_range.Changed += this.DoRangeChanged;
		}
		
		// This is lame but the normal cursor handling goo doesn't work with child windows...
		public new void mouseMoved(NSEvent theEvent)
		{
			NSCursor.openHandCursor().set();
		}
		
		public bool IsValid
		{
			get {return m_parent != null && m_range.IsValid;}
		}
		
		public NSRange Anchor
		{
			get
			{
				Trace.Assert(IsValid, "anchor is invalid");
				return new NSRange(m_range.Index, m_range.Length);
			}
		}
		
		public NSColor BackColor
		{
			get {return m_view.BackColor;}
			set {m_view.BackColor = value; m_view.setNeedsDisplay(true);}
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
			get {return m_parent != null && m_visible;}
			set
			{
				Trace.Assert(m_parent != null || !value, "m_parent is null");
				
				if (m_parent != null)
				{
					// Note that we have to maintain this extra state and muck with add/removeChild
					// because orderIn and orderOut affect the parent window instead of the child
					// window if we don't...
					if (value != m_visible)
					{
						if (value)
						{
							m_parent.addChildWindow_ordered(this, Enums.NSWindowAbove);
							orderFront(this);
						}
						else
						{
							m_text.window().removeChildWindow(this);
							orderOut(this);
						}
						m_visible = value;
					}
				}
			}
		}
		
		public void Close()
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
					
					if (Visible)
						Visible = false;
					m_parent = null;
				}
				
				m_attrs.release();
				close();
				autorelease();
				m_attrs = null;
			}
		}
		
		public void parentWillClose(NSObject data)
		{
			Close();
		}
		
		public void parentBoundsChanged(NSObject data)
		{
			NSSize size = frame().size;
			DoAdjustFrame(size);
		}
		
		public new void mouseDown(NSEvent theEvent)
		{
			m_text.showFindIndicatorForRange(Anchor);
			SuperCall("mouseDown:", theEvent);
		}
		
		public new void mouseUp(NSEvent theEvent)
		{
			NSRect currentFrame = frame();
			NSRect content = m_parent.contentRectForFrameRect(m_parent.frame());
			if (content.Intersects(currentFrame))
			{
				NSRect baseFrame = DoGetFrame(currentFrame.size);
				m_offset = currentFrame.origin - baseFrame.origin;
			
				SuperCall("mouseUp:", theEvent);
			}
			else
			{
				Close();
				
				NSPoint centerPt = currentFrame.Center;
				Functions.NSShowAnimationEffect(Enums.NSAnimationEffectPoof, centerPt);
			}
		}
		
		#region Private Methods
		private void DoRangeChanged(object sender, EventArgs e)
		{
			if (m_parent != null)
			{
				if (m_range.IsValid)
				{
					NSSize size = frame().size;
					DoAdjustFrame(size);
				}
				else
				{
					Close();
				}
			}
		}
		
		private void DoSetString(NSAttributedString value)
		{
			// Unfortunately size is returning a value that is a bit too large so the 
			// text isn't vertically centered when it is drawn. Not sure why this is,
			// maybe it's adding in leading, but the leading() method on our font
			// returns 0.0 so we can't use that to try to fix it up...
			NSSize size = value.size();
			size.width += 2*AnnotateView.LeftMargin;
			size.height = 2*AnnotateView.LeftMargin + m_fontHeight;
			
			DoAdjustFrame(size);
			m_view.SetText(value);
			m_view.setNeedsDisplay(true);
		}
		
		private void DoAdjustFrame(NSSize size)
		{
			NSRect frame = DoGetFrame(size);
			frame.origin += m_offset;
			
			// We'll allow the annotation to extend to the left or the right, but if it
			// scrolls too far up or down we'll hide it.			
			NSRect content = m_parent.contentRectForFrameRect(m_parent.frame());
			if (frame.Bottom >= content.Bottom && frame.Top < content.Top)
			{
				setFrame_display(frame, false);			// this is in screen coordinates even though we are a child window
			}
			else
			{
				frame.origin.x -= 8000;
				setFrame_display(frame, false);
			}
		}
		
		private NSRect DoGetFrame(NSSize size)
		{
			NSRect bbox = m_editor.GetBoundingBox(new NSRange(m_range.Index, m_range.Length));
			NSPoint origin = m_parent.convertBaseToScreen(bbox.origin);
			
			// The origin is the bottom-left coordinate of the anchor character
			// which should be the top-left of our window.
			origin.y -= size.height;
			NSRect frame = new NSRect(origin, size);
			
			return frame;
		}
		#endregion
		
		#region Fields
		private ITextEditor m_editor;
		private LiveRange m_range;
		private NSWindow m_parent;
		private NSTextView m_text;
		private AnnotateView m_view;
		private NSDictionary m_attrs;
		private bool m_visible;
		private NSPoint m_offset;
		private float m_fontHeight;
		#endregion
	}
}
