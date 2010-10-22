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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace App
{
	[ExportClass("IgnoreExceptionController", "NSWindowController", Outlets = "text")]
	internal sealed class IgnoreExceptionController : NSWindowController
	{
		public IgnoreExceptionController() : base(NSObject.AllocAndInitInstance("IgnoreExceptionController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("ignore-exception"), this);
			
			Boss boss = ObjectModel.Create("Application");
			var exceptions = boss.Get<IExceptions>();
			
			m_text = new IBOutlet<NSTextView>(this, "text").Value;
			m_text.insertText(NSString.Create(string.Join("\n", exceptions.Ignored)));
			window().makeKeyAndOrderFront(this);
		}
		
		public void pressedOK(NSObject sender)
		{
			string text = m_text.textStorage().ToString();
			string[] types = text.Split(new char[]{' ', '\t', '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
			string badType = DoValidate(types);
			if (badType == null)
			{
				Boss boss = ObjectModel.Create("Application");
				var exceptions = boss.Get<IExceptions>();
				exceptions.Ignored = types;
				
				NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
				window().orderOut(this);
				window().release();
			}
			else
			{
				Functions.NSBeep();
				int i = text.IndexOf(badType);
				m_text.setSelectedRange(new NSRange(i, badType.Length));
			}
		}
		
		public void pressedCancel(NSObject sender)
		{
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSCancelButton);
			window().orderOut(this);
			window().release();
		}
		
		#region Private Methods
		private string DoValidate(string[] types)
		{
			foreach (string type in types)
			{
				if (!DoValidate(type))
					return type;
			}
			
			return null;
		}
		
		private bool DoValidate(string type)
		{
			Contract.Requires(type.Length > 0);
			
			// Can't start or end with a period.
			if (type[0] == '.' || type[type.Length - 1] == '.')
				return false;
				
			// All characters must be letters, numbers, underscores, or periods.
			foreach (char ch in type)
			{
				if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.' && ch != ':')
					return false;
			}
			
			return true;
		}
		#endregion
		
		#region Fields
		private NSTextView m_text;
		#endregion
	}
}
