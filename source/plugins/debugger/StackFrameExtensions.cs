// Copyright (C) 2010 Jesse Jones
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

using Mono.Debugger.Soft;
using System;
using System.Linq;

namespace Debugger
{
	internal static class StackFrameExtensions
	{
		// Returns true if the two stack frames are the same.
		public static bool Matches(this StackFrame lhs, StackFrame rhs)
		{
			bool matches = false;
			
			if (lhs == rhs)					// this will use reference equality (as of mono 2.6)
			{
				matches = true;
			}
			else if (lhs != null && rhs != null)
			{
				if (lhs.Thread.Id == rhs.Thread.Id)			// note that Address can change after a GC
					if (lhs.Method.MetadataToken == rhs.Method.MetadataToken)
						if (lhs.Method.FullName == rhs.Method.FullName)	// this is kind of expensive, but we can't rely on just the metadata token (we need the assembly as well which we can't always get)
							matches = true;
			}
			
			return matches;
		}
	}
}
