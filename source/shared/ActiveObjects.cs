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
using MObjc;
using MObjc.Helpers;
using System;
using System.Diagnostics;

namespace Shared
{
	// Used to detect objects which should have gone away. 
	public static class ActiveObjects
	{
		[Conditional("DEBUG")]
		public static void Add(object o)
		{
#if DEBUG
			Contract.Requires(o != null, "o is null");
			Contract.Requires(!(o is IInterface), "o is a gear object");	// use dump bosses to track these
			Contract.Requires(!(o is ValueType), "o is a struct");			// not much point in tracking structs
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			ms_objects.Add(o);
#endif
		}
		
#if DEBUG
		public static object[] Snapshot()
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			return ms_objects.Snapshot();
		}
		
		private static WeakList<object> ms_objects = new WeakList<object>(32);
#endif
	}
}
