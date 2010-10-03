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
using Shared;
using System;
using System.Diagnostics;

namespace AutoComplete
{
	internal static class AutoCompleteHelpers
	{
		// Returns the 1-based argument the insertionPoint is within. If the method
		// is generic this may return a negative 1-based index for the generic argument
		// instead. Returns 0 if an argument couuld not be found.
		public static int GetArgIndex(string text, int anchorLoc, int anchorLen, int insertionPoint)
		{
			Contract.Requires(!string.IsNullOrEmpty(text), "text is null or empty");
			Contract.Requires(anchorLoc >= 0, "anchorLoc is negative");
			Contract.Requires(anchorLen > 0, "anchorLen is not positive");
			Contract.Requires(anchorLoc + anchorLen <= text.Length, "anchorLen is too big");
			Contract.Requires(insertionPoint < text.Length, "insertionPoint is too large");
			
			int arg = 0;
			
			int delta = insertionPoint - (anchorLoc + anchorLen);
			int start = text.IndexOfAny(new char[]{'(', '<'}, anchorLoc, anchorLen);
			if (delta >= 0 && delta < 2048 && start > 0)			// quick sanity check
			{
				if (text[start] == '<')
				{
					int lastAngle = TextHelpers.SkipBraces(text, start, insertionPoint, "<>");
					if (lastAngle == insertionPoint || insertionPoint < lastAngle)
					{
						arg = DoGetGenericIndex(start, insertionPoint, text);
						start = -1;
					}
					else
					{
						start = text.IndexOf('(', anchorLoc, insertionPoint - anchorLoc);
					}
				}
				
				if (start > 0 && start <= insertionPoint)
					arg = DoGetRegularIndex(start, insertionPoint, text);
			}
			
			return arg;
		}
		
		#region Private Methods
		private static int DoGetRegularIndex(int start, int insertionPoint, string text)
		{
			int arg = 1;
			int i = start + 1;
			string[] braces = new string[]{"()", "[]", "{}"};
			while (i < insertionPoint)
			{
				if (Array.Exists(braces, b => text[i] == b[0]))
				{
					i = TextHelpers.SkipBraces(text, i, insertionPoint, braces);
					if (i == insertionPoint)
					{
						return 0;
					}
					++i;
				}
				else if (text[i] == '<')
				{
					int k = TextHelpers.SkipBraces(text, i, insertionPoint, "<>");
					if (k < insertionPoint || text[k] == '>')
						i = k;
					else
						++i;				// presumably the < was a less than instead of a generic brace
				}
				else if (text[i] == ')')
				{
					return 0;
				}
				else
				{
					if (text[i] == ',')
					{
						++arg;
					}
					++i;
				}
			}
			
			return arg;
		}
		
		private static int DoGetGenericIndex(int start, int insertionPoint, string text)
		{
			int arg = -1;
			int i = start + 1;
			string[] braces = new string[]{"()", "[]", "{}"};
			while (i < insertionPoint)
			{
				if (Array.Exists(braces, b => text[i] == b[0]))
				{
					i = TextHelpers.SkipBraces(text, i, insertionPoint, braces);
					if (i == insertionPoint)
						return 0;
					++i;
				}
				else if (text[i] == '<')
				{
					int k = TextHelpers.SkipBraces(text, i, insertionPoint, "<>");
					if (k < insertionPoint || text[k] == '>')
						i = k;
					else
						++i;				// presumably the < was a less than instead of a generic brace
				}
				else if (text[i] == '>')
				{
					return 0;
				}
				else
				{
					if (text[i] == ',')
						--arg;
					++i;
				}
			}
			
			return arg;
		}
		#endregion
	}
}
