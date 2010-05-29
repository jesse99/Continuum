// Copyright (C) 2010 Jesse Jones
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

using MObjc.Helpers;
using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Debugger
{
	internal sealed partial class ExpressionParser
	{
		private Expression DoEvalAndExpr(List<Result> results)
		{
			Expression result = results[0].Value;
			
			for (int i = 2; i < results.Count; i += 2)
			{
				result = new AndExpression(result, results[i].Value);
			}
			
			return result;
		}
		
		private Expression DoEvalExclusiveOrExpr(List<Result> results)
		{
			Expression result = results[0].Value;
			
			for (int i = 2; i < results.Count; i += 2)
			{
				result = new ExclusiveOrExpression(result, results[i].Value);
			}
			
			return result;
		}
		
		private Expression DoEvalOrExpr(List<Result> results)
		{
			Expression result = results[0].Value;
			
			for (int i = 2; i < results.Count; i += 2)
			{
				result = new OrExpression(result, results[i].Value);
			}
			
			return result;
		}
		
		private Expression DoEvalPostfixExpr(List<Result> results)
		{
			var suffixes = new List<PostfixOperator>();
		
			for (int i = 1; i < results.Count; ++i)
			{
				suffixes.Add((PostfixOperator) results[i].Value);
			}
			
			var identifier = new Identifier(results[0].Text);
			return new PostfixExpression(identifier, suffixes.ToArray());
		}
		
		private Expression DoParseEscapeChar(char ch)
		{
			switch (ch)
			{
				case '\'':
					return new Literal<char>('\'');
				
				case '"':
					return new Literal<char>('"');
				
				case '\\':
					return new Literal<char>('\\');
				
				case '0':
					return new Literal<char>('\0');
				
				case 'a':
					return new Literal<char>('\a');
				
				case 'b':
					return new Literal<char>('\b');
				
				case 'f':
					return new Literal<char>('\f');
				
				case 'n':
					return new Literal<char>('\n');
				
				case 'r':
					return new Literal<char>('\r');
				
				case 't':
					return new Literal<char>('\t');
				
				case 'v':
					return new Literal<char>('\v');
				
				default:
					Contract.Assert(false);
					return null;
			}
		}
		
		// Text will be "'\xABCD'".
		private Expression DoParseHexEscapeChar(string text)
		{
			int codePoint = int.Parse(text.Substring(3, text.Length - 4), NumberStyles.AllowHexSpecifier);
			return new Literal<char>((char) codePoint);
		}
		
		private Expression DoParseReal(string text)
		{
			if (text.EndsWith("f") || text.EndsWith("F"))
				return new Literal<float>(float.Parse(text.Substring(0, text.Length - 1)));
			
			else if (text.EndsWith("d") || text.EndsWith("D"))
				return new Literal<double>(double.Parse(text.Substring(0, text.Length - 1)));
			
			else if (text.EndsWith("m") || text.EndsWith("M"))
				return new Literal<decimal>(decimal.Parse(text.Substring(0, text.Length - 1)));
			
			else
				return new Literal<double>(double.Parse(text));
		}
		
		// Text will be "\"blah\"'.
		private Expression DoParseString(string text)
		{
			string value = Parsers.ParseString(text.Substring(1, text.Length - 2));
			return new StringLiteral(value);
		}
		
		// Text will be @"\"blah\"'.
		private Expression DoParseVerbatimString(string text)
		{
			string value = Parsers.ParseVerbatimString(text.Substring(2, text.Length - 3));
			return new StringLiteral(value);
		}
	}
}
