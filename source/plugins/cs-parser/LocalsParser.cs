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
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace CsParser
{
	[Serializable]
	public sealed class LocalException : Exception
	{
		public LocalException()
		{
		}
		
		public LocalException(string message) : base(message)
		{
		}
		
		public LocalException(string message, Exception inner) : base(message)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private LocalException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	internal sealed class LocalsParser : ICsLocalsParser
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		// This is an ad-hoc parser used with auto-complete. It would also be possible
		// to write a real method body parser but that is an awful lot of work just to
		// extract local declarations and is made trickier because the method body is
		// being edited so it may be very malformed.
		//
		// What we do here is treat everything that could be a local variable as a 
		// local variable. This may result in a few false positives, but that should
		// happen rarely and should not be a problem unless the false local hides
		// a real one.
		public Local[] Parse(string text, int start, int stop)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(start >= 0, "start is negative");
			Contract.Requires(start <= text.Length, "start is too big");
			Contract.Requires(start <= stop, "stop is too small");
			
			var locals = new List<Local>();
			
			try
			{
				m_text = text.Substring(start, stop - start);
				
				m_scanner = new Scanner();
				m_scanner.Init(m_text);
				
				while (m_scanner.Token.IsValid())
				{
					if (m_scanner.Token.IsPunct("{"))
						DoParseBlock(locals, 0);
					else
						m_scanner.Advance();
				}
			}
			catch (ScannerException)
			{
			}
			
			return locals.ToArray();
		}
		
		#region Private Methods
		public void DoParseBlock(List<Local> locals, int depth)
		{
			m_scanner.Advance();
			
			int count = locals.Count;
			while (m_scanner.Token.IsValid())
			{
				if (m_scanner.Token.Kind == TokenKind.Identifier)
				{
					if (!ms_keywords.Contains(m_scanner.Token.Text()))
						DoTryParseLocals(locals);
					else
						m_scanner.Advance();
				}
				else if (m_scanner.Token.IsPunct("{"))
				{
					DoParseBlock(locals, depth + 1);
				}
				else if (m_scanner.Token.IsPunct("}"))
				{
					locals.RemoveRange(count, locals.Count - count);
					m_scanner.Advance();
					break;
				}
				else
					m_scanner.Advance();
			}
		}
		
		private string DoParseExpression()
		{
			var builder = new StringBuilder();
			
			int angleCount = 0;
			while (m_scanner.Token.IsValid())
			{
				if (m_scanner.Token.IsPunct(";") || (angleCount == 0 && m_scanner.Token.IsPunct(",")))
				{
					break;
				}
				else if (m_scanner.Token.IsPunct(")") || m_scanner.Token.IsPunct("}"))
				{
					break;
				}
				else if (m_scanner.Token.IsPunct("(") || m_scanner.Token.IsPunct("{"))
				{
					angleCount = Math.Max(--angleCount, 0);
					DoParseSubExpression(builder);
				}
				else if (m_scanner.Token.IsPunct("["))
				{
					DoParseSubExpression(builder);
				}
				else
				{
					// This is a bit tricky: we don't want to stop on a comma if we're inside a
					// type-argument-list but we don't have enough context to know if the '<'
					// is starting the list or is the less than operator. So what we do is assume
					// it is the list and invalidate our assumption if we hit a token which can't
					// be part of a type name.
					if (m_scanner.Token.Kind == TokenKind.Punct)
					{
						if (m_scanner.Token.Equals("<"))
							++angleCount;
						else if (m_scanner.Token.Equals(">"))
							angleCount = Math.Max(--angleCount, 0);
						else if (angleCount > 0 && !(m_scanner.Token.Equals(".") || m_scanner.Token.Equals("::") || m_scanner.Token.Equals("?") || m_scanner.Token.Equals("*")))
							angleCount = Math.Max(--angleCount, 0);
					}
					else if (m_scanner.Token.Kind == TokenKind.Char || m_scanner.Token.Kind == TokenKind.Number || m_scanner.Token.Kind == TokenKind.String)
						angleCount = Math.Max(--angleCount, 0);
					else if (m_scanner.Token.Kind == TokenKind.Identifier && ms_keywords.Contains(m_scanner.Token.Text()))
						angleCount = Math.Max(--angleCount, 0);
					
					builder.Append(m_scanner.Token.Text());
					builder.Append(' ');
					m_scanner.Advance();
				}
			}
			
			return builder.ToString();
		}
		
		// LocalName := Name ('=' Expression)?
		private void DoParseLocalName(string type, List<Local> locals)
		{
			Contract.Requires(!ms_keywords.Contains(type), type + " is a keyword");
			
			string name = m_scanner.Token.Text();
			m_scanner.Advance();
			
			string value = null;
			if (m_scanner.Token.IsPunct("="))
			{
				m_scanner.Advance();
				value = DoParseExpression();
			}
			
			var local = new Local(type, name, value);
			Log.WriteLine(TraceLevel.Verbose, "LocalsParser", "    adding {0}", local);
			locals.Add(local);
		}
		
		// LocalNames := LocalName (',' LocalName)*
		private void DoParseLocalNames(string type, List<Local> locals)
		{
			DoParseLocalName(type, locals);
			
			while (m_scanner.Token.IsPunct(","))
			{
				m_scanner.Advance();
				
				if (m_scanner.Token.Kind == TokenKind.Identifier)
					DoParseLocalName(type, locals);
				else
					throw new LocalException("Expected an identifier, but found " + m_scanner.Token);
			}
		}
		
		// namespace-or-type-name:
		//    identifier   type-argument-list?
		//    namespace-or-type-name   .   identifier   type-argument-list?
		//    qualified-alias-member
		// 
		// qualified-alias-member:
		//      identifier   ::   identifier   type-argument-list?
		private void DoParseNamespaceOrTypeName(StringBuilder builder)
		{
			DoParseNamespaceOrTypeName2(builder);
			while (m_scanner.Token.IsPunct("."))
			{
				builder.Append('.');
				m_scanner.Advance();
				
				if (m_scanner.Token.Kind == TokenKind.Identifier)
					DoParseNamespaceOrTypeName2(builder);
				else
					throw new LocalException("Expected an identifier, but found " + m_scanner.Token);
			}
		}
		
		private void DoParseNamespaceOrTypeName2(StringBuilder builder)
		{
			if (m_scanner.Token.Kind != TokenKind.Identifier)
				throw new LocalException("Expected a namespace or type name, but found " + m_scanner.Token);
		
			builder.Append(m_scanner.Token.Text());	
			m_scanner.Advance();
			
			if (m_scanner.Token.IsPunct("::"))
			{
				builder.Append("::");
				m_scanner.Advance();
				
				if (m_scanner.Token.Kind == TokenKind.Identifier)
				{
					builder.Append(m_scanner.Token.Text());	
					m_scanner.Advance();
				}
				else
					throw new LocalException("Expected an identifier, but found " + m_scanner.Token);
			}
			
			if (m_scanner.Token.IsPunct("<"))
				DoParseTypeArgumentList(builder);
		}
		
		// rank-specifiers:
		//     rank-specifier
		//     rank-specifiers   rank-specifier
		// 
		// rank-specifier:
		//     [   dim-separators?   ]
		// 
		// dim-separators:
		//     ,
		//     dim-separators   ,
		private void DoParseRankSpecifiers(StringBuilder builder)
		{
			while (m_scanner.Token.IsPunct("["))
			{
				builder.Append('[');
				m_scanner.Advance();
				
				while (m_scanner.Token.IsPunct(","))
				{
					builder.Append(',');
					m_scanner.Advance();
				}
				
				if (m_scanner.Token.IsPunct("]"))
				{
					builder.Append(']');
					m_scanner.Advance();
				}
				else
					throw new LocalException("Expected a ']', but found " + m_scanner.Token);
			}
		}
		
		private void DoParseSubExpression(StringBuilder builder)
		{
			var open = new List<string>();
			open.Add(m_scanner.Token.Text());
			builder.Append(m_scanner.Token.Text());
			builder.Append(' ');
			m_scanner.Advance();
			
			while (m_scanner.Token.IsValid() && open.Count > 0)
			{
				builder.Append(m_scanner.Token.Text());
				builder.Append(' ');
				
				if (m_scanner.Token.IsPunct("(") || m_scanner.Token.IsPunct("[") || m_scanner.Token.IsPunct("{"))
				{
					open.Add(m_scanner.Token.Text());
				}
				else if (m_scanner.Token.IsPunct(")"))
				{
					if (open.Last() == "(")
						open.Pop();
					else
						throw new LocalException("Found a ')' which doesn't close a '('");
				}
				else if (m_scanner.Token.IsPunct("]"))
				{
					if (open.Last() == "[")
						open.Pop();
					else
						throw new LocalException("Found a ']' which doesn't close a '['");
				}
				else if (m_scanner.Token.IsPunct("}"))
				{
					if (open.Last() == "{")
						open.Pop();
					else
						throw new LocalException("Found a '}' which doesn't close a '{'");
				}
				
				m_scanner.Advance();
			}
			
			// Note that it isn't necessarily an error if we were not able to find all of the close
			// tokens because we only parse up to the insertion point.
		}
		
		private void DoParseType(StringBuilder builder)
		{
			DoParseNamespaceOrTypeName(builder);
			
			while (m_scanner.Token.IsValid())
			{
				if (m_scanner.Token.IsPunct("?"))
				{
					builder.Append('?');
					m_scanner.Advance();
				}
				else if (m_scanner.Token.IsPunct("*"))
				{
					builder.Append('*');
					m_scanner.Advance();
				}
				else if (m_scanner.Token.IsPunct("["))
				{
					DoParseRankSpecifiers(builder);
				}
				else
					break;
			}
		}
		
		// type-argument-list:
		//      <   type-arguments   >
		// 
		// type-arguments:
		//      type-argument
		//      type-arguments   ,   type-argument
		//
		// type-argument:
		//      type
		private void DoParseTypeArgumentList(StringBuilder builder)
		{
			builder.Append('<');
			m_scanner.Advance();
			
			DoParseType(builder);
			while (m_scanner.Token.IsPunct(","))
			{
				builder.Append(',');
				m_scanner.Advance();
				
				DoParseType(builder);
			}
			
			if (m_scanner.Token.IsPunct(">"))
			{
				builder.Append('>');
				m_scanner.Advance();
			}
			else
				throw new LocalException("Expected '>', but found " + m_scanner.Token);
		}
		
		// Locals := Type LocalNames
		private void DoTryParseLocals(List<Local> locals)
		{
			try
			{
				int line = m_scanner.Token.Line;
				var type = new StringBuilder();
				DoParseType(type);
				Log.WriteLine(TraceLevel.Verbose, "LocalsParser", "found candidate type {0} on line {1}", type, line);
				
				if (m_scanner.Token.Kind == TokenKind.Identifier)
					DoParseLocalNames(type.ToString(), locals);
			}
			catch (LocalException e)
			{		
				Log.WriteLine(TraceLevel.Info, "LocalsParser", "Parse error on line {0}", m_scanner.Token.Line);
				Log.WriteLine(TraceLevel.Info, "LocalsParser", e.Message);
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private Scanner m_scanner;
		private string m_text;
		private HashSet<string> ms_keywords = new HashSet<string>
		{
			// keywords which are not type names
			"abstract",
			"as",
			"base",
			"break",
			"case",
			"catch",
			"checked",
			"class",
			"const",
			"continue",
			"default",
			"delegate",
			"do",
			"else",
			"enum",
			"event",
			"explicit",
			"extern",
			"false",
			"finally",
			"fixed",
			"for",
			"foreach",
			"goto",
			"if",
			"implicit",
			"in",
			"interface",
			"internal",
			"is",
			"lock",
			"namespace",
			"new",
			"null",
			"operator",
			"out",
			"override",
			"params",
			"private",
			"protected",
			"public",
			"readonly",
			"ref",
			"return",
			"sealed",
			"sizeof",
			"stackalloc",
			"static",
			"struct",
			"switch",
			"this",
			"throw",
			"true",
			"try",
			"typeof",
			"unchecked",
			"unsafe",
			"using",
			"virtual",
			"void",
			"volatile",
			"while",
			
			// context sensitive keywords
			"ascending",
			"by",
			"descending",
			"from",
			"get",
			"group",
			"into",
			"join",
			"let",
			"on",
			"orderby",
			"partial",
			"select",
			"set",
			"where",
			"yield",
		};
		#endregion
	}
}
