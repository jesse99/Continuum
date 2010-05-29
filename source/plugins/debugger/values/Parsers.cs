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

//using MCocoa;
//using MObjc;
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;

namespace Debugger
{
	internal static class Parsers
	{
		public static string ParseString(string text)
		{
			var builder = new System.Text.StringBuilder(text.Length);
			
			int i = 0;
			while (i < text.Length)
			{
				char ch = text[i++];
				
				if (ch == '\\' && i + 1 < text.Length)
				{
					if (text[i] == 'n')
					{
						builder.Append('\n');
						++i;
					}
					else if (text[i] == 'r')
					{
						builder.Append('\r');
						++i;
					}
					else if (text[i] == 't')
					{
						builder.Append('\t');
						++i;
					}
					else if (text[i] == 'f')
					{
						builder.Append('\f');
						++i;
					}
					else if (text[i] == '"')
					{
						builder.Append('"');
						++i;
					}
					else if (text[i] == '\\')
					{
						builder.Append('\\');
						++i;
					}
					else if (text[i] == 'x' || text[i] == 'u')
					{
						++i;
						uint codePoint = 0;
						int count = 0;
						while (i < text.Length && DoIsHexDigit(text[i]) && count < 4)
						{
							codePoint = 16*codePoint + DoGetHexValue(text[i++]);
							++count;
						}
						builder.Append((char) codePoint);
					}
					else
					{
						builder.Append(ch);
					}
				}
				else
				{
					builder.Append(ch);
				}
			}
			
			return builder.ToString();
		}
		
		public static string ParseVerbatimString(string text)
		{
			var builder = new System.Text.StringBuilder(text.Length);
			
			int i = 0;
			while (i < text.Length)
			{
				char ch = text[i++];
				
				if (ch == '"' && i < text.Length && text[i] == '"')
				{
					builder.Append('"');
					++i;
				}
				else
				{
					builder.Append(ch);
				}
			}
			
			return builder.ToString();
		}
		
		#region Private Methods
		private static bool DoIsHexDigit(char ch)
		{
			return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
		}
		
		private static uint DoGetHexValue(char ch)
		{
			if (ch >= '0' && ch <= '9')
				return (uint) (ch - '0');
				
			else if (ch >= 'a' && ch <= 'f')
				return (uint) (ch - 'a') + 10;
			
			else
				return (uint) (ch - 'A') + 10;
		}
		#endregion
	}
}
