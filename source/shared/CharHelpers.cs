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

namespace Shared
{
	// Some handy character related methods.
	[ThreadModel(ThreadModel.Concurrent)]
	public static class CharHelpers
	{
		// Returns true if the character is a control character that should not
		// appear in source code.
		public static bool IsBadControl(char c)
		{
			int b = (int) c;
			
			if (b < 0x20 && b != (int) '\t' && b != (int) '\n' && b != (int) '\r')
				return true;
			
			else if (b == 0x7F)
				return true;
			
			return false;
		}
		
		public static bool IsZeroWidth(char c)
		{
			if (c == '\t')
				return false;
			
			else if (char.IsControl(c))
				return true;
			
			else if (c == Constants.ZeroWidthSpace[0] || c == Constants.ZeroWidthNonJoiner[0] || c == Constants.ZeroWidthJoiner[0])
				return true;
			
			return false;
		}
	}
}
