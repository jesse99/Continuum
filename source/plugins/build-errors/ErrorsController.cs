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
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BuildErrors	
{
	[ExportClass("ErrorsTextView", "NSTextView")]
	internal sealed class ErrorsTextView : NSTextView
	{
		private ErrorsTextView(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public new void mouseDown(NSEvent evt)
		{
			if (evt.clickCount() == 2)
			{
				Boss boss = ObjectModel.Create("Application");
				var handle = boss.Get<IHandleBuildError>();
				handle.ShowCurrent();
			}
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{			
			NSMenu menu = NSMenu.Create();
			
			Boss boss = ObjectModel.Create("Application");
			var errors = boss.Get<IBuildErrors>();
			
			for (int i = 0; i < errors.Count; ++i)
			{
				BuildError error = errors.Get(i);
				
				string title = (i + 1) + ": " + error.Message;
				NSMenuItem item = NSMenuItem.Alloc().initWithTitle_action_keyEquivalent(NSString.Create(title), "showError:", NSString.Empty);
				item.autorelease();
				item.setTag(i);
				
				menu.addItem(item);
			}
			
			return menu;
		}
	}

	[ExportClass("ErrorsController", "NSWindowController", Outlets = "textView")]
	internal sealed class ErrorsController : NSWindowController, IObserver
	{
		public ErrorsController() : base("ErrorsController", "build-errors")
		{
			m_textView = new IBOutlet<NSTextView>(this, "textView");
			
			Unused.Value = window().setFrameAutosaveName(NSString.Create("build-errors window"));
			Unused.Value = window().Call("setBecomesKeyOnlyIfNeeded:", true);
			
			m_textView.Value.setEditable(false);
			
			Broadcaster.Register("errors font changed", this);
			Broadcaster.Register("errors color changed", this);
			
			m_defaultStyle = NSMutableDictionary.Create().Retain();
			DoUpdateFont(string.Empty, null);
			DoUpdateBackgroundColor(string.Empty, null);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "errors font changed":
					DoUpdateFont(name, value);
					break;
					
				case "errors color changed":
					DoUpdateBackgroundColor(name, value);
					break;
					
				default:
					Trace.Fail("bad name: " + name);
					break;
			}
		}
		
		public void Clear()
		{
			window().orderOut(this);
			m_textView.Value.textStorage().setAttributedString(NSAttributedString.Create(string.Empty, m_defaultStyle));
		}
		
		public void Set(string message, int index, int count)
		{
			m_textView.Value.textStorage().setAttributedString(NSAttributedString.Create(message, m_defaultStyle));
			window().setTitle(NSString.Create("Error " + (index + 1) + " of " + count));
			window().orderFront(this);
		}
		
		public void showError(NSObject sender)
		{
			int tag = (int) sender.Call("tag");
			
			Boss boss = ObjectModel.Create("Application");
			var errors = boss.Get<IBuildErrors>();
			errors.Show(tag);
		}
		
		#region Private Methods
		private void DoUpdateFont(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			m_defaultStyle.removeAllObjects();
			
			// font
			NSString fname = defaults.stringForKey(NSString.Create("errors font name"));
			float ptSize = defaults.floatForKey(NSString.Create("errors font size"));
			
			NSFont font = NSFont.fontWithName_size(fname, ptSize);
			m_defaultStyle.setObject_forKey(font, Externs.NSFontAttributeName);
			
			// attributes
			var data = defaults.objectForKey(NSString.Create("errors font attributes")).To<NSData>();
			if (!NSObject.IsNullOrNil(data))
			{
				NSDictionary attributes = NSUnarchiver.unarchiveObjectWithData(data).To<NSDictionary>();
				m_defaultStyle.addEntriesFromDictionary(attributes);
			}
			
			// update text
			NSTextStorage storage = m_textView.Value.textStorage();
			storage.setAttributes_range(m_defaultStyle, new NSRange(0, (int) storage.length()));
		}
		
		private void DoUpdateBackgroundColor(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create("errors color")).To<NSData>();
			var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
			
			m_textView.Value.setBackgroundColor(color);
		}
		#endregion
		
		#region Fields
		private IBOutlet<NSTextView> m_textView;
		private NSMutableDictionary m_defaultStyle;
		#endregion
	}
}	