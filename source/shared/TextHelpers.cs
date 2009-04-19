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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	// Some handy text related methods.
	public static class TextHelpers
	{
		// This is called with start pointing to an open brace in text and braces 
		// consisting of pairs of open/close brace characters. The method will return 
		// either text.Length or the index of the character closing start. Note that
		// nested braces are skipped as well.
		public static int SkipBraces(string text, int start, params string[] braces)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(braces != null, "braces is null");

			return SkipBraces(text, start, text.Length, braces);
		}
		
		// This is called with start pointing to an open brace in text and braces 
		// consisting of pairs of open/close brace characters. The method will return 
		// either last or the index of the character closing start. Note that nested 
		// braces are skipped as well.
		public static int SkipBraces(string text, int start, int last, params string[] braces)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(start >= 0, "start is negative");
			Contract.Requires(start < last, "start is too large");
#if DEBUG
			Contract.Requires(braces.All(b => b.Length == 2), "brace strings should be two characters");
			Contract.Requires(Array.Exists(braces, b => text[start] == b[0]), "start character isn't an open brace");
#endif
			
			var openBraces = new List<char>();
			openBraces.Add(text[start]);
			
			int index = start + 1;
			while (index < last && index < text.Length && openBraces.Count > 0)
			{
				char ch = text[index];
				if (DoIsOpenBrace(ch, braces))
				{
					openBraces.Add(ch);
					++index;
				}
				else if (DoIsCloseBrace(ch, braces))
				{
					if (openBraces.Count > 0 && DoClosesBrace(openBraces.Last(), ch, braces))
					{
						openBraces.RemoveLast();
						if (openBraces.Count > 0)
							++index;
					}
					else
					{
						index = last;			// mismatched close brace so we have to give up
					}
				}
				else
					++index;
			}
			
			return openBraces.Count == 0 ? index : last;
		}
		
		// This is called with start pointing to an close brace in text and braces 
		// consisting of pairs of open/close brace characters. The method will return 
		// either -1 or the index of the character opening start. Note that nested
		// braces are skipped as well.
		public static int ReverseSkipBraces(string text, int start, params string[] braces)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(braces != null, "braces is null");
			
			return ReverseSkipBraces(text, start, text.Length, braces);
		}
		
		// This is called with start pointing to an close brace in text and braces 
		// consisting of pairs of open/close brace characters. The method will return 
		// either first or the index of the character opening start. Note that nested
		// braces are skipped as well.
		public static int ReverseSkipBraces(string text, int start, int first, params string[] braces)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(start >= 0, "start is negative");
			Contract.Requires(start > first, "start is too small");
#if DEBUG
			Contract.Requires(braces.All(b => b.Length == 2), "brace strings should be two characters");
			Contract.Requires(Array.Exists(braces, b => text[start] == b[1]), "start character isn't a close brace");
#endif
			
			var closeBraces = new List<char>();
			closeBraces.Add(text[start]);
			
			int index = start - 1;
			while (index > first && index > 0 && closeBraces.Count > 0)
			{
				char ch = text[index];
				if (DoIsCloseBrace(ch, braces))
				{
					closeBraces.Add(ch);
					--index;
				}
				else if (DoIsOpenBrace(ch, braces))
				{
					if (closeBraces.Count > 0 && DoClosesBrace(ch, closeBraces.Last(), braces))
					{
						closeBraces.RemoveLast();
						--index;
					}
					else
					{
						index = first;			// mismatched open brace so we have to give up
					}
				}
				else
					--index;
			}
			
			return closeBraces.Count == 0 ? index : first;
		}
		
		#region Private Methods
		private static bool DoIsOpenBrace(char ch, string[] braces)
		{
			return Array.Exists(braces, b => ch == b[0]);
		}
		
		private static bool DoIsCloseBrace(char ch, string[] braces)
		{
			return Array.Exists(braces, b => ch == b[1]);
		}
		
		private static bool DoClosesBrace(char open, char close, string[] braces)
		{
			return Array.Exists(braces, b => open == b[0] && close == b[1]);
		}
		#endregion
	}
}
