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
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Shared
{
	[ExportClass("GetTextController", "NSWindowController", Outlets = "text")]
	internal sealed class GetTextController : NSWindowController
	{
		public GetTextController() : base(NSObject.AllocNative("GetTextController"))
		{		
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("get-text"), this);	
			Unused.Value = window().setFrameAutosaveName(NSString.Create("get-text window"));
			
			m_text = new IBOutlet<NSTextView>(this, "text");
		}
		
		public string Text
		{
			get {return m_text.Value.string_().description();}
			set {m_text.Value.setString(NSString.Create(value));}
		}
		
		public string Run()
		{
			string result = null;
			
			Unused.Value = window().makeFirstResponder(m_text.Value);

			int button = NSApplication.sharedApplication().runModalForWindow(window());
			if (button == Enums.NSOKButton)
				result = Text;
				
			return result;
		}
		
		public void pressedOK(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
			window().orderOut(this);
		}
	
		public void pressedCancel(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSCancelButton);
			window().orderOut(this);
			
			Text = string.Empty;
		}
	
		#region Fields
		private IBOutlet<NSTextView> m_text;
		#endregion
	}
}
