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
using System;
using System.Diagnostics;

namespace AutoComplete
{
	[ExportClass("CompletionsWindow", "NSWindow")]
	internal sealed class CompletionsWindow : NSWindow
	{
		private CompletionsWindow(IntPtr instance) : base(instance)
		{
		}
		
		public void SetLoc(NSPoint loc)
		{
			m_loc = loc;
		}
				
		// This is called by our parent's window_willPositionSheet_usingRect.
		public NSRect positionSheet(NSRect usingRect)
		{
			// TODO: this isn't quite right.
			float x = usingRect.size.width/2.0f;
			
			return new NSRect(
				x,					// this is the origin of the sheet in parent window coordinates
				m_loc.y,
				0.0f,				// this is reserved
				1.0f);			// if this is smaller then the window the sheet genies out, else it slides out
		}
		
		#region Fields
		private NSPoint m_loc;
		#endregion
	}
}
