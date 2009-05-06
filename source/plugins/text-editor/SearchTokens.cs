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
using Gear.Helpers;
using MCocoa;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class SearchTokens : ISearchTokens
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
			DoUpdateCache();
			return DoIsWithin(m_comments, offset);
		}
		
		public bool IsWithinString(int offset)
		{
			DoUpdateCache();
			return DoIsWithin(m_strings, offset);
		}
		
		public NSRange GetIdentifier(int offset)
		{
			DoUpdateCache();
			
			Token key = new Token(offset);
			int i = Array.BinarySearch(m_identifiers, key, new CompareRanges());
			
			return i >= 0 ? new NSRange(m_identifiers[i].Offset, m_identifiers[i].Length) : NSRange.Empty;
		}
		
		public NSRange GetNextIdentifier(int offset)
		{
			DoUpdateCache();
			
			Token key = new Token(offset);
			int i = Array.BinarySearch(m_identifiers, key, new CompareRanges());
			if (i >= 0)
				i += 1;
			else
				i = ~i;
			
			return i < m_identifiers.Length ? new NSRange(m_identifiers[i].Offset, m_identifiers[i].Length) : NSRange.Empty;
		}
		
		public NSRange GetPreviousIdentifier(int offset)
		{
			DoUpdateCache();
			
			Token key = new Token(offset);
			int i = Array.BinarySearch(m_identifiers, key, new CompareRanges());
			if (i >= 0)
				i -= 1;
			else
				i = ~i - 1;
			
			return i >= 0 ? new NSRange(m_identifiers[i].Offset, m_identifiers[i].Length) : NSRange.Empty;
		}
		
		#region Private Methods
		private bool DoIsWithin(Token[] tokens, int offset)
		{
			Token key = new Token(offset);
			int i = Array.BinarySearch(tokens, key, new CompareRanges());
			return i >= 0;
		}
		
		private void DoUpdateCache()
		{
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
			var editor = m_boss.Get<ITextEditor>();
			var text = m_boss.Get<IText>();
			
			Parse parse = parses.Parse(editor.Path, text.EditCount, text.Text);
			if (parse.Edit != m_edit)
			{
				var strings = new List<Token>();
				var identifiers = new List<Token>();
				
				foreach (Token token in parse.Tokens)
				{
					if (token.Kind == TokenKind.String && token.Length > 2)
						strings.Add(new Token(text.Text, token.Offset + 1, token.Length - 2, token.Line, TokenKind.String));
					
					if (token.Kind == TokenKind.Identifier)
						identifiers.Add(token);
				}
				
				m_edit = parse.Edit;
				m_comments = parse.Comments;
				m_strings = strings.ToArray();
				m_identifiers = identifiers.ToArray();
			}
		}
		#endregion
		
		#region Private Types
		private sealed class CompareRanges : IComparer<Token>
		{
			public int Compare(Token lhs, Token rhs)
			{
				Contract.Requires(lhs.Length == 0, "lhs is not of zero length");
				
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
		private int m_edit = -1;
		private Token[] m_comments = new Token[0];
		private Token[] m_strings = new Token[0];
		private Token[] m_identifiers = new Token[0];
		#endregion
	}
}
