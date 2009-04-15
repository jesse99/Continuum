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
using System.Globalization;
using System.Threading;

namespace CsParser
{
	// It would be a bit cleaner if we returned TokenKind.Keyword for keywords but changing
	// the code to so do is somewhat obnoxious and doesn't buy us a whole lot since we don't
	// care if keywords are used as identifiers. Note that instances are safe to use from a thread
	// (but not from multiple threads).
	internal sealed unsafe class Scanner : IScanner
	{
		public Scanner()
		{
			m_threadID = Thread.CurrentThread.ManagedThreadId;
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(string text)
		{
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, "can only be used with one thread");
			
			Init(text, 0);
		}
		
		public void Init(string text, int offset)
		{
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, "can only be used with one thread");
			
			m_text = text + '\x00';		// scanning is easier and faster with a sentinel value
			m_index = offset;
			m_comments.Clear();
			m_tokens.Clear();
			
			fixed (char* buffer = m_text) DoAdvance(buffer);
			
			if (Token.IsValid())
				m_tokens.Add(Token);
		}
		
		public Token Token
		{
			get {return m_token;}
		}
		
		public Token LookAhead(int delta)
		{
#if DEBUG
			Contract.Requires(delta >= 0, "delta is negative");
#endif
			
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
		
		public void Advance()
		{
#if DEBUG
			Contract.Requires(Token.Kind != TokenKind.Invalid, "can't advance past the end of the text");
#endif
		
			fixed (char* buffer = m_text) DoAdvance(buffer);
			
			if (Token.IsValid())
				m_tokens.Add(Token);
		}
		
		public CsPreprocess[] Preprocess
		{
			get {return m_preprocess.ToArray();}
		}
		
		public Token[] Comments
		{
			get {return m_comments.ToArray();}
		}
		
		public Token[] Tokens
		{
			get {return m_tokens.ToArray();}
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
		
		private char NextNext
		{
			get {return m_buffer[m_index + 2];}
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
			if (CsHelpers.CanStartIdentifier(Current) || Current == '_')
			{
				DoScanIdentifier();
			}
			else if (Current == '@' && (CsHelpers.CanStartIdentifier(Next) || Next == '_'))
			{
				++m_index;
				DoScanIdentifier();
			}
			
			// number
			else if (Current == '0' && (Next == 'x' || Next == 'X'))
			{
				DoScanHexNumber();
			}
			
			else if (Current >= '0' && Current <= '9')
			{
				DoScanNumber();
			}
			
			else if (Current == '.' && Next >= '0' && Next <= '9')
			{
				++m_index;
				DoScanFloat(m_index - 1);
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
			
			// punctuation
			else if (char.IsPunctuation(Current) || char.IsSymbol(Current))
			{
				DoScanPunct();
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
		
		// hexadecimal-integer-literal:
		//     0x   hex-digits   integer-type-suffix?
		//     0X   hex-digits   integer-type-suffix?
		// 
		// integer-type-suffix:  one of
		//      U  u  L  l  UL  Ul  uL  ul  LU  Lu  lU  lu
		private void DoScanHexNumber()
		{
			int offset = m_index;
			m_index += 2;
			
			while ((Current >= '0' && Current <= '9') || (Current >= 'a' && Current <= 'f') || (Current >= 'A' && Current <= 'F'))
			{
				++m_index;
			}
			
			if (Current == 'u' || Current == 'l' || Current == 'U' || Current == 'L')
			{
				if (Next == 'u' || Next == 'l' || Next == 'U' || Next == 'L')
					m_index += 2;
				else
					m_index += 1;
			}
			
			m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Number);
		}
		
		// decimal-integer-literal:
		//     decimal-digits   integer-type-suffix?
		private void DoScanNumber()
		{
			int offset = m_index;
			
			while (Current >= '0' && Current <= '9')
			{
				++m_index;
			}
			
			if (Current == '.')
			{
				++m_index;
				DoScanFloat(offset);
			}
			else if (Current == 'e' || Current == 'E')
			{
				DoScanExponent(offset);
			}
			else
			{
				if (Current == 'u' || Current == 'l' || Current == 'U' || Current == 'L')
				{
					if (Next == 'u' || Next == 'l' || Next == 'U' || Next == 'L')
						m_index += 2;
					else
						m_index += 1;
				}
				else if (Current == 'f' || Current == 'd' || Current == 'm' || Current == 'F' || Current == 'D' || Current == 'M')
				{
					m_index += 1;
				}
				
				m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Number);
			}
		}
		
		// real-literal:
		//       decimal-digits   .   decimal-digits   exponent-part?   real-type-suffix?
		//       .   decimal-digits   exponent-part?   real-type-suffix?
		//       decimal-digits   exponent-part   real-type-suffix?
		//       decimal-digits   real-type-suffix
		private void DoScanFloat(int offset)
		{
			while (Current >= '0' && Current <= '9')
			{
				++m_index;
			}
			
			DoScanExponent(offset);
		}
		
		// exponent-part:
		//     e   sign?   decimal-digits
		//     E   sign?   decimal-digits
		// 
		// sign:  one of
		//     +  -
		// 
		// real-type-suffix: one of
		//      F   f   D    d M  m
		private void DoScanExponent(int offset)
		{
			if (Current == 'e' || Current == 'E')
			{
				m_index += 1;
				
				if (Current == '+' || Current == '-')
					m_index += 1;
					
				while (Current >= '0' && Current <= '9')
				{
					++m_index;
				}
			}
			
			if (Current == 'f' || Current == 'd' || Current == 'm' || Current == 'F' || Current == 'D' || Current == 'M')
			{
				m_index += 1;
			}
			
			m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Number);
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
			
			while (Current != '\'' && Current != '\x00' && Current != '\n' && Current != '\r')
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
			{
				throw new ScannerException("Expected a terminating ''' for line {0}", m_line);
			}
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
			{
				throw new ScannerException("Expected a terminating '\"' on line {0}", m_line);
			}
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
			{
				throw new ScannerException("Expected a terminating '\"' for line {0}", m_line);
			}
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
			
			while (CsHelpers.CanContinueIdentifier(Current))	
			{
				++m_index;
			}
			
			m_token = new Token(m_text, offset, m_index - offset, m_line, TokenKind.Identifier);
		}
		
		// operator-or-punctuator: one of
		// {	}	[	]	(	)	.	,	:	;
		// +	-	*	/	%	&	|	^	!	~
		// =	<	>	?	??	::	++	--	&&	||
		// ->	==	!=	<=	>=	+=	-=	*=	/=	%=
		// &=	|=	^=	<<	<<=	=>	 	 	 	 
		private void DoScanPunct()
		{
			switch (Current)
			{
				case '?':
					if (Next == '?')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case ':':
					if (Next == ':')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '+':
					if (Next == '+')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '-':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '>')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '-')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '&':
					if (Next == '&')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '|':
					if (Next == '|')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '=':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '>')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '!':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '<':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '<')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else if (Next == '<' && NextNext == '=')
					{
						m_token = new Token(m_text, m_index, 3, m_line, TokenKind.Punct);
						m_index += 3;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '>':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '*':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '/':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '%':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
					
				case '^':
					if (Next == '=')
					{
						m_token = new Token(m_text, m_index, 2, m_line, TokenKind.Punct);
						m_index += 2;
					}
					else
					{
						m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
						++m_index;
					}
					break;
				
				default:
					m_token = new Token(m_text, m_index, 1, m_line, TokenKind.Punct);
					++m_index;
					break;
			}
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
			while (CsHelpers.CanContinueIdentifier(Current))	
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
			int offset = m_index;
			
			while (Current != '\n' && Current != '\r' && Current != '\x00')
			{
				++m_index;
			}
			
			m_comments.Add(new Token(m_text, offset, m_index - offset, m_line, TokenKind.Comment));
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
			int offset = m_index;
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
					m_comments.Add(new Token(m_text, offset, m_index - offset, line, TokenKind.Comment));
					return;
				}
				else
				{
					++m_index;
				}
			}
			
			throw new ScannerException("Expected a terminating '*/' for line {0}", line);
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_text;
		private char* m_buffer;
		private int m_index;
		private int m_line = 1;
		private Token m_token;
		private List<CsPreprocess> m_preprocess = new List<CsPreprocess>();
		private List<Token> m_tokens = new List<Token>();
		private List<Token> m_comments = new List<Token>();
		private int m_threadID;
		#endregion
	}
}
