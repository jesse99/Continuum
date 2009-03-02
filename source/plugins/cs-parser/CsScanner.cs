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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace CsParser
{
	internal sealed unsafe class CsScanner
	{
		public CsScanner(string text)	 : this(text, 0)
		{
		}
		
		public CsScanner(string text, int offset)
		{
			m_text = text + '\x00';		// scanning is easier and faster with a sentinel value
			m_index = offset;
			fixed (char* buffer = m_text) DoAdvance(buffer);
		}
		
		// Returns the current token. Once all the characters have been
		// consumed the token's kind will be invalid.
		public Token Token
		{
			get {return m_token;}
		}
		
		// Returns the nth token from the current token.
		public Token LookAhead(int delta)
		{
			Debug.Assert(delta >= 0, "delta is negative");
			
			int oldIndex = m_index;
			int oldLine = m_line;
			Token oldToken = m_token;
			
			fixed (char* buffer = m_text)
			{
				while (delta-- > 0 && Token.Kind != TokenKind.Invalid)
				{
					DoAdvance(buffer);
				}
			}
			
			Token token = m_token;
			m_index = oldIndex;
			m_line = oldLine;
			m_token = oldToken;
			
			return token;
		}
		
		// Skips whitespace and comments and advances to the next token.
		// Should only be called if Token is valid.
		public void Advance()
		{
			Debug.Assert(Token.Kind != TokenKind.Invalid, "can't advance past the end of the text");
		
			fixed (char* buffer = m_text) DoAdvance(buffer);
		}
		
		// Returns all of the preprocessing directives that have been encountered
		// so far. 
		public CsPreprocess[] Preprocess
		{
			get {return m_preprocess.ToArray();}
		}
		
		#region Private Methods
		// This is a bottleneck for the parser so it's important that it be fast.
		// Using a pointer should be pretty fast, but other possibilities are
		// to use Marshal.AllocHGlobal and ReadInt16 or the System.IO.UnmanagedMemoryAccessor
		// class in C# 4.0. UnmanagedMemoryStream also seems like it would work OK.
		private char Current
		{
			get {return m_buffer[m_index];}	// unsafe code so that we can avoid range checkes
		}
		
		private char Next
		{
			get {return m_buffer[m_index + 1];}
		}
		
		private void DoAdvance(char* buffer)
		{
			m_buffer = buffer;
			
			// skip whitespace and comments
			while (true)
			{
				if (char.IsWhiteSpace(Current))
					DoSkipWhiteSpace();
				else if (Current == '/' && Next == '/')
					DoSkipSingleLineComment();
				else if (Current == '/' && Next == '*')
					DoSkipDelimitedComment();
				else
					break;
			}
			
			// identifier
			if (DoIsLetter(Current) || Current == '_')
			{
				DoScanIdentifier();
			}
			else if (Current == '@' && (DoIsLetter(Next) || Next == '_'))
			{
				++m_index;
				DoScanIdentifier();
			}
			
			// char
			else if (Current == '\'')
			{
				DoScanChar();
			}
			
			// string 
			else if (Current == '"')
			{
				DoScanString();
			}
			
			// verbatim string
			else if (Current == '@' && Next == '"')
			{
				DoScanVerbatimString();
			}
			
			// eof
			else if (Current == '\x00')
			{
				m_token = new Token(m_text, m_token.Line);
				++m_index;
			}
			
			// catch all
			else
			{
				m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Other);
				++m_index;
			}
		}
		
		// character-literal:
		//     '   character   '
		// 
		// character:
		//     single-character
		//     simple-escape-sequence
		//     hexadecimal-escape-sequence
		//     unicode-escape-sequence
		// 
		// single-character:
		//       Any character except ' (U+0027), \ (U+005C), and new-line-character
		// 
		// simple-escape-sequence:  one of
		//     \'  \''  \\  \0  \a  \b  \f  \n  \r  \t  \v
		// 
		// hexadecimal-escape-sequence:
		//     \x   hex-digit   hex-digit?   hex-digit?   hex-digit?
		//
		// unicode-escape-sequence:
		//     \u   hex-digit   hex-digit   hex-digit   hex-digit
		//     \U   hex-digit   hex-digit   hex-digit   hex-digit   hex-digit  hex-digit   hex-digit   hex-digit
		private void DoScanChar()
		{
			int offset = m_index;
			++m_index;
			
			while (Current != '\'' && Current != '\x00')
			{
				if (Current == '\\' && Next == '\\')
					++m_index;
				else if (Current == '\\' && Next == '\'')
					++m_index;
				++m_index;
			}
			
			if (Current == '\'')
			{
				++m_index;
				m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Char);
			}
			else
				throw new CsScannerException("Expected a terminating ''' for line {0}", m_line);
		}
		
		// regular-string-literal:
		//     "   regular-string-literal-characters?   "
		// 
		// regular-string-literal-characters:
		//     regular-string-literal-character
		//     regular-string-literal-characters   regular-string-literal-character
		//     
		// regular-string-literal-character:
		//     single-regular-string-literal-character
		//     simple-escape-sequence
		//     hexadecimal-escape-sequence
		//     unicode-escape-sequence
		// 
		// single-regular-string-literal-character:
		//      Any character except " (U+0022), \ (U+005C), and new-line-character
		private void DoScanString()
		{
			int offset = m_index;
			++m_index;
			
			while (Current != '"' && Current != '\n' && Current != '\r' && Current != '\x00')
			{
				if (Current == '\\' && Next == '\\')
					++m_index;
				else if (Current == '\\' && Next == '"')
					++m_index;
				++m_index;
			}
			
			if (Current == '"')
			{
				++m_index;
				m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.String);
			}
			else
				throw new CsScannerException("Expected a terminating '\"' on line {0}", m_line);
		}
		
		// verbatim-string-literal:
		//     @"   verbatim-string-literal-characters?   "
		// 
		// verbatim-string-literal-characters:
		//     verbatim-string-literal-character
		//     verbatim-string-literal-characters   verbatim-string-literal-character
		// 
		// verbatim-string-literal-character:
		//     single-verbatim-string-literal-character
		//     quote-escape-sequence
		// 
		// single-verbatim-string-literal-character:
		//     any character except "
		// 
		// quote-escape-sequence:
		//     ""
		private void DoScanVerbatimString()
		{
			int line = m_line;
			m_index = m_index + 2;
			int offset = m_index - 1;
			
			while (Current != '\x00')
			{
				if (char.IsWhiteSpace(Current))
					DoSkipWhiteSpace();
				else if (Current == '\\' && Next == '"')
					m_index = m_index + 2;
				else if (Current == '"' && Next == '"')
					m_index = m_index + 2;
				else if (Current == '"')
					break;
				else
					++m_index;
			}
			
			if (Current == '"')
			{
				++m_index;
				m_token = new Token(m_text, offset, m_index - offset, line, TokenKind.String);
			}
			else
				throw new CsScannerException("Expected a terminating '\"' for line {0}", m_line);
		}
		
		// identifier:
		//      available-identifier
		//      @   identifier-or-keyword
		// 
		// available-identifier:
		//      An identifier-or-keyword that is not a keyword
		// 
		// identifier-or-keyword:
		//      identifier-start-character   identifier-part-characters?
		// 
		// identifier-start-character:
		//      letter-character
		//      _ (the underscore character U+005F)
		// 
		// identifier-part-characters:
		//      identifier-part-character
		//      identifier-part-characters  identifier-part-character
		private void DoScanIdentifier()
		{
			int offset = m_index;
			
			while (DoIsIdentifierPartChar())	
			{
				++m_index;
			}
			
			m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Identifier);
		}
		
		// letter-character:
		//       A Unicode character of class Lu, Ll, Lt, Lm, Lo, or Nl
		//       A unicode-escape-sequence representing a character of class Lu, Ll, Lt, Lm, Lo, or Nl
		private bool DoIsLetter(char ch)
		{
			if (char.IsLetter(ch))			// fast path
				return true;
				
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			switch (cat)
			{
				case UnicodeCategory.UppercaseLetter:
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
				case UnicodeCategory.LetterNumber:
					return true;
			}
			
			return false;
		}
		
		// identifier-part-character:
		//      letter-character
		//      decimal-digit-character
		//      connecting-character
		//      combining-character
		//      formatting-character
		//
		// decimal-digit-character:
		//     A Unicode character of the class Nd
		//     A unicode-escape-sequence representing a character of class Nd
		// 
		// connecting-character:
		//     A Unicode character of the class Pc
		//     A unicode-escape-sequence representing a character of class Pc
		// 
		// combining-character:
		//     A Unicode character of class Mn or Mc
		//     A unicode-escape-sequence representing a character of class Mn or Mc
		// 
		// formatting-character:
		//     A Unicode character of the class Cf
		//     A unicode-escape-sequence representing a character of class Cf
		private bool DoIsIdentifierPartChar()
		{
			if (char.IsLetterOrDigit(Current) || Current == '_')	// fast path
				return true;
			
			if (Current == '\\' && (Next == 'u' || Next == 'U'))
				throw new CsScannerException("Line {0} has a unicode escape in an identifier which the parser does not support.", m_line);
				
			UnicodeCategory cat = char.GetUnicodeCategory(Current);
			switch (cat)
			{
				case UnicodeCategory.DecimalDigitNumber:
				case UnicodeCategory.ConnectorPunctuation:
				case UnicodeCategory.NonSpacingMark:
				case UnicodeCategory.SpacingCombiningMark:
				case UnicodeCategory.Format:
					return true;
			}
			
			return false;
		}
		
		// whitespace:
		//     Any character with Unicode class Zs
		//     Horizontal tab character (U+0009)
		//     Vertical tab character (U+000B)
		//     Form feed character (U+000C)
		private void DoSkipWhiteSpace()
		{
			while (true)
			{
				if (Current == '\n' && Next == '\r')
				{
					++m_line;
					m_index += 2;
				}
				else if (Current == '\n' || Current == '\r')
				{
					++m_line;
					++m_index;
				}
				else if (Current == '\t' || Current == '\x0B' || Current == '\x0C')
				{
					++m_index;
				}
				else if (char.GetUnicodeCategory(Current) == UnicodeCategory.SpaceSeparator)
				{
					++m_index;
				}
				else if (Current == '#')
				{
					if (!DoSkipPreprocessor())
						break;
				}
				else
					break;
			}
		}
		
		// '#'   whitespace?   name   body  pp-new-line		
		private bool DoSkipPreprocessor()
		{
			// '#'
			int oldIndex = m_index++;
			
			// whitespace?
			while (Current == '\t' || Current == '\x0B' || Current == '\x0C' || char.GetUnicodeCategory(Current) == UnicodeCategory.SpaceSeparator)
			{
				++m_index;
			}
			
			// name
			int offset = m_index;
			while (DoIsIdentifierPartChar())	
			{
				++m_index;
			}
			
			bool matched = true;
			string name = m_text.Substring(offset, m_index - offset);
			switch (name)
			{
				case "define":
				case "undef":
				case "if":
				case "elif":
				case "else":
				case "endif":
				case "line":
				case "error":
				case "warning":
				case "region":
				case "endregion":
				case "pragma":
					// body  pp-new-line
					int bodyIndex = m_index;
					while (Current != '\n' && Current != '\r' && Current != '\x00')
					{
						++m_index;
					}

					string body = m_text.Substring(bodyIndex, m_index - bodyIndex);
					m_preprocess.Add(new CsPreprocess(name, body, oldIndex, m_index - oldIndex, m_line));
					break;
				
				default:
					m_index = oldIndex;
					matched = false;
					break;
			}
			
			return matched;
		}
		
		// single-line-comment:
		//      //   input-characters?
		// 
		// input-characters:
		//     input-character
		//     input-characters   input-character
		// 
		// input-character:
		//     Any Unicode character except a new-line-character
		private void DoSkipSingleLineComment()
		{
			while (Current != '\n' && Current != '\r' && Current != '\x00')
			{
				++m_index;
			}
		}
		
		// delimited-comment:
		//      /*   delimited-comment-text?   asterisks   /
		// 
		// delimited-comment-text:
		//      delimited-comment-section
		//      delimited-comment-text   delimited-comment-section
		// 
		// delimited-comment-section:
		//      /
		//      asterisks?   not-slash-or-asterisk
		// 
		// asterisks:
		//      *
		//      asterisks   *
		// 
		// not-slash-or-asterisk:
		//     Any Unicode character except / or *
		private void DoSkipDelimitedComment()
		{
			int line = m_line;
			while (m_index < m_text.Length - 1)
			{
				if (char.IsWhiteSpace(Current))
				{
					DoSkipWhiteSpace();
				}
				else if (Current == '*' && Next == '/')
				{
					m_index = m_index + 2;
					return;
				}
				else
				{
					++m_index;
				}
			}
			
			throw new CsScannerException("Expected a terminating '*/' for line {0}", line);
		}
		#endregion
		
		#region Fields 
		private string m_text;
		private char* m_buffer;
		private int m_index;
		private int m_line = 1;
		private Token m_token;
		private List<CsPreprocess> m_preprocess = new List<CsPreprocess>();
		#endregion
	}
}
