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

using Gear;
using MCocoa;
using System;

namespace Shared
{
	// Pops up a dialog allowing the user to enter multiple lines of text.
	public class GetText
	{
		public GetText()
		{
			Title = "Input"; 
			Text = string.Empty;
		}
		
		// Window title.
		public string Title {get; set;}
		
		// Initial text.
		public string Text {private get; set;}
						
		// Returns null on cancel.
		public string Run()
		{
			if (ms_controller == null)
				ms_controller = new GetTextController();

			NSWindow window = ms_controller.window();
			window.setTitle(NSString.Create(Title));	
			ms_controller.Text = Text;
						
			return ms_controller.Run();
		}
		
		#region Fields
		private static GetTextController ms_controller;
		#endregion
	}
}