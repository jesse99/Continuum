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

namespace AutoComplete
{
	[ExportClass("CompletionsWindow", "NSWindow")]
	internal sealed class CompletionsWindow : NSWindow
	{
		private CompletionsWindow(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public void SetLoc(NSPoint loc)
		{
			m_loc = loc;
		}
		
		// This is called by our parent's window_willPositionSheet_usingRect method.
		public NSRect positionSheet(NSRect usingRect)
		{
			// This will center the sheet on the text we're completing. It might
			// be better to align the left side of the sheet on the text instead,
			// but this seems tricky to do.
			float x = m_loc.x;
			
			return new NSRect(
				x,						// this is the origin of the sheet in parent window coordinates
				m_loc.y - 4.0f,
				0.0f,					// this is reserved
				1.0f);				// if this is smaller then the window the sheet genies out, else it slides out
		}
		
		public new void setFrame_display(NSRect frame, bool display)
		{
			this.SuperCall("setFrame:display:", frame, display);
			NSRect current = this.frame();
			NSRect constrained = DoConstrainToScreen(current);
			if (constrained != current)
				this.SuperCall("setFrame:display:", constrained, display);
		}
		
		#region Private Methods
		// Cocoa doesn't provide a way to set the initial size of a sheet and
		// it doesn't do a very good job when the sheet is near the edge of
		// the screen so we'll try and fix things up here.
		private NSRect DoConstrainToScreen(NSRect frame)
		{
			NSRect result = frame;
			
			NSScreen s = this.screen();
			if (!NSObject.IsNullOrNil(s))
			{
				NSRect screen = s.frame();
				
				// The sheet cannot be larger than the screen.
				result.size.width = Math.Min(result.size.width, screen.size.width);
				result.size.height = Math.Min(result.size.height, screen.size.height);
				
				// The sheet's origin (aka the bottom-left corner) can't be
				// off screen.
				result.origin.x = Math.Max(result.origin.x, screen.origin.x);
				result.origin.y = Math.Max(result.origin.y, screen.origin.y);
			}
			
			return result;
		}
		#endregion
		
		#region Fields
		private NSPoint m_loc;
		#endregion
	}
}
