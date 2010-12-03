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

namespace Shared
{
	public static class Constants
	{
		// Unicode characters.
		public static readonly string Bullet = "\x2022";
		public static readonly string Ellipsis = "\x2026";
		public static readonly string Replacement = "\xFFFD";
		
		public static readonly string ZeroWidthSpace = "\x200B";
		public static readonly string ZeroWidthNonJoiner = "\x200C";
		public static readonly string ZeroWidthJoiner = "\x200D";

		public static readonly string ThinSpace = "\x2009";
		public static readonly string HairSpace = "\x200A";
		
		public static readonly string LeftSingleQuote = "\x2018";
		public static readonly string RightSingleQuote = "\x2019";
		public static readonly string LeftDoubleQuote = "\x201C";
		public static readonly string RightDoubleQuote = "\x201D";
		
		// Control characters.
		public static readonly string BOM = "\xFEFF";			// Cocoa considers this a control character
		public static readonly string Escape = "\x001B";
		public static readonly string Delete = "\x007F";
		
		// Key codes.
		public static readonly int DeleteKey = 0x33;
		public static readonly int DownArrowKey = 0x7D;
		public static readonly int EnterKey = 0x4C;
		public static readonly int EscapeKey = 0x35;
		public static readonly int LeftArrowKey = 0x7B;
		public static readonly int RightArrowKey = 0x7C;
		public static readonly int TabKey = 0x30;
		public static readonly int UpArrowKey = 0x7E;

		public static readonly int Number0Key = 29;
		public static readonly int Number1Key = 18;
		public static readonly int Number2Key = 19;
		public static readonly int Number3Key = 20;
		public static readonly int Number4Key = 21;
		public static readonly int Number5Key = 23;
		public static readonly int Number6Key = 22;
		public static readonly int Number7Key = 26;
		public static readonly int Number8Key = 28;
		public static readonly int Number9Key = 25;
	}
}
