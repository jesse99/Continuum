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
    // Note that we don't use C# events because we want to decouple broadcasters
    // from observers (so we can't have plugins maintain events) and we want to
    // make broadcasting extensible without requiring changes to the shared plugin
    // (so shared cannot have the events). NSNotificationCenter is a better fit
    // but we'd like to avoid requiring observers to link with mcocoa.
	public static class Broadcaster
	{
		public static void Register(string name, object key, Action<string, object> callback)
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			Trace.Assert(key != null, "key is null");
			Trace.Assert(callback != null, "callback is null");
			
			List<Entry> callbacks;
			if (!ms_callbacks.TryGetValue(name, out callbacks))
			{
				callbacks = new List<Entry>();
				ms_callbacks.Add(name, callbacks);
			}
			
			callbacks.Add(new Entry(key, callback));
		}
		
		// Removes all callbacks that match the key (which is usually a this pointer
		// or string).
		public static void Unregister(object key)
		{
			Trace.Assert(key != null, "key is null");
			
			foreach (var callbacks in ms_callbacks.Values)
			{
				Unused.Value = callbacks.RemoveAll((e) => e.Key == key);
			}
		}
		
		public static void Invoke(string name, object value)
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");

			List<Entry> callbacks;
			if (ms_callbacks.TryGetValue(name, out callbacks))
			{
				foreach (Entry entry in callbacks)
				{
					DoInvoke(name, entry.Callback, value);
				}
			}
		}
	
		private static void DoInvoke(string name, Action<string, object> callback, object value)
		{
			try
			{
				callback(name, value);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "There was an error processing the {0} broadcast:", name);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.ToString());
			}
		}
	
		private struct Entry
		{	
			public Entry(object key, Action<string, object> callback)
			{
				Key = key;
				Callback = callback;
			}
			
			public object Key  {get; private set;}
			public Action<string, object> Callback {get; private set;}												
		}

		private static Dictionary<string, List<Entry>> ms_callbacks = new Dictionary<string, List<Entry>>();
	} 
}
