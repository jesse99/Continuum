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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Shared
{
	// This should generally only be used with static observers. Note that a 
	// strong reference to the trampoline must be retained and if the trampoline
	// is unregistered the instance used must be identical to the one used when
	// registering.
	public sealed class ObserverTrampoline : IObserver
	{
		public ObserverTrampoline(Action<string, object> callback)
		{
			Contract.Requires(callback != null, "callback is null");
			
			m_callback = callback;
		}
		
		public void OnBroadcast(string name, object value)
		{
			m_callback(name, value);
		}
		
		private Action<string, object> m_callback;
	}
	
	// Note that we don't use C# events because we want to keep broadcasters and
	// observers as decoupled as possible. And we don't use NSNotificationCenter
	// because we'd like to be able to include arbitrary data in the broadcast: not
	// just cocoa types. And we use an interface instead of a delegate because that
	// allows us to use a WeakReference which means that clients don't need to
	// explicitly unregister themselves (it can be painful to do that correctly and 
	// mistakes often lead to hard to find leaks).
	public static class Broadcaster
	{
		public static void Register(string name, IObserver observer)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(observer != null, "observer is null");
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			List<WeakReference> observers;
			if (!ms_observers.TryGetValue(name, out observers))
			{
				observers = new List<WeakReference>();
				ms_observers.Add(name, observers);
			}
			
			if (!observers.Exists(o => object.ReferenceEquals(observer, o.Target)))
				observers.Add(new WeakReference(observer));
		}
		
		// Note that this generally does not have to be called.
		public static void Unregister(IObserver observer)
		{
			Contract.Requires(observer != null, "observer is null");
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			foreach (List<WeakReference> candidates in ms_observers.Values)
			{
				candidates.RemoveAll(c => object.ReferenceEquals(observer, c.Target));
			}
		}
		
		public static void Invoke(string name, object value)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
		
			List<WeakReference> observers;
			if (ms_observers.TryGetValue(name, out observers))
			{
				for (int i = observers.Count - 1; i >= 0; --i)
				{
					IObserver observer = observers[i].Target as IObserver;
					if (observer != null)
						observer.OnBroadcast(name, value);
					else
						observers.RemoveAt(i);
				}
			}
		}
		
		private static Dictionary<string, List<WeakReference>> ms_observers = new Dictionary<string, List<WeakReference>>();
	}
}
