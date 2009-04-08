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
	[ExportClass("CompletionsController", "NSWindowController", Outlets = "table label")]
	internal sealed class CompletionsController : NSWindowController
	{
		public CompletionsController() : base(NSObject.AllocNative("CompletionsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("completions"), this);
			Unused.Value = window().setFrameAutosaveName(NSString.Create("auto-complete window"));
			
			m_table = new IBOutlet<CompletionsTable>(this, "table");
			m_label = new IBOutlet<NSTextField>(this, "label");
			
			ActiveObjects.Add(this);
		}
		
		public void Show(ITextEditor editor, NSTextView text, string type, Member[] names, int prefixLen, bool isInstance, bool isStatic)
		{
			var wind = (CompletionsWindow) window();
			NSPoint loc = editor.GetBoundingBox(text.selectedRange()).origin;
			wind.SetLoc(loc);
			
			string defaultLabel = type;
			if (isInstance && !isStatic)
				defaultLabel += " Members";
			else if (isInstance)
				defaultLabel += " Instance Members";
			else if (isStatic)
				defaultLabel += " Static Members";
			
			m_label.Value.setStringValue(NSString.Create(defaultLabel));
			m_table.Value.Open(type, editor, text, names, prefixLen, m_label.Value, defaultLabel);
			Log.WriteLine("AutoComplete", "took {0:0.000} secs to open the window", AutoComplete.Watch.ElapsedMilliseconds/1000.0);
			
			NSApplication.sharedApplication().beginSheet_modalForWindow_modalDelegate_didEndSelector_contextInfo(
				wind, text.window(), null, null, IntPtr.Zero);
		}
		
		public void hide()
		{
			NSApplication.sharedApplication().endSheet(window());
			window().orderOut(this);
		}
		
		public void updateLabel(NSObject text)
		{
			m_label.Value.setObjectValue(text);
		}
		
		#region Fields
		private IBOutlet<CompletionsTable> m_table;
		private IBOutlet<NSTextField> m_label;
		#endregion
	}
}
