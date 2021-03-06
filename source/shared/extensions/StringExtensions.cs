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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public static class StringExtensions
	{
		[Pure]
		public static int Count(this string s, char c)
		{
			return s.Count(d => d == c);
		}
		
		[Pure]
		public static bool IsNullOrWhiteSpace(this string s)	// .NET 4.0 method
		{
			if (string.IsNullOrEmpty(s))
				return true;
			
			foreach (char ch in s)
			{
				if (!char.IsWhiteSpace(ch))
					return false;
			}
			
			return true;
		}
		
		// Escapes control characters and high ASCII.
		[Pure]
		public static string EscapeAll(this string s)
		{
			Contract.Requires(s != null, "s is null");
			
			var builder = new StringBuilder(s.Length);
			
			foreach (char ch in s)
			{
				if (ch == '\n')
					builder.Append("\\n");
				
				else if (ch == '\r')
					builder.Append("\\r");
				
				else if (ch == '\t')
					builder.Append("\\t");
				
				else if (ch == '"')
					builder.Append("\\\"");
				
				else if (ch < ' ')
					builder.AppendFormat("\\x{0:X2}", (int) ch);
				
				else if (ch > '~')
					builder.AppendFormat("\\x{0:X4}", (int) ch);
				
				else
					builder.Append(ch);
			}
			
			return builder.ToString();
		}
		
		// Return the str with characters in chars replaced by value.
		[Pure]
		public static string ReplaceChars(this string str, string chars, string value)
		{
			Contract.Requires(str != null);
			Contract.Requires(chars != null);
			Contract.Requires(value != null);
			
			var builder = new System.Text.StringBuilder(str.Length);
			
			for (int i = 0; i < str.Length; ++i)
			{
				if (chars.Contains(str[i]))
					builder.Append(value);
				else
					builder.Append(str[i]);
			}
			
			return builder.ToString();
		}
		
		[Pure]
		public static string ReversePath(this string path)
		{
			Contract.Requires(path != null);
			
			// Returns "baz • bar • foo" for "/foo/bar/baz".
			string[] parts = path.Split(new char[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
			Array.Reverse(parts);
			return string.Join(Constants.ThinSpace + Constants.Bullet + Constants.ThinSpace, parts);
		}
		
		// Removes all whitespace from the string.
		[Pure]
		public static string TrimAll(this string s)
		{
			Contract.Requires(s != null, "s is null");
			
			string result;
			
			int index = s.IndexOfAny(ms_whitespace);
			if (index >= 0)
				result = DoTrimAll(s, index);
			else
				result = s;			// we don't want to build a new string for the common case of no whitespace
			
			return result;
		}
		
		#region Private Methods		
		private static string DoTrimAll(string s, int startAt)
		{
			StringBuilder buffer = new StringBuilder(s.Length);
			
			for (int i = 0; i < startAt; ++i)
				buffer.Append(s[i]);
			
			for (int i = startAt; i < s.Length; ++i)
				if (!ms_whitespace.Contains(s[i]))
					buffer.Append(s[i]);
			
			return buffer.ToString();
		}
		#endregion
		
		#region Fields		
		private static char[] ms_whitespace = new char[]{' ', '\t', '\n', '\r'};
		#endregion
	}
}
