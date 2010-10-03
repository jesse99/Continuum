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

using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace TextEditor
{
	internal sealed class TextMetrics
	{
		public TextMetrics(string text)
		{
			Reset(text);
			
			ActiveObjects.Add(this);
		}
		
		public void Reset(string text)
		{	
			m_text = text;
			DoComputeLineStarts();
		}
		
		public int LineCount
		{
			get {return m_lineOffsets.Count;}
		}
		
		// Returns 1-based line number.
		public int GetLine(int offset)
		{
			int line = m_lineOffsets.BinarySearch(offset);
			if (line < 0)
				line = ~line;
				
			if (line == m_lineOffsets.Count || m_lineOffsets[line] != offset)
				--line;
			
//			Console.WriteLine("offset: {0}, offsets: [{1}], line: {2}", offset, string.Join(", ", (from x in m_lineOffsets select x.ToString()).ToArray()), line + 1);
			
			return line + 1;
		}
		
		public int GetLineOffset(int line)
		{
			if (line >= m_lineOffsets.Count + 1)
				return m_text.Length;
			else
				return m_lineOffsets[line - 1];
		}
		
		// Returns 1-based column number.
		public int GetCol(int offset)
		{
			int col = 1, i = Math.Min(offset, m_text.Length);
			while (i > 0 && m_text[i - 1] != '\n' && m_text[i - 1] != '\r')
			{
				++col;
				--i;
			}
			
			return col;
		}
		
		// Returns a range by extending the selection left until an open brace is found 
		// which is not closed within the range. Then the range is extended right until
		// the new brace is closed. The returned range will start and end with braces 
		// or have a zero length if it could not be balanced.
		public NSRange Balance(string text, NSRange range)
		{
			NSRange result = new NSRange(range.location, range.length);
			
			// First we need to get a list of all of the braces in the range which are not paired up.
			List<char> braces = DoFindBraces(text, range);
			
			// Then we need to expand the range to the left until we hit an open brace
			// which isn't closed within the range.
			while (result.location > 0)
			{
				result.location -= 1;
				result.length += 1;
				
				char ch = text[result.location];
				if (DoIsOpenBrace(ch))
				{
					if (braces.Count > 0 && DoClosesBrace(ch, braces[0]))
					{
						braces.RemoveAt(0);
					}
					else
					{
						braces.Insert(0, ch);
						break;
					}
				}
				else if (DoIsCloseBrace(ch))
				{
					braces.Insert(0, ch);
				}
			}
			
			// Finallly we need to expand the range right until we close the new brace.
			if (braces.Count > 0 && DoIsOpenBrace(braces[0]))
			{
				while (result.location + result.length < text.Length && braces.Count > 0)
				{
					result.length += 1;
					
					char ch = text[result.location + result.length - 1];
					if (DoIsOpenBrace(ch))
					{
						braces.Add(ch);
					}
					else if (DoIsCloseBrace(ch))
					{
						if (DoClosesBrace(braces[braces.Count - 1], ch))
							braces.RemoveAt(braces.Count - 1);
						else
							break;
					}
				}
				
				if (braces.Count != 0)
					result = new NSRange(0, 0);
			}
			else if (result.location < 0 || !result.Intersects(range) || !DoIsOpenBrace(text[result.location]))
				result = new NSRange(0, 0);
			
//			Console.WriteLine("{0} at {1} => {2}", text, range, result);
			
			return result;
		}
		
		// Returns the index to the left which balances the closing brace at index,
		// or -1 if the brace could not be closed, or -2 if the character at index is
		// not a closing brace.
		public int BalanceLeft(string text, int index)
		{
			if (index < 0 || index >= text.Length || !DoIsCloseBrace(text[index]))
				return -2;
			
			int i = index;
			var close = new List<char>();
			while (i >= 0)
			{
				if (DoIsCloseBrace(text[i]))
				{
					close.Add(text[i]);
				}	
				else if (close.Count > 0 && DoClosesBrace(text[i], close[close.Count - 1]))
				{
					close.RemoveAt(close.Count - 1);
					
					if (close.Count == 0)
						break;
				}
				else if (DoIsOpenBrace(text[i]))
				{
					break;
				}
				
				--i;
			}
			
			int result = close.Count == 0 ? i : -1;
//			Console.WriteLine("{0} at {1} => {2}", text, index, result);
			
			return result;
		}
		
		#region Private Methods
		private List<char> DoFindBraces(string text, NSRange range)
		{
			List<char> braces = new List<char>();
			
			for (int i = range.location; i < range.location + range.length; ++i)
			{
				if (DoIsOpenBrace(text[i]))
				{
					braces.Add(text[i]);
				}
				else if (DoIsCloseBrace(text[i]))
				{
					if (braces.Count > 0 && DoClosesBrace(braces[braces.Count - 1], text[i]))
						braces.RemoveAt(braces.Count - 1);
					else
						braces.Add(text[i]);
				}
			}
			
			return braces;
		}
		
		private bool DoIsOpenBrace(char ch)
		{
			return ch == '(' || ch == '[' || ch == '{';
		}
				
		private bool DoIsCloseBrace(char ch)
		{
			return ch == ')' || ch == ']' || ch == '}';
		}
		
		private bool DoClosesBrace(char open, char ch)
		{
			if (open == '(')
				return ch == ')';
			
			else if (open == '[')
				return ch == ']';
				
			else if (open == '{')
				return ch == '}';
				
			return false;
		}
		
		private void DoComputeLineStarts()
		{
			m_lineOffsets.Clear();
			
			// Add an entry for the first line.
			m_lineOffsets.Add(0);
				
			int offset = 0;
			while (offset < m_text.Length && m_text[offset] != '\r' && m_text[offset] != '\n')
			{
				++offset;
			}
			
			// Add entries for any remaining lines.
			while (offset < m_text.Length)
			{
				if (offset + 1 < m_text.Length && m_text[offset] == '\r' && m_text[offset + 1] == '\n')
				{
					offset += 2;
					if (offset <= m_text.Length)
						m_lineOffsets.Add(offset);
				}
				else if (m_text[offset] == '\r' || m_text[offset] == '\n')
				{
					++offset;
					if (offset <= m_text.Length)
						m_lineOffsets.Add(offset);
				}
				else
				{
					++offset;
				}
			}
		}
		#endregion
		
		#region Fields
		private string m_text;
		private List<int> m_lineOffsets = new List<int>();	// offset at which each (zero-based) row starts
		#endregion
	}
}	