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
	// Note that arrays implement IList.
	public static class ListExtensions
	{
		public static void AddIfMissing<T>(this List<T> data, T value)
		{
			Trace.Assert(data != null, "data is null");

			if (data.IndexOf(value) < 0)
				data.Add(value);
		}
		
		// Returns true if the lengths of the two lists are equal and each element
		// is equal.
		public static bool EqualValues<T>(this IList<T> lhs, IList<T> rhs)
		{
			bool equal = false;
			
			if (Equals(lhs, rhs))
			{
				equal = true;		// if they are both null or both the same instance then return true
			}
			else if (lhs != null && rhs != null)
			{
				equal = lhs.Count == rhs.Count;
				for (int i = 0; i < lhs.Count && equal; ++i)
					equal = Equals(lhs[i], rhs[i]);
			}
			
			return equal;
		}
		
		public static T Pop<T>(this IList<T> data)
		{
			Trace.Assert(data != null, "data is null");
			
			T result = data[data.Count - 1];
			data.RemoveAt(data.Count - 1);
			
			return result;
		}
		
		public static void RemoveLast<T>(this IList<T> data)
		{
			Trace.Assert(data != null, "data is null");
			
			data.RemoveAt(data.Count - 1);
		}
		
		// This uses the insertion sort algorithm which is simple to implement
		// and has very good performance if the data is already mostly sorted.
		public static void StableSort<T>(this IList<T> data) where T : IComparable<T>
		{
			Trace.Assert(data != null, "data is null");
			
			for (int i = 1; i <= data.Count - 1; ++i)
			{
				T value = data[i];
				
				int j = i - 1;
				while (j >= 0 && data[j].CompareTo(value) > 0)
				{
					data[j + 1] = data[j];
					--j;
				}
				
				data[j + 1] = value;
			}
		}
		
		public static void StableSort<T>(this IList<T> data, Func<T, T, int> compare)
		{
			Trace.Assert(data != null, "data is null");
			Trace.Assert(compare != null, "compare is null");
			
			for (int i = 1; i <= data.Count - 1; ++i)
			{
				T value = data[i];
				
				int j = i - 1;
				while (j >= 0 && compare(data[j], value) > 0)
				{
					data[j + 1] = data[j];
					--j;
				}

				data[j + 1] = value;
			}
		}
	}
}
