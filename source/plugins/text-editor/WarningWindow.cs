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

namespace TextEditor
{
	internal sealed class WarningWindow 
	{
		public WarningWindow() 
		{			
			// Interface Builder won't allow us to create a window with no title bar
			// so we have to create it manually.
			NSRect rect = new NSRect(0, 0, 460, 105);
			m_window = NSWindow.Alloc().initWithContentRect_styleMask_backing_defer(rect, 0, Enums.NSBackingStoreBuffered, false);
			
			m_window.setHasShadow(false);
			
			// Initialize the text attributes.
			var dict = NSMutableDictionary.Create();
			
			NSFont font = NSFont.fontWithName_size(NSString.Create("Georgia"), 64.0f);			
			dict.setObject_forKey(font, Externs.NSFontAttributeName);
			
			NSMutableParagraphStyle style = NSMutableParagraphStyle.Create();
			style.setAlignment(Enums.NSCenterTextAlignment);
			dict.setObject_forKey(style, Externs.NSParagraphStyleAttributeName);
			
			m_attrs = dict.Retain();
			
			// Initialize the background bezier.
			m_background = NSBezierPath.Create().Retain();  
			m_background.appendBezierPathWithRoundedRect_xRadius_yRadius(m_window.contentView().bounds(), 20.0f, 20.0f);
 
 			m_color = NSColor.colorWithDeviceRed_green_blue_alpha(250/255.0f, 128/255.0f, 114/255.0f, 1.0f).Retain();	

			ActiveObjects.Add(this);
		}
		
		public void Show(NSWindow parent, string text)
		{
			if ((object) m_text != null)
				m_text.release();
				
			m_text = NSString.Create(text).Retain();
			
			NSRect pframe = parent.frame();
			NSRect cframe = m_window.frame();
			NSPoint center = pframe.Center;
			m_window.setFrameTopLeftPoint(new NSPoint(center.x - cframe.size.width/2, center.y + cframe.size.height/2));
			
			m_opening = true;
			m_alpha = MinAlpha;
						
			DoAnimate();	
		}
		
		#region Private Methods -----------------------------------------------
		private void DoAnimate()
		{
			if (m_opening)
			{
				if (m_alpha == MinAlpha)
					m_window.orderFront(null);

				m_alpha += 10;
				if (m_alpha == 100)
					m_opening = false;
			}
			else
			{
				m_alpha -= 10;
				if (m_alpha == MinAlpha)
					m_window.orderOut(null);
			}

			if (m_alpha >= MinAlpha)
			{
				DoDraw();			
				NSApplication.sharedApplication().BeginInvoke(() => DoAnimate(), TimeSpan.FromMilliseconds(30));					
			}
		}
		
		private void DoDraw()
		{
			m_window.setAlphaValue(m_alpha/100.0f);
			
			NSGraphicsContext.saveGraphicsState_c();
			NSGraphicsContext context = NSGraphicsContext.graphicsContextWithWindow(m_window);
			NSGraphicsContext.setCurrentContext(context);
			
			NSRect bounds = m_window.contentView().bounds();
			
			// draw the background
			m_color.setFill();		
			m_background.fill();
			
			// draw the text			
			m_text.drawInRect_withAttributes(bounds, m_attrs);

			context.flushGraphics();
			NSGraphicsContext.restoreGraphicsState_c();
		}
		#endregion
		
		#region Fields --------------------------------------------------------
		private NSWindow m_window;
		private bool m_opening;
		private int m_alpha;
		private NSString m_text;
		private NSMutableDictionary m_attrs;
		
		private NSBezierPath m_background;
		private NSColor m_color;
		
		private const int MinAlpha = 20;
		#endregion
	}
}	