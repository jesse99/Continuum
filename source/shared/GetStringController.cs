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
using System.Text.RegularExpressions;

namespace Shared
{
	[ExportClass("GetStringController", "NSWindowController", Outlets = "text label okButton")]
	internal sealed class GetStringController : NSWindowController
	{
		public GetStringController() : base(NSObject.AllocNative("GetStringController"))
		{		
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("get-string"), this);	
			Unused.Value = window().setFrameAutosaveName(NSString.Create("get-string window"));
			
			m_okButton = new IBOutlet<NSButton>(this, "okButton");
			
			m_text = new IBOutlet<NSTextField>(this, "text");
			m_label = new IBOutlet<NSTextField>(this, "label");
			m_text.Value.setDelegate(this);
		}
		
		public string Text
		{
			get {return m_text.Value.stringValue().description();}
			set {m_text.Value.setStringValue(NSString.Create(value));}
		}
		
		public string Label
		{
			set {m_label.Value.setStringValue(NSString.Create(value));}
		}
		
		public Regex Validator {get; set;}
		
		public string Run()
		{
			string result = null;
			
			DoUpdateButtons();
			Unused.Value = window().makeFirstResponder(m_text.Value);
			
			int button = NSApplication.sharedApplication().runModalForWindow(window());
			if (button == Enums.NSOKButton)
				result = Text;
				
			return result;
		}
		
		public void controlTextDidChange(NSNotification notification)
		{
			Unused.Value = notification;
			
			DoUpdateButtons();
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
		
		#region Private Methods
		private void DoUpdateButtons()
		{
			bool matches = Validator.IsMatch(Text);
			m_okButton.Value.setEnabled(matches);
		}
		#endregion
		
		#region Fields
		private IBOutlet<NSTextField> m_text;
		private IBOutlet<NSTextField> m_label;
		private IBOutlet<NSButton> m_okButton;
		#endregion
	}
}
