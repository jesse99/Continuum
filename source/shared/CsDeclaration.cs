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

using System;
using System.Diagnostics;

namespace Shared
{
	// Base class for classes representing things like classes and methods. Note that we
	// use a Cs prefix instead of a Cs namespace because we have a number of types
	// like Enum which we can't use without causing confusion with System types and
	// can't use the obvious alternative EnumType without causing confusion with the
	// refactor types.
	public abstract class CsDeclaration
	{
		protected CsDeclaration(int offset, int length, int line)	
		{
			Debug.Assert(offset >= 0, "offset is negative");
			Debug.Assert(length >= 0, "length is negative");
			Debug.Assert(line > 0, "length is not positive");
			
			Offset = offset;
			Length = length;
			Line = line;
		}
		
		// Index of the start of the declaration.
		public int Offset {get; private set;}
		
		// The number of characters within the declaration.
		public int Length {get; private set;}
		
		// The one-based line the declaration started on.
		public int Line {get; private set;}
	} 
}
