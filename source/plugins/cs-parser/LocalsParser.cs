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

namespace CsParser
{
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
		// Despite the simplistic implementation the code should work pretty well. 
		// The only problem caused by its simple nature is that locals defined in
		// linq expressions with let, for/foreach loop variables, and fixed pointer
		// variables have incorrect scopes. But, in practice, this should be OK: locals
		// defined later will hide the earlier ones so everything will normally work 
		// fine for users.
		public Local[] Parse(string text, int start, int stop)
		{
			Contract.Requires(text != null, "text is null");
			Contract.Requires(start >= 0, "start is negative");
			Contract.Requires(start <= text.Length, "start is too big");
			Contract.Requires(start <= stop, "stop is too small");
//Console.WriteLine("---------------------------");
//Console.WriteLine(text.Substring(start, stop - start).EscapeAll());
			
			var locals = new List<Local>();
			
			try
			{
				m_text = text.Substring(start, stop - start);
				
				m_scanner = new Scanner();
				m_scanner.Init(m_text);
				
				while (m_scanner.Token.IsValid())
				{
					if (m_scanner.Token.IsPunct("{"))
					{
						DoParseBlock(locals, 0);
					}
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
//Console.WriteLine("token = {0}, line {1}", m_scanner.Token.Text(), m_scanner.Token.Line);
				if (m_scanner.Token.Kind == TokenKind.Identifier)
				{
					DoParseLocal(locals);
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
		
		// TODO: handle fixed-statement
		private void DoParseLocal(List<Local> locals)
		{
			if (m_scanner.Token.Kind == TokenKind.Identifier)
			{
				string typeOrName = DoParseType();
				if (typeOrName != null)
				{
					if (typeOrName == "foreach")
						DoParseForEach(locals);
					else if (typeOrName == "catch")
						DoParseCatch(locals);
					else
						DoParseLocalVariable(locals, typeOrName);
				}
			}
		}
		
		// foreach-statement:
		//     foreach   (   local-variable-type   identifier   in   expression   )   embedded-statement
		private void DoParseForEach(List<Local> locals)
		{
			if (m_scanner.Token.IsPunct("("))
			{
				m_scanner.Advance();
				
				string type = DoParseType();
				if (type != null)
				{
					if (m_scanner.Token.Kind == TokenKind.Identifier)
					{
						string name = m_scanner.Token.Text();
						m_scanner.Advance();
						
//Console.WriteLine("type: {0}", type);
//Console.WriteLine("    name: {0}", name);
						locals.Add(new Local(type, name, null));
					}
				}
			}
		}
		
		// specific-catch-clause:
		//     catch   (   class-type   identifier?   )   block
		private void DoParseCatch(List<Local> locals)
		{
			if (m_scanner.Token.IsPunct("("))
			{
				m_scanner.Advance();
				
				string type = DoParseType();
				if (type != null)
				{
					if (m_scanner.Token.Kind == TokenKind.Identifier)
					{
						string name = m_scanner.Token.Text();
						m_scanner.Advance();
						
						locals.Add(new Local(type, name, null));
					}
				}
			}
		}
		
		// declaration-statement:
		//      local-variable-declaration   ;
		//      local-constant-declaration   ;
		// 
		// local-variable-declaration:
		//      local-variable-type   local-variable-declarators
		// 
		// local-variable-type:
		//      type
		//      var
		// 
		// local-variable-declarators:
		//      local-variable-declarator
		//      local-variable-declarators   ,   local-variable-declarator
		private void DoParseLocalVariable(List<Local> locals, string type)
		{
//Console.WriteLine("type: {0}", type);
			var candidates = new List<Local>();
			bool ok = DoParseLocalDeclarator(type, candidates);
			
			while (ok && m_scanner.Token.IsPunct(","))
			{
				m_scanner.Advance();
				ok = DoParseLocalDeclarator(type, candidates);
			}
			
			if (ok && (m_scanner.Token.IsPunct(";") || (candidates.Last().Value != null && candidates.Last().Value.StartsWith("from "))))
			{
//Console.WriteLine("candidates: {0}", candidates.ToDebugString());
				m_scanner.Advance();
				locals.AddRange(candidates);
			}
		}
		
		// local-variable-declarator:
		//      identifier
		//      identifier   =   local-variable-initializer
		// 
		// local-variable-initializer:
		//      expression
		//      array-initializer
		private bool DoParseLocalDeclarator(string type, List<Local> locals)
		{
			bool ok = false;
			
			if (m_scanner.Token.Kind == TokenKind.Identifier)
			{
				string name = m_scanner.Token.Text();
				m_scanner.Advance();
				ok = true;
//Console.WriteLine("    name: {0}", name);
				
				string value = null;
				if (m_scanner.Token.IsPunct("="))
				{
					m_scanner.Advance();
					if (m_scanner.Token.IsValid())
						value = DoParseExpression();
//Console.WriteLine("    value: {0}", value);
				}
				
				if (ok)
					locals.Add(new Local(type, name, value));
			}
			
			return ok;
		}
		
		// This is definitely cheesy, but it will usually work well enough for our
		// purposes. 
		// TODO: need to  handle 
		// linq, `var foo = from x in xs let y = 2*x select x + y;` should result in 3 locals
		// lambda-expression
		private string DoParseExpression()
		{
			Token start = m_scanner.Token;
			Token last = m_scanner.Token;
			
			bool scanning = true;
			string body;
			while (m_scanner.Token.IsValid() && scanning)
			{
				switch (m_scanner.Token.Text())
				{
					case ";":
					case ",":
					case "for":
					case "foreach":
					case "let":
					case "where":
					case "join":
					case "into":
					case "orderby":
					case "select":
					case "group":
					case "fixed":
					case "=>":
						scanning = false;
						break;
					
					// Need these so we don't stop at a comma we should not stop at.
					case "(":
						body = DoScanBody("(", ")", ref last);
						if (body == null)
							scanning = false;
						break;
					
					case "[":
						body = DoScanBody("[", "]", ref last);
						if (body == null)
							scanning = false;
						break;
					
					case "<":
						body = DoScanBody("<", ">", ref last, ";", "(", "{", ")");
						if (body == null)
							scanning = false;
						break;
					
					// Need this one so our caller doesn't think a block has ended.
					case "{":
						body = DoScanBody("{", "}", ref last);
						if (body == null)
							scanning = false;
						break;
						
					default:
						last = m_scanner.Token;
						m_scanner.Advance();
						break;
				}
			}
			
			return m_text.Substring(start.Offset, last.Offset + last.Length - start.Offset);
		}
		
		private string DoParseType()
		{
			string type = m_scanner.Token.Text();
			m_scanner.Advance();
			
			while (m_scanner.Token.IsPunct(".") && m_scanner.LookAhead(1).Kind == TokenKind.Identifier)
			{
				type += ".";
				m_scanner.Advance();
				
				type += m_scanner.Token.Text();
				m_scanner.Advance();
			}
			
			Token last = m_scanner.Token;
			while (m_scanner.Token.IsPunct("<") || m_scanner.Token.IsPunct("[") || m_scanner.Token.IsPunct("?") || m_scanner.Token.IsPunct("*"))
			{
				if (m_scanner.Token.IsPunct("<"))
				{
					type += DoScanBody("<", ">", ref last, ";", "(", "{", ")");	// scan "<KEY, VALUE>" but skip (most of) "if (foo < xxx)"
				}
				else if (m_scanner.Token.IsPunct("["))
				{
					type += DoScanBody("[", "]");
				}
				else
				{
					type += m_scanner.Token.Text();
					m_scanner.Advance();
				}
			}
			
			if (ms_nonTypeKeywords.Contains(type))
				type = null;
			
			return type;
		}
		
		// Returns either null or the inclusive text between the (possibly nested)
		// open and close strings.
		private string DoScanBody(string open, string close)
		{
			Token last = m_scanner.Token;
			return DoScanBody(open, close, ref last);
		}
		
		private string DoScanBody(string open, string close, ref Token last)
		{
			return DoScanBody(open, close, ref last, new string[0]);
		}

		// TODO: reset the scanner on failure?
		private string DoScanBody(string open, string close, ref Token rlast, params string[] bad)
		{
			Token first = m_scanner.Token;
			Token last = m_scanner.Token;
			
			int count = 1;
			m_scanner.Advance();
			
			while (m_scanner.Token.IsValid() && count > 0 && !bad.Contains(m_scanner.Token.Text()))
			{
				if (m_scanner.Token.IsPunct(open))
					++count;
				else if (m_scanner.Token.IsPunct(close))
					--count;
				
				last = m_scanner.Token;
				m_scanner.Advance();
			}
			
			string result = null;
			if (count == 0)
			{
				rlast = last;
				result = m_text.Substring(first.Offset, last.Offset + last.Length - first.Offset);
			}
			
			return result;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private Scanner m_scanner;
		private string m_text;
		private static string[] ms_nonTypeKeywords = new string[]
		{
			"abstract",
			"as",
			"ascending",
			"base",
			"break",
			"by",
			"case",
			"catch",
			"checked",
			"class",
			"const",
			"continue",
			"default",
			"delegate",
			"descending",
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
			"from",
			"get",
			"goto",
			"group",
			"if",
			"implicit",
			"in",
			"interface",
			"internal",
			"into",
			"is",
			"join",
			"let",
			"lock",
			"namespace",
			"new",
			"null",
			"on",
			"operator",
			"orderby",
			"out",
			"override",
			"params",
			"partial",
			"partial",
			"private",
			"protected",
			"public",
			"readonly",
			"ref",
			"return",
			"sealed",
			"select",
			"set",
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
			"volatile",
			"where",
			"where",
			"while",
			"yield",
		};
		#endregion
	}
}
