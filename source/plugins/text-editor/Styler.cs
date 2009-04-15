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
using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace TextEditor
{
	internal class Styler : IStyler
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public virtual void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			m_timer = new SafeTimer(o => DoTimer(), null, Timeout.Infinite, Timeout.Infinite);
		}
		
		public void Apply(IComputeRuns computer, Action callback)
		{
			Contract.Requires(computer != null, "computer is null");
			Contract.Requires(callback != null, "callback is null");
			
			// This is called via a timer so sometimes it will be closed.
			if (!m_closed)
			{
				var text = m_boss.Get<IText>();
				if (text.Text.Length < 128*1024)
				{
					lock (m_mutex)
					{
						m_path = text.Boss.Get<ITextEditor>().Path;
						m_text = text.Text;
						m_edit = text.EditCount;
						m_computer = computer;
						m_callback = callback;
						
						Unused.Value = m_timer.Change(0, Timeout.Infinite);
					}
				}
				else
				{
					// We need to ensure that the callback is always called because
					// TextController uses the call as a signal that it is OK to restore the
					// scroller.
					var cachedRuns = m_boss.Get<ICachedStyleRuns>();
					cachedRuns.Reset(text.EditCount, new StyleRun[0]);
					
					NSApplication.sharedApplication().BeginInvoke(callback);
					Unused.Value = m_timer.Change(Timeout.Infinite, Timeout.Infinite);
				}
			}
		}
		
		public void Queue(IComputeRuns computer, Action callback)
		{
			Contract.Requires(computer != null, "computer is null");
			Contract.Requires(callback != null, "callback is null");
			Contract.Requires(!m_closed, "m_closed is true");
			
			lock (m_mutex)
			{
				m_path = null;
				m_text = null;
				m_edit = 0;
				m_computer = computer;
				m_callback = callback;
				
				Unused.Value = m_timer.Change(750, Timeout.Infinite);
			}
		}
		
		public void Close()
		{
			if (!m_closed)
			{
				m_timer.Dispose();
				m_timer = null;
				m_closed = true;
			}
		}
		
		#region Private Methods
		private void DoQueuedApply()
		{
			IComputeRuns computer = null;
			Action callback = null;
			
			lock (m_mutex)
			{
				if (m_text == null)				// Apply may have been called before we landed here
				{
					computer = m_computer;
					callback = m_callback;
				}
			}
			
			if (callback != null)
				Apply(computer, callback);
		}
		
		private void DoTimer()			// threaded		TODO: might be better to use a low priority thread (tho mono 2.2 doesn't support them)
		{
			string path = null;
			string text = null;
			int edit = 0;
			IComputeRuns computer = null;
			Action callback = null;
			
			lock (m_mutex)	
			{
				path = m_path;
				text = m_text;
				edit = m_edit;
				computer = m_computer;
				callback = m_callback;
			}
			
			if (text != null)
			{
				computer.ComputeRuns(m_boss, path, text, edit);
				
				if (!m_closed)
					NSApplication.sharedApplication().BeginInvoke(callback);
			}
			else if (!m_closed)
				NSApplication.sharedApplication().BeginInvoke(DoQueuedApply);
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private SafeTimer m_timer;
		private volatile bool m_closed;
		
		private object m_mutex = new object();
			private string m_path;
			private string m_text;
			private int m_edit;
			private IComputeRuns m_computer;
			private Action m_callback;
		#endregion
	}
}
