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
using System.Text.RegularExpressions;

namespace Shared
{
	// Pops up a dialog allowing the user to enter a single line of text.
	public class GetString
	{
		public GetString()
		{
			Title = "Input";
			Text = string.Empty;
			ValidText = @".+";
		}
		
		// Window title.
		public string Title {get; set;}
		
		// Static text next to the editbox.
		public string Label {get; set;}
		
		// Initial text.
		public string Text {private get; set;}
		
		// String form of the validator.
		public string ValidText
		{
			set {m_validator = new Regex(value);}
		}
		
		// Regex form of the validator.
		public Regex ValidRegex
		{
			set {m_validator = value;}
		}
		
		// Returns null on cancel.
		public string Run()
		{
			if (ms_controller == null)
				ms_controller = new GetStringController();
			
			NSWindow window = ms_controller.window();
			window.setTitle(NSString.Create(Title));	
			ms_controller.Text = Text;
			if (Label != null)
				ms_controller.Label = Label;
			ms_controller.Validator = m_validator;
			
			return ms_controller.Run();
		}
		
		#region Fields
		private Regex m_validator;
		private static GetStringController ms_controller;
		#endregion
	}
}
