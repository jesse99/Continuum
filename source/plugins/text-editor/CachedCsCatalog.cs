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

using Gear;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class CachedCsCatalog : ICachedCsCatalog
	{		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public bool IsWithinComment(int offset)
		{
			return DoIsWithin(m_comments, offset);
		}
		
		public bool IsWithinString(int offset)
		{
			return DoIsWithin(m_strings, offset);
		}
		
		public void Reset(Token[] comments, Token[] strings)
		{
			Trace.Assert(comments != null, "comments is null");
			Trace.Assert(strings != null, "strings is null");
			
			// .NET guarantees that these fields are atomically set but we need
			// to ensure that the entire group is set atomically.
			lock (m_mutex)
			{
				m_comments = comments;
				m_strings = strings;
			}
		}
		
		#region Private Methods
		private bool DoIsWithin(Token[] tokens, int offset)
		{
			Token key = new Token(offset);
			int i = Array.BinarySearch(tokens, key, new CompareRanges());
			return i >= 0;
		}
		#endregion
		
		#region Private Types
		private sealed class CompareRanges : IComparer<Token>
		{
			public int Compare(Token lhs, Token rhs)
			{
				Trace.Assert(lhs.Length == 0, "lhs is not of zero length");
				
				if (lhs.Offset < rhs.Offset)
					return -1;
					
				else if (lhs.Offset > rhs.Offset + rhs.Length)
					return +1;
					
				else
					return 0;
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private object m_mutex = new object();
			private Token[] m_comments = new Token[0];
			private Token[] m_strings = new Token[0];
		#endregion
	}
}
