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
using System.Linq;

namespace Shared
{	
	public static class EnumerableExtensions
	{
#if DEBUG
		public static string ToDebugString(this System.Collections.IEnumerable list)
		{
			Trace.Assert(list != null, "list is null");
			
			var items = new List<string>();
			
			foreach (object item in list)
			{
				items.Add(item.ToString());
				
				if (items.Count > 32)
				{
					items.Add("...");
					break;
				}
			}
			
			return "[" + string.Join(", ", items.ToArray()) + "]";
		}
		
		public delegate string Converter<T>(T item);

		public static string ToDebugString<T>(this IEnumerable<T> list, Converter<T> converter)
		{
			Trace.Assert(list != null, "list is null");
			Trace.Assert(converter != null, "converter is null");
						
			var items = new List<string>();
			
			foreach (T item in list)
			{
				items.Add(converter(item));
				
				if (items.Count > 32)
				{
					items.Add("...");
					break;
				}
			}
			
			return "[" + string.Join(", ", items.ToArray()) + "]";
		}
	} 
#endif	// DEBUG
}
