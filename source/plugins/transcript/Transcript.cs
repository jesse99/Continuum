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

namespace Transcript
{
	internal sealed class Transcript : ITranscript, IText, IFactoryPrefs
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			// command
			dict.setObject_forKey(NSString.Create("Verdana-Bold"), NSString.Create("transcript command font name"));
			dict.setObject_forKey(NSNumber.Create(12.0f), NSString.Create("transcript command font size"));
			
			var attrs = NSDictionary.dictionaryWithObject_forKey(NSColor.blackColor(), Externs.NSForegroundColorAttributeName);
			NSData data = NSArchiver.archivedDataWithRootObject(attrs);
			dict.setObject_forKey(data, NSString.Create("transcript command font attributes"));	// need to explicitly add this so that revert has something to revert to
			
			// stdout
			dict.setObject_forKey(NSString.Create("Verdana"), NSString.Create("transcript stdout font name"));
			dict.setObject_forKey(NSNumber.Create(12.0f), NSString.Create("transcript stdout font size"));
			
			attrs = NSDictionary.dictionaryWithObject_forKey(NSColor.blackColor(), Externs.NSForegroundColorAttributeName);
			data = NSArchiver.archivedDataWithRootObject(attrs);
			dict.setObject_forKey(data, NSString.Create("transcript stdout font attributes"));
			
			// stderr
			dict.setObject_forKey(NSString.Create("Verdana"), NSString.Create("transcript stderr font name"));
			dict.setObject_forKey(NSNumber.Create(12.0f), NSString.Create("transcript stderr font size"));
			
			attrs = NSDictionary.dictionaryWithObject_forKey(NSColor.redColor(), Externs.NSForegroundColorAttributeName);
			data = NSArchiver.archivedDataWithRootObject(attrs);
			dict.setObject_forKey(data, NSString.Create("transcript stderr font attributes"));
			
			// background color
			NSColor color = NSColor.colorWithDeviceWhite_alpha(0.98f, 1.0f);
			data = NSArchiver.archivedDataWithRootObject(color);
			dict.setObject_forKey(data, NSString.Create("transcript color"));
		}
		
		public bool Visible
		{
			get {return m_controller != null  && m_controller.window().isVisible();}
		}
		
		public void Show()
		{
			if (m_controller == null)
			{
				m_controller = new TranscriptController();
				Unused.Value = m_controller.Retain();
			}
			
			m_controller.window().makeKeyAndOrderFront(null);
		}
		
		public void Write(Output type, string text)		// Write methods need to be thread safe
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => Write(type, text));
				return;
			}
			
			if (m_controller == null)
			{
				m_controller = new TranscriptController();
				Unused.Value = m_controller.Retain();
			}
			
			m_controller.Write(type, text);
			m_editCount = unchecked(m_editCount + 1);
		}
		
		public void Write(Output type, string format, params object[] args)
		{
			Write(type, string.Format(format, args));
		}
		
		public void WriteLine(Output type, string text)
		{
			Write(type, text + Environment.NewLine);
		}
		
		public void WriteLine(Output type, string format, params object[] args)
		{
			Write(type, string.Format(format, args) + Environment.NewLine);
		}
		
		public string Text
		{
			get
			{
				if (m_controller == null || !m_controller.window().isVisible())
					return string.Empty;
					
				NSTextView view = m_controller.TextView;
				return view.textStorage().ToString();
			}
		}
		
		public int EditCount
		{
			get {return m_editCount;}
		}
		
		public void Replace(string replacement, int index, int length, string undoText)
		{
			NSTextView view = m_controller.TextView;
			
			NSRange range = new NSRange(index, length);
			NSString str = NSString.Create(replacement);
			if (view.shouldChangeTextInRange_replacementString(range, str))
			{
				view.replaceCharactersInRange_withString(range, str);
				
				NSUndoManager undo = view.undoManager();
				undo.setActionName(NSString.Create(undoText));
				m_editCount = unchecked(m_editCount + 1);
			}
		}
		
		public NSRange Selection
		{
			get
			{
				if (m_controller == null || !m_controller.window().isVisible())
					return new NSRange(0, 0);
					
				NSTextView view = m_controller.TextView;
				return view.selectedRange();
			}
			
			set
			{
				NSTextView view = m_controller.TextView;
				
				view.setSelectedRange(value);
			}
		}
		
		public void ShowSelection()
		{
			NSTextView view = m_controller.TextView;
			NSRange range = view.selectedRange();
			
			view.scrollRangeToVisible(range);
			
			System.Threading.Thread thread = new System.Threading.Thread(() => DoAnimateError(view, range));
			thread.Name = "show selection animation";
			thread.Start();
		}
		
		// This is retarded, but showFindIndicatorForRange only works if the window is aleady visible
		// and the indicator doesn't always show up if we simply use BeginInvoke.
		private static void DoAnimateError(NSTextView view, NSRange range)
		{
			System.Threading.Thread.Sleep(200);
			
			NSApplication.sharedApplication().BeginInvoke(() => view.showFindIndicatorForRange(range));
		}
		
		#region Fields 
		private Boss m_boss;
		private TranscriptController m_controller;
		private int m_editCount;
		#endregion
	}
}
