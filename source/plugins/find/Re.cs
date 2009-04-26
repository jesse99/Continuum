// Copyright (C) 2007-2008 Jesse Jones
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

using Shared;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Find
{
	internal sealed class Re
	{
		public bool UseRegex {get; set;}
		public bool CaseSensitive {get; set;}
		public bool MatchWords {get; set;}
		
		public string WithinText {get; set;}
		
		// TODO: the MatchWords option can be a bit annoying because it is sticky
		// and won't properly match things like "//". One fix would be to only use 
		// \b if the character next to it was a word but we'd have to account for
		// whitespace in the pattern, things like '(' or '[', and maybe escape sequences.
		public Regex Make(string pattern)
		{
			RegexOptions options = 0;
			if (UseRegex)
				options |= RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline;
			if (!CaseSensitive)
				options |= RegexOptions.IgnoreCase;
				
			if (!UseRegex)
				pattern = Escape(pattern);
			if (MatchWords)
				pattern = @"\b" + pattern + @"\b";
			if (WithinText != null && WithinText.Length > 0 && WithinText != Constants.Ellipsis)
				pattern = DoSearchWithin(pattern, WithinText, ref options);
				
			return new Regex(pattern, options);
		}
		
		public static string Escape(string pattern)
		{
			StringBuilder builder = new StringBuilder(pattern.Length);
			
			foreach (char ch in pattern)
			{
				switch (ch)
				{
					case '.':
					case '$':
					case '^':
					case '{':
					case '}':
					case '[':
					case ']':
					case '(':
					case ')':
					case '|':
					case '*':
					case '+':
					case '?':
					case '#':
					case ' ':		// need this because our regex ignores whitespace
						builder.Append('\\');
						break;
				}
				
				builder.Append(ch);
			}
			
			return builder.ToString();
		}
		
		#region Private Methods
		private string DoSearchWithin(string pattern, string within, ref RegexOptions options)
		{
			int i = within.IndexOf(Constants.Ellipsis[0]);
			if (i < 0)
				throw new ArgumentException("Within text is missing an ellipsis.");
				
			string prefix = Escape(within.Substring(0, i));
			string suffix = Escape(within.Substring(i + 1));
			
			var result = new StringBuilder();
			
			if (prefix.Length > 0)
			{
				// zero width match of prefix
				result.AppendFormat("(?<=(?:{0})", prefix);
				
				// match zero or more characters, but not the suffix
				if (suffix.Length > 0 && prefix != suffix)
					result.AppendFormat("(?:.(?!{0}))*?)", suffix);
				else
					result.Append(".*?)");
			}
			
			// match the user's re
			result.Append(pattern);
			
			if (suffix.Length > 0)
			{
				// match zero or more characters, but not the prefix
				if (prefix.Length > 0 && prefix != suffix)
					result.AppendFormat("(?=(?:.(?!{0}))*?", prefix);
				else
					result.Append("(?=.*?");
				
				// zero width match the suffix
				result.AppendFormat("{0})", suffix);
			}
			
			// If we can distinguish between the prefix and the suffix then allow
			// . to match new lines.
			if (prefix.Length > 0 && suffix.Length > 0 && prefix != suffix)
				options |= RegexOptions.Singleline;
			
			return result.ToString();
		}
		#endregion
	}
}
