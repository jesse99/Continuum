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

using Gear.Helpers;
using System;
using System.Diagnostics;
using System.Threading;

namespace Shared
{
	// This works a lot like System.Threading.Timer except that it does not
	// retain references to the delegate when disposed. (System.Threading.
	// ThreadPool (and therefore Time) do do this although I think the references 
	// might go away eventually. However while that may be technically correct
	// it makes finding leaks quite painful).
	public sealed class SafeTimer : IDisposable
	{
		[ThreadModel("finalizer")]
		~SafeTimer()
		{
			Dispose();
		}
		
		[ThreadModel(ThreadModel.NamedThread)]
		public delegate void Callback(object data);
		
		public SafeTimer(Callback callback)
		{
			Contract.Requires(callback != null, "callback is null");
			
			Thread thread = new Thread(this.DoThread);
			thread.Name = "SafeTimer.DoThread";
			thread.IsBackground = true;					// allow the app to quit even if the thread is still running
			thread.Start();
			
			m_callback = callback;
		}
		
		public SafeTimer(Callback callback, object state, int dueTime, int period) : this(callback)
		{
			m_state = state;
			
			Change(dueTime, period);
		}
		
		public SafeTimer(Callback callback, object state, TimeSpan dueTime, TimeSpan period) : this(callback)
		{
			m_state = state;
			
			Change(dueTime, period);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public bool Change(int dueTime, int period)
		{
			return Change(TimeSpan.FromMilliseconds(dueTime), TimeSpan.FromMilliseconds(period));
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public bool Change(TimeSpan dueTime, TimeSpan period)
		{
			Contract.Requires(dueTime.TotalMilliseconds >= -1, "bad dueTime");
			Contract.Requires(period.TotalMilliseconds >= -1, "bad period");
			
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
				
			lock (m_mutex)
			{
				m_dueTime = dueTime;
				m_period = period;
				
				Monitor.Pulse(m_mutex);
			}
			
			return true;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		public void Dispose()
		{
			if (!m_disposed)
			{
				lock (m_mutex)
				{
					m_disposed = true;
					Monitor.Pulse(m_mutex);
				}
				
				GC.SuppressFinalize(this);
			}
		}
		
		#region Private Methods
		[ThreadModel(ThreadModel.NamedThread)]
		private void DoThread()
		{
			try
			{
				while (!m_disposed)
				{
					Callback callback = null;
					
					lock (m_mutex)
					{
						bool pulsed = Monitor.Wait(m_mutex, m_dueTime);
						
						if (!pulsed)
						{
							callback = m_callback;
							m_dueTime = m_period;
						}
					}
					
					if (callback != null)
					{
						Contract.Assert(!m_disposed, "timer is disposed");
						callback(m_state);
					}
				}
			}
			catch (Exception)
			{
				// This will terminate the app. TODO: need to verify that this is what
				// System.Threading.Timer does.
				throw;
			}
		}
		#endregion
		
		#region Fields
		private readonly Callback m_callback;
		private readonly object m_state;
		private object m_mutex = new object();
			private volatile bool m_disposed;
			private TimeSpan m_dueTime = TimeSpan.FromMilliseconds(-1);
			private TimeSpan m_period = TimeSpan.FromMilliseconds(-1);
		#endregion
	}
}
