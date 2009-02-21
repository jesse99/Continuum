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

using Gear;
//using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

#if false
namespace Styler
{
	public abstract class Styler
	{
		protected Styler()
		{
			m_timer = new Timer((o) => DoTimer(), null, Timeout.Infinite, Timeout.Infinite);
		}
		
		// Asynchronously computes the style runs and calls the callback
		// on the main thread when finished. The callback will be called
		// with the edit count. The runs will cover the text and the caller 
		// can assume ownership of runs.
		public void Apply(string text, int edit, Action<int, List<StyleRun>> callback)
		{
			Trace.Assert(text != null, "text is null");
			Trace.Assert(callback != null, "callback is null");
			
			if (text.Length < 128*1024)
			{
				lock (m_mutex)
				{
					m_text = text;
					m_edit = edit;
					m_data = null;
					m_callback = callback;
					
					Unused.Value = m_timer.Change(0, Timeout.Infinite);
				}
			}
			else
			{
				// We need to ensure that the callback is always called because
				// TextController uses the call as a signal that it is OK to restore the
				// scroller.
				var runs = new List<StyleRun>();
				NSApplication.sharedApplication().BeginInvoke(() => callback(edit, runs));
			}
		}
		
		// Like the above except data is called to get the text and edit
		// count (on the main thread) and there is a delay before data
		// is called. Queue can be called multiple times and any queue
		// requests which have not yet been processed are dropped.
		public void Queue(Func<Tuple2<string, int>> data, Action<int, List<StyleRun>> callback)
		{
			Trace.Assert(data != null, "data is null");
			Trace.Assert(callback != null, "callback is null");
			
			lock (m_mutex)
			{
				m_text = null;
				m_edit = 0;
				m_data = data;
				m_callback = callback;
				
				Unused.Value = m_timer.Change(750, Timeout.Infinite);
			}
		}
						
		#region Protected Methods
		protected abstract void OnComputeRuns(string text, int edit, List<StyleRun> runs);		// threaded
		#endregion
		
		#region Private Methods
		private void DoQueuedApply()
		{
			string text = null;
			int edit = 0;
			Action<int, List<StyleRun>> callback = null;
			
			lock (m_mutex)
			{
				if (m_data != null)				// Apply may have been called before we landed here
				{
					var data = m_data();
					text = data.First;
					edit = data.Second;
					callback = m_callback;
				}
			}
			
			if (text != null)
				Apply(text, edit, callback);
		}
		
		private void DoTimer()			// threaded		TODO: might be better to use a low priority thread (tho mono 2.2 doesn't support them)
		{
			string text = null;
			int edit = 0;
			Action<int, List<StyleRun>> callback = null;
			
			lock (m_mutex)	
			{
				text = m_text;
				edit = m_edit;
				callback = m_callback;
			}
			
			if (text != null)
			{
				var runs = new List<StyleRun>(text.Length/50);
				OnComputeRuns(text, edit, runs);
				
				NSApplication.sharedApplication().BeginInvoke(() => callback(edit, runs));
			}
			else
				NSApplication.sharedApplication().BeginInvoke(DoQueuedApply);
		}
		#endregion
		
		#region Fields 
		private Timer m_timer;
		
		private object m_mutex = new object();
			private string m_text;
			private int m_edit;
			private Func<Tuple2<string, int>> m_data;
			private Action<int, List<StyleRun>> m_callback;
		#endregion
	}
}
#endif
