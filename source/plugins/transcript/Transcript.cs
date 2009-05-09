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
using System.Text;
using System.Threading;

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
			m_timer = new SafeTimer(o => DoFlush(), null, Timeout.Infinite, Timeout.Infinite);
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
		
		[ThreadModel(ThreadModel.Concurrent)]
		public void Write(Output type, string text)
		{
			if (text.Length > 0)
			{
				lock (m_lock)
				{
					// Note that error output causes the transcript window to be made key (which
					// will helpfully ensure that errors are seen). But when buffering this happens
					// a bit later than it otherwise would which causes an annoying interaction when
					// HandleBuildError opens windows with errors. So, to avoid this we won't buffer
					// up error output. TODO: this may bog down the UI quite a bit though if there
					// is lots of error output.
					if (m_currentType == type && m_currentText.Length < 4*1024 && type != Output.Error)
					{
						m_currentText.Append(text);
						Unused.Value = m_timer.Change(250, Timeout.Infinite);
					}
					else
					{
						if (m_currentText.Length > 0)
						{
							DoWrite(m_currentType, m_currentText.ToString());
							Unused.Value = m_timer.Change(Timeout.Infinite, Timeout.Infinite);
						}
						
						m_currentType = type;
						m_currentText = new StringBuilder(text);
					}
					m_editCount = unchecked(m_editCount + 1);
				}
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public void Write(Output type, string format, params object[] args)
		{
			Write(type, string.Format(format, args));
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public void WriteLine(Output type, string text)
		{
			Write(type, text + Environment.NewLine);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
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
		[ThreadModel(ThreadModel.Concurrent)]
		private static void DoAnimateError(NSTextView view, NSRange range)
		{
			System.Threading.Thread.Sleep(200);
			
			NSApplication.sharedApplication().BeginInvoke(() => view.showFindIndicatorForRange(range));
		}
		
		#region Private Methods
		private void DoFlush()		// threaded
		{
			lock (m_lock)
			{
				if (m_currentText.Length > 0)
					DoWrite(m_currentType, m_currentText.ToString());
					
				m_currentType = Output.Normal;
				m_currentText = new StringBuilder();
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoWrite(Output type, string text)		// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoNonthreadedWrite(type, text));
				return;
			}
			
			DoNonthreadedWrite(type, text);
		}
		
		[ThreadModel("main", ThreadModel.AllowEveryCaller)]
		private void DoNonthreadedWrite(Output type, string text)
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			if (m_controller == null)
			{
				m_controller = new TranscriptController();
				Unused.Value = m_controller.Retain();
			}
			
			m_controller.Write(type, text);
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private TranscriptController m_controller;
		private volatile int m_editCount;
		private SafeTimer m_timer;
		
		private object m_lock = new object();
			private Output m_currentType;
			private StringBuilder m_currentText = new StringBuilder();
		#endregion
	}
}
