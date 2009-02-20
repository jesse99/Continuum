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

namespace CsRefactor.Script
{
	[Serializable]
	internal sealed class ScannerException : ScriptException
	{
		public ScannerException()
		{
		}
		
		public ScannerException(int line, string text) : base(line, text) 
		{
		}

		public ScannerException(int line, string format, params object[] args) : base(line, string.Format(format, args)) 
		{
		}

		public ScannerException(int line, string text, Exception inner) : base (line, text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private ScannerException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
		
	internal sealed unsafe class Scanner
	{
		public Scanner(string text) : this(text, 1)
		{
		}
		
		public Scanner(string text, int line)
		{
			Trace.Assert(text != null, "text is null");
			Trace.Assert(line > 0, "line is not positive");
			
			m_line = line;
			m_text = text + '\x00';		// scanning is easier and faster with a sentinel value			
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

		#region Private Methods
		// This is a bottleneck for the parser so it's important that it be fast.
		// Using a pointer should be pretty fast, but other possibilities are
		// to use Marshal.AllocHGlobal and ReadInt16 or the System.IO.UnmanagedMemoryAccessor
		// class in C# 4.0.
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
				else if (Current == '#')
					DoSkipComment();
				else
					break;
			}
							
			// identifier
			if (char.IsLetter(Current) || Current == '_')
			{
				DoScanIdentifier();
			}
			
			// string 
			else if (Current == '"')
			{
				DoScanString();
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
		
		// StringLiteral := '"' StringChar+ '"'
		// StringChar := any char but '"' or '""'
		private void DoScanString()
		{
			int line = m_line;
			int offset = m_index;
			m_index = m_index + 1;
	
			while (Current != '\x00')
			{
				if (char.IsWhiteSpace(Current))
					DoSkipWhiteSpace();
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
				throw new ScannerException(m_line, "Expected a terminating '\"' for.");
		}
		
		// Identifier := IdentifierStart IdentifierSuffix*
		// IdentifierStart := ascii letter or underscore
		// IdentifierSuffix := ascii letter, ascii digit, or underscore
		private void DoScanIdentifier()
		{
			int offset = m_index;
			
			while (char.IsLetterOrDigit(Current) || Current == '_')	
			{
				++m_index;
			}
			
			string name = m_text.Substring(offset, m_index - offset);	
			if (ms_reserved.Contains(name))
				throw new ScannerException(m_line, "{0} is a reserved word.", name);
				
			TokenKind kind = ms_keywords.Contains(name) ? TokenKind.Keyword : TokenKind.Identifier;
			m_token = new Token(m_text, offset, m_index - offset, m_line, kind);
		}
				
		// Whitespace := ' ' | '\t' | '\n'
		private void DoSkipWhiteSpace()
		{
			while (true)
			{
				if (Current == '\n')
				{
					++m_line;
					++m_index;
				}
				else if (Current == '\r')
				{
					throw new ScannerException(1, "Refactor scripts should use Unix line endings.");
				}
				else if (Current == ' ' || Current == '\t')
				{
					++m_index;
				}
				else
					break;
			}
		}
		
		// Comment := '#' AnyChar* NewLine
		private void DoSkipComment()
		{
			while (Current != '\n' && Current != '\x00')
			{
				++m_index;
			}
		}
		#endregion

		#region Fields 
		private string m_text;
		private char* m_buffer;
		private int m_index = 0;
		private int m_line;
		private Token m_token; 
		
		private static HashSet<string> ms_keywords = new HashSet<string>{"and",  "define",  "do",  "elif",  "else",  "end",  "false",  "for",  "from",  "if",  "in",  "is",  "let",  "not",  "null",  "or",  "property",  "return",  "select",  "self",  "then",  "true",  "when",  "where"};
		private static HashSet<string> ms_reserved = new HashSet<string>{"assert",  "class",  "case",  "except",  "foreach",  "match",  "otherwise",  "while"};
		#endregion
	} 
}
