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
using System.Collections.Generic;
using System.Diagnostics;

namespace Transcript
{
	[ExportClass("TranscriptController", "NSWindowController", Outlets = "output")]
	internal sealed class TranscriptController : NSWindowController, IObserver
	{			
		public TranscriptController() : base("TranscriptController", "transcript")
		{
			m_output = new IBOutlet<NSTextView>(this, "output");
			
			Unused.Value = window().setFrameAutosaveName(NSString.Create("transcript window"));
			m_output.Value.textStorage().setAttributedString(NSAttributedString.Create());
			
			foreach (string name in new[]{"transcript command font changed", "transcript stdout font changed", "transcript stderr font changed"})
			{
				Broadcaster.Register(name, this);
				m_attributes.Add(name, NSMutableDictionary.Create().Retain());
				DoUpdateFont(name, null);
			}
			
			Broadcaster.Register("transcript color changed", this);
			DoUpdateBackgroundColor(string.Empty, null);
			
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			if (name == "transcript color changed")
				DoUpdateBackgroundColor(name, value);
			else
				DoUpdateFont(name, value);
		}
		
		public NSTextView TextView
		{
			get {return m_output.Value;}
		}
		
		public bool validateMenuItem(NSMenuItem item)
		{
			Selector sel = item.action();
			
			if (sel.Name == "openSelection:")
			{
				NSRange range = m_output.Value.selectedRange();
				return range.length > 0;
			}
			else if (sel.Name == "dirHandler:")
			{
				NSWindow window = DoGetDirEditor();
				if (window != null)
					return window.windowController().Call("validateUserInterfaceItem:", item).To<bool>();
				else
					return false;
			}
			else if (respondsToSelector(sel))
			{
				return true;
			}
			else if (SuperCall("respondsToSelector:", new Selector("validateMenuItem:")).To<bool>())
			{
				return SuperCall("validateMenuItem:", item).To<bool>();
			}
			
			return false;
		}

		public void dirHandler(NSObject sender)
		{			
			NSWindow window = DoGetDirEditor();
			if (window != null)
				Unused.Value = window.windowController().Call("dirHandler:", sender);
		}

		public void clearTranscript(NSObject sender)
		{			
			Unused.Value = sender;
			
			m_output.Value.textStorage().mutableString().setString(NSString.Empty);
		}

		public void openSelection(NSObject sender)
		{			
			Unused.Value = sender;
			
			NSRange range = m_output.Value.selectedRange();
			if (range.length > 0)
			{
				string text = m_output.Value.textStorage().ToString();
				
				Boss boss = ObjectModel.Create("TextEditorPlugin");
				var opener = boss.Get<IOpenSelection>();
				
				int loc = range.location, len = range.length;
				if (opener.Open(text, ref loc, ref len))
					m_output.Value.setSelectedRange(new NSRange(loc, len));
			}
		}

		public void Write(Output type, string text)
		{
			switch (type)
			{
				case Output.Normal:
					m_output.Value.textStorage().appendAttributedString(NSAttributedString.Create(text, m_attributes["transcript stdout font changed"]));
					break;
					
				case Output.Command:
					m_output.Value.textStorage().appendAttributedString(NSAttributedString.Create(text, m_attributes["transcript command font changed"]));
					break;
					
				case Output.Error:
					window().makeKeyAndOrderFront(this);
					m_output.Value.textStorage().appendAttributedString(NSAttributedString.Create(text, m_attributes["transcript stderr font changed"]));
					break;
					
				default:
					Contract.Assert(false, "bad type");
					break;
			}
			
			DoOverflowCheck();
			
			int len = (int) m_output.Value.string_().length();
			NSRange range = new NSRange(len, 0);
			m_output.Value.scrollRangeToVisible(range);
		}
		
		#region Private Methods
		private NSWindow DoGetDirEditor()
		{
			NSWindow result = null;
			
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(null);
			
			if (boss != null)
			{
				var window = boss.Get<IWindow>();
				result = window.Window;
			}
			
			return result;
		}
		
		private void DoUpdateFont(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string key = name.Substring(0, name.Length - "changed".Length);
			
			m_attributes[name].removeAllObjects();
			
			// font
			NSString fname = defaults.stringForKey(NSString.Create(key + "name"));
			float ptSize = defaults.floatForKey(NSString.Create(key + "size"));
			
			NSFont font = NSFont.fontWithName_size(fname, ptSize);
			m_attributes[name].setObject_forKey(font, Externs.NSFontAttributeName);
			
			// attributes
			var data = defaults.objectForKey(NSString.Create(key + "attributes")).To<NSData>();
			if (!NSObject.IsNullOrNil(data))
			{
				NSDictionary attributes = NSUnarchiver.unarchiveObjectWithData(data).To<NSDictionary>();
				m_attributes[name].addEntriesFromDictionary(attributes);
			}
			
			// name
			m_attributes[name].setObject_forKey(NSString.Create(name), NSString.Create("style name"));
			
			// update text	
			DoUpdateAttributes();
		}
		
		private void DoUpdateAttributes()
		{
			NSTextStorage storage = m_output.Value.textStorage();
			storage.beginEditing();
			
			int index = 0;
			while (index < storage.length())
			{
				NSRange range;
				NSDictionary attrs = storage.attributesAtIndex_effectiveRange((uint) index, out range);
				if (!NSObject.IsNullOrNil(attrs))
				{
					string name = attrs.objectForKey(NSString.Create("style name")).To<NSObject>().description();
					storage.setAttributes_range(m_attributes[name], range);
				}
				
				index = range.location + range.length;
			}
			
			storage.endEditing();
		}
		
		private void DoUpdateBackgroundColor(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create("transcript color")).To<NSData>();
			var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
			
			m_output.Value.setBackgroundColor(color);
		}
		
		private void DoOverflowCheck()
		{
			int len = (int) m_output.Value.string_().length();
			if (len > MaxCharacters)
			{
				int overflow = (len - MaxCharacters) + ShrinkBy;
				
				NSTextStorage storage = m_output.Value.textStorage();
				NSRange range = new NSRange(0, overflow);
				storage.deleteCharactersInRange(range);
			}
		}
		#endregion
		
		#region Fields
		private const int MaxCharacters = 128*1024;
		private const int ShrinkBy = 8*1024;
		
		private IBOutlet<NSTextView> m_output;
		private Dictionary<string, NSMutableDictionary> m_attributes = new Dictionary<string, NSMutableDictionary>();
		#endregion
	}
}
