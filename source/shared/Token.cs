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
using System.Diagnostics;

namespace Shared
{
	[Serializable]
	public enum TokenKind
	{
		Invalid,
		Identifier,
		Keyword,
		Char,				// text will include the ' characters
		String,			// text will include the " characters, text may have new lines
		Punct,
		Other,
	}
	
	public struct Token : IEquatable<Token>
	{
		public Token(string text, int line)
		{
			Debug.Assert(text != null, "text is null");
			Debug.Assert(line >= 0, "text is negative");	// may be zero if the text is all whitespace
			
			m_text = text;
			Offset = text.Length;
			Length = 0;
			Line = line;
			Kind = TokenKind.Invalid;
		}
		
		public Token(string text, int offset, int length, int line, TokenKind kind)
		{
			Debug.Assert(text != null, "text is null");
			Debug.Assert(offset >= 0, "offset is negative");
			Debug.Assert(offset < text.Length, "offset is too large");
			Debug.Assert(length > 0, "length is not positive");
			Debug.Assert(offset + length <= text.Length, "length is too large");
			Debug.Assert(kind != TokenKind.Invalid, "kind is invalid");
			Debug.Assert(line > 0, "line is not positive");
			
			m_text = text;
			Offset = offset;
			Length = length;
			Line = line;
			Kind = kind;
		}
		
		// Index of the first character within the token. 
		public int Offset {get; private set;}
		
		// The number of characters within the token. 
		public int Length {get; private set;}
		
		// The one-based line the token started on.
		public int Line {get; private set;}
		
		public TokenKind Kind {get; private set;}
		
		public bool IsValid()
		{
			return Kind != TokenKind.Invalid;
		}
		
		public bool IsIdentifier(string name)
		{
			return Kind == TokenKind.Identifier && this == name;
		}
		
		public bool IsKeyword(string name)
		{
			return Kind == TokenKind.Keyword && this == name;
		}
		
		public bool IsPunct(string name)
		{			
			return Kind == TokenKind.Punct && this == name;
		}
		
		public string Text()
		{
			// TODO: Unforunately strings are only usually immutable (unsafe
			// code can mutate them). This means that Substring is a fairly 
			// slow operation. It would be better to use something like a StringSlice
			// struct instead.
			if (Kind != TokenKind.Invalid)
				return m_text.Substring(Offset, Length);	
			else
				return "eof";
		}
		
		public override string ToString()
		{
			return Text();
		}
		
		#region Equality Methods
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
				
			string s = obj as string;
			if (s != null)
				return this == s;
			
			if (GetType() != obj.GetType())
				return false;
		
			Token rhs = (Token) obj;
			return this == rhs;
		}
		
		public bool Equals(Token rhs)
		{
			return this == rhs;
		}
		
		public bool Equals(string rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Token lhs, Token rhs)
		{
			return lhs.Offset == rhs.Offset && lhs.Length == rhs.Length;
		}
		
		public static bool operator!=(Token lhs, Token rhs)
		{
			return !(lhs == rhs);
		}
		
		public static bool operator==(Token lhs, string rhs)
		{
			bool equals = lhs.Length == rhs.Length;
			
			for (int i = 0; i < lhs.Length && equals; ++i)
			{
				equals = lhs.m_text[lhs.Offset + i] == rhs[i];
			}
			
			return equals;
		}
		
		public static bool operator!=(Token lhs, string rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 33;
			
			unchecked
			{
				hash += 3*Offset.GetHashCode() + 7*Length.GetHashCode();
			}
			
			return hash;
		}
		#endregion
		
		#region Fields
		private readonly string m_text;
		#endregion
	}
}
