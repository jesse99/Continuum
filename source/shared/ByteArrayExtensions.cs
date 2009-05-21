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
//using System.Diagnostics;
//using System.Linq;
using System.Text;

namespace Shared
{
	public static class ByteArrayExtensions
	{
		// Returns a hexdump -C sort of string except that unicode symbols are
		// used for control characters.
		[Pure]
		public static string ToText(this byte[] data)
		{
			var builder = new StringBuilder();
			
			int i = 0;
			while (i < data.Length)
			{
				// Offset
				builder.AppendFormat("{0:X8}\t", i);
				
				// Byte values
				for (int d = 0; d < 16 && i + d < data.Length; ++d)
				{
					builder.AppendFormat("{0:X2} ", data[i + d]);
					if (d == 7)
						builder.Append("   ");
				}
				
				// Char values
				builder.Append('\t');
				for (int j = i; j < i + 16 && j < data.Length; ++j)
				{
					if (data[j] == '\n')
						builder.AppendFormat(DownArrow);
					
					else if (data[j] == '\r')
						builder.AppendFormat(DownHookedArrow);
					
					else if (data[j] == '\t')
						builder.AppendFormat(RightArrow);
					
					else if (data[j] < 0x20 || data[j] >= 0x7f)
						builder.AppendFormat(Constants.Replacement);
					
					else
						builder.AppendFormat("{0}", (char) data[j]);
				}
				
				i += 16;
				builder.AppendLine();
			}
			
			return builder.ToString();
		}
		
		#region Fields		
		private static readonly string RightArrow = "\x2192";
		private static readonly string DownArrow = "\x2193";
		private static readonly string DownHookedArrow = "\x21A9";

		// These are more technically correct but the new-line and tab
		// symbols are really hard to read unless the font's point size
		// is very large.
//		private static readonly string NewLineSymbol = "\x2424";
//		private static readonly string ReturnSymbol = "\x23CE";
//		private static readonly string TabSymbol = "\x2409";
		#endregion
	}
}
