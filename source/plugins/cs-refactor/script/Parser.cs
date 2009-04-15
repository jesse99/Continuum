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
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace CsRefactor.Script
{
	[Serializable]
	internal sealed class ParserException : ScriptException
	{
		public ParserException()
		{
		}
				
		public ParserException(int line, string text) : base(line, text) 
		{
		}

		public ParserException(int line, string format, params object[] args) : base(line, string.Format(format, args)) 
		{
		}

		public ParserException(int line, string text, Exception inner) : base (line, text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private ParserException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	internal sealed class Parser
	{
		public Parser(string text)
		{
			Contract.Requires(text != null, "text is null");
			
			m_scanner = new Scanner(text);
		}
		
		private Parser(string text, int line)
		{
			Contract.Requires(text != null, "text is null");
			
			m_scanner = new Scanner(text, line);
		}
		
		public Script Parse()
		{
			Script script = DoParseCompilationUnit();
			
			if (!script.Methods.Any(m => m.Name == "Run"))
				throw new ParserException(1, "Script has no Run method.");
			
			return script;
		}

		#region Private Methods
		// "foo #{e} bar #{f}" => "foo " + (e) + " bar " + (f) + ""
		private string DoInterpolateString(int line, string text)
		{
			var builder = new StringBuilder(text.Length + 8);
			
			builder.Append('"');
			
			int i = 0;
			bool copying = true;
			while (i < text.Length)
			{
				if (copying)
				{
					if (i + 1 < text.Length && text[i] == '#' && text[i + 1] == '{')
					{
						copying = false;
						builder.Append("\" + (");
						i += 2;
					}
					else
					{
						builder.Append(text[i]);
						++i;
					}
				}
				else
				{
					if (text[i] == '}')
					{
						copying = true;
						builder.Append(") + \"");
					}
					else if (i + 1 < text.Length && text[i] == '"' && text[i + 1] == '"')
					{
						builder.Append('"');
						i += 1;
					}
					else
					{
						builder.Append(text[i]);
					}
					++i;
				}
			}
			
			if (!copying)
				throw new ParserException(line, "Expected a '}' to close the string interpolation.");
			
			builder.Append('"');

			return builder.ToString();
		}

		// ActualArgs := '(' ExpressionList? ')'
		private Expression[] DoParseActualArgs(List<string> locals, string method)
		{
			Expression[] args;
			
			DoParsePunct("(", string.Format("to open the {0} method argument list", method));

			if (!m_scanner.Token.IsPunct(")"))
				args = DoParseExpressionList(locals);
			else
				args = new Expression[0];
			
			DoParsePunct(")", string.Format("to close the {0} method argument list", method));

			return args;
		}
		
		// AddExpresion := UnaryExpression ('+' UnaryExpression)*
		private Expression DoParseAddExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParseUnaryExpression(locals);
			
			while (m_scanner.Token.IsPunct("+"))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression rhs = DoParseUnaryExpression(locals);
				result = new InvokeMethod(line, result, "op_Add", new Expression[]{rhs});
			}

			return result;
		}
		
		// AndExpression = EqualityExpression ('and' EqualityExpression)*
		private Expression DoParseAndExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParseEqualityExpression(locals);
			
			while (m_scanner.Token.IsKeyword("and"))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression rhs = DoParseEqualityExpression(locals);
				result = new InvokeMethod(line, result, "op_LogicalAnd", new Expression[]{rhs});
			}

			return result;
		}
		
		// CallExpression = PrimaryExpression ('.' MethodCall)*
		private Expression DoParseCallExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParsePrimaryExpression(locals);

			while (m_scanner.Token.IsPunct("."))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				result = DoParseMethodCall(line, locals, result);
			}

			return result;
		}
		
		// CompilationUnit := Declaration+
		// Declaration := MethodDeclaration | PropertyDeclaration
		public Script DoParseCompilationUnit()
		{
			var methods = new List<Method>();
			
			var locals = new List<string>();
			while (m_scanner.Token.IsKeyword("define"))
			{
				m_scanner.Advance();
				
				if (m_scanner.Token.IsKeyword("property"))
				{
					m_scanner.Advance();
					methods.Add(DoParsePropertyDeclaration(locals));
				}
				else
				{
					methods.Add(DoParseMethodDeclaration(locals));
				}
			}
			Contract.Requires(locals.Count == 0, "not all locals were cleaned up");
			
			for (int i = 0; i < methods.Count; ++i)
			{
				for (int j = i + 1; j < methods.Count; ++j)
				{
					if (methods[i].Name == methods[j].Name)
						throw new ParserException(1, "The {0} method was defined more than once.", methods[i].Name);
				}
			}
			
			if (m_scanner.Token.IsValid())
				throw new ParserException(m_scanner.Token.Line, "Expected eof, but found '{0}'.", m_scanner.Token.Text());

			return new Script(methods.ToArray());
		}
		
		// ElifClause := 'elif' Expression 'then' Statement*
		private void DoParseElifClause(List<string> locals, List<Expression> predicates, List<Statement[]> blocks)
		{
			Expression predicate = DoParseExpression(locals);
			DoParseKeyword("then", "for the elif clause");
			Statement[] statements = DoParseStatements(locals);
			
			predicates.Add(predicate);
			blocks.Add(statements);
		}
		
		// ElseClause := 'else' Statement*
		private void DoParseElseClause(int line, List<string> locals, List<Expression> predicates, List<Statement[]> blocks)
		{
			Statement[] statements = DoParseStatements(locals);
			
			predicates.Add(new BooleanLiteral(line, true));
			blocks.Add(statements);
		}
		
		// EqualityExpression = RelationalExpression (('==' | '!=') RelationalExpression)?
		private Expression DoParseEqualityExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParseRelationalExpression(locals);
			
			if (m_scanner.Token.IsPunct("=="))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression rhs = DoParseRelationalExpression(locals);
				result = new InvokeMethod(line, result, "op_Equals", new Expression[]{rhs});
			}
			else if (m_scanner.Token.IsPunct("!="))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression rhs = DoParseRelationalExpression(locals);
				result = new InvokeMethod(line, result, "op_NotEquals", new Expression[]{rhs});
			}
			
			return result;
		}
		
		// Expression := OrExpression | FromExpression | WhenExpression
		private Expression DoParseExpression(List<string> locals)
		{
			Expression result;
			
			if (m_scanner.Token.IsKeyword("from"))
			{
				m_scanner.Advance();
				result = DoParseFromExpression(locals);
			}
			else
			{
				int line = m_scanner.Token.Line;
				result = DoParseOrExpression(locals);

				if (m_scanner.Token.IsKeyword("when"))
				{
					m_scanner.Advance();
					result = DoParseWhenExpression(locals, result, line);
				}
			}

			return result;
		}
		
		// ExpressionList := Expression (',' Expression)*
		private Expression[] DoParseExpressionList(List<string> locals)
		{
			var exprs = new List<Expression>();
			
			exprs.Add(DoParseExpression(locals));
			while (m_scanner.Token.IsPunct(","))
			{
				m_scanner.Advance();
				exprs.Add(DoParseExpression(locals));
			}
			
			return exprs.ToArray();
		}
		
		// FormalArgs := '(' IdentifierList? ')'
		private string[] DoParseFormalArgs()
		{
			string[] args;
			
			DoParsePunct("(", "to open the method's formal argument list");

			if (m_scanner.Token.Kind == TokenKind.Identifier)
				args = DoParseIdentifierList("for the method's formal arguments");
			else
				args = new string[0];
			
			DoParsePunct(")", "to close the method's formal argument list");

			return args;
		}
		
		// ForStatement := ForClause WhereClause? 'do' Statement* 'end'
		// ForClause := 'for' Identifier 'in' CallExpression	# Expression may be null
		// WhereClause := 'where' OrExpression
		private Statement DoParseFor(int line, List<string> locals)
		{
			string local = DoParseIdentifier("for the for loop variable");
			DoParseKeyword("in", "for the for elements");
			Expression elements = DoParseCallExpression(locals);
			
			if (locals.Contains(local))
				throw new ParserException(m_scanner.Token.Line, "There is already a definition for the '{0}' local.", local);
			locals.Add(local);
			
			Expression filter = null;
			if (m_scanner.Token.IsKeyword("where"))
			{
				m_scanner.Advance();
				filter = DoParseOrExpression(locals);
			}
			
			DoParseKeyword("do", "to start the for statements");
			Statement[] block = DoParseStatements(locals);
			DoParseKeyword("end", "to end the for statements");
			
			locals.Remove(local);
			
			return new For(line, local, elements, filter, block);
		}

		// FromExpression := FromClause ('where' OrExpression)? ('select' OrExpression)?
		// FromClause := 'from' Identifier 'in' CallExpression
		private Expression DoParseFromExpression(List<string> locals)
		{
			int line = m_scanner.Token.Line;
			string local = DoParseIdentifier("for the from loop variable");
			DoParseKeyword("in", "for the from elements");
			Expression elements = DoParseCallExpression(locals);
			
			if (locals.Contains(local))
				throw new ParserException(m_scanner.Token.Line, "There is already a definition for the '{0}' local.", local);
			locals.Add(local);
			
			Expression filter = null;
			if (m_scanner.Token.IsKeyword("where"))
			{
				m_scanner.Advance();
				filter = DoParseOrExpression(locals);
			}
						
			Expression map = null;
			if (m_scanner.Token.IsKeyword("select"))
			{
				m_scanner.Advance();
				map = DoParseOrExpression(locals);
			}
						
			locals.Remove(local);
			
			return new From(line, local, elements, filter, map);
		}		
		
		private string DoParseIdentifier(string reason)
		{
			if (m_scanner.Token.Kind != TokenKind.Identifier)
				throw new ParserException(m_scanner.Token.Line, "Expected an identifier {0}, but found '{1}'", reason, m_scanner.Token.Text());
			
			string name = m_scanner.Token.Text();
			m_scanner.Advance();
			
			return name;
		}
		
		// IdentifierList := Identifier (',' Identifier)*
		private string[] DoParseIdentifierList(string reason)
		{
			var identifiers = new List<string>();
			
			identifiers.Add(DoParseIdentifier(reason));
			while (m_scanner.Token.IsPunct(","))
			{
				m_scanner.Advance();
				identifiers.Add(DoParseIdentifier(reason));
			}
			
			return identifiers.ToArray();
		}
		
		// IfStatement := IfClause ElifClause* ElseClause? 'end'
		private Statement DoParseIf(int line, List<string> locals)
		{
			var predicates = new List<Expression>();
			var blocks = new List<Statement[]>();
			
			DoParseIfClause(locals, predicates, blocks);

			while (m_scanner.Token.IsKeyword("elif"))
			{
				m_scanner.Advance();
				DoParseElifClause(locals, predicates, blocks);
			}

			if (m_scanner.Token.IsKeyword("else"))
			{
				line = m_scanner.Token.Line;
				m_scanner.Advance();
				DoParseElseClause(line, locals, predicates, blocks);
			}
			
			DoParseKeyword("end", "to close the if statement");

			return new Conditional(line, predicates.ToArray(), blocks.ToArray());
		}
		
		// IfClause := 'if' Expression 'then' Statement*
		private void DoParseIfClause(List<string> locals, List<Expression> predicates, List<Statement[]> blocks)
		{
			Expression predicate = DoParseExpression(locals);
			DoParseKeyword("then", "for the if statement");
			Statement[] statements = DoParseStatements(locals);
			
			predicates.Add(predicate);
			blocks.Add(statements);
		}
		
		private void DoParseKeyword(string name, string reason)
		{
			if (!m_scanner.Token.IsKeyword(name))
				throw new ParserException(m_scanner.Token.Line, "Expected '{0}' {1}, but found '{2}'", name, reason, m_scanner.Token.Text());
			
			m_scanner.Advance();
		}
		
		// LetStatement := 'let' LetLocal (',' LetLocal)* 'in' Statement* 'end'
		private Statement DoParseLet(int line, List<string> locals)
		{
			var lets = new List<string>();
			var values = new List<Expression>();
			
			DoParseLetLocal(line, locals, lets, values);			
			while (m_scanner.Token.IsPunct(","))
			{
				m_scanner.Advance();
				DoParseLetLocal(line, locals, lets, values);			
			}
									
			DoParseKeyword("in", "for the local statement");

			Statement[] block = DoParseStatements(locals);
			DoParseKeyword("end", "to end the from statements");
			
			foreach (string local in lets)
			{
				locals.Remove(local);
			}
			
			return new Let(line, lets.ToArray(), values.ToArray(), block);
		}
				
		// LetLocal := Identifier '=' Expression 
		private void DoParseLetLocal(int line, List<string> locals, List<string> lets, List<Expression> values)
		{
			string local = DoParseIdentifier("for the let local");
			
			lets.Add(local);
			DoParsePunct("=", "for the let local value");
			values.Add(DoParseExpression(locals));
			
			if (locals.Contains(local))
				throw new ParserException(m_scanner.Token.Line, "There is already a definition for the '{0}' local.", local);
			locals.Add(local);
		}
		
		// Literal := 'true' | 'false' | 'null' | 'self' | SequenceLiteral | StringLiteral
		private Expression DoParseLiteral(List<string> locals)
		{
			Expression result = null;
			
			int line = m_scanner.Token.Line;
			if (m_scanner.Token.IsKeyword("true"))
			{
				result = new BooleanLiteral(line, true);
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsKeyword("false"))
			{
				result = new BooleanLiteral(line, false);
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsKeyword("null"))
			{
				result = new NullLiteral(line);
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsKeyword("self"))
			{
				result = new SelfLiteral(line);
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsPunct("["))
			{
				m_scanner.Advance();
				result = DoParseSequenceLiteral(line, locals);
			}
			else if (m_scanner.Token.Kind == TokenKind.String)
			{
				result = DoParseStringLiteral(locals);
			}
			
			return result;
		}
		
		// MethodCall := Identifier ActualArgs?
		private Expression DoParseMethodCall(int line, List<string> locals, Expression target)
		{
			Expression result;
			
			string name = DoParseIdentifier("for a method or property name");
			
			if (m_scanner.Token.IsPunct("("))
			{
				Expression[] args = DoParseActualArgs(locals, name);
				result = new InvokeMethod(line, target, name, args);
			}
			else
			{
				result = new InvokeMethod(line, target, "get_" + name, new Expression[0]);
			}
			
			return result;
		}
		
		// MethodDeclaration := 'define' Identifier FormalArgs Statement* 'end'
		private Method DoParseMethodDeclaration(List<string> locals)
		{
			int line = m_scanner.Token.Line;
			string name = DoParseIdentifier("for the method's name");
			string[] args = DoParseFormalArgs();
			
			Contract.Assert(locals.Count == 0, "locals was not reset");
			foreach (string arg in args)
			{
				if (locals.Contains(arg))
					throw new ParserException(line, "The '{0}' argument name appears twice.", arg);
				locals.Add(arg);
			}

			var statements = DoParseStatements(locals);
			locals.Clear();
			
			DoParseKeyword("end", string.Format("to end the {0} method", name));

			return new Method(name, args, statements);
		}
		
		// MethodStatement := CallExpression		# should be of type Void
		private Statement DoParseMethodStatement(List<string> locals)
		{
			int line = m_scanner.Token.Line;
			Expression expr = DoParseCallExpression(locals);
					
			return new MethodCall(line, expr);
		}
		
		// ParenthesizedExpression = '(' Expression ')'
		private Expression DoParseParenthesizedExpression(List<string> locals)
		{
			Expression result = DoParseExpression(locals);

			DoParsePunct(")", "to close the parenthesized expression");
			
			return result;
		}
		
		// OrExpression = AndExpression ('or' AndExpression)*
		private Expression DoParseOrExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParseAndExpression(locals);
			
			while (m_scanner.Token.IsKeyword("or"))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression rhs = DoParseAndExpression(locals);
				result = new InvokeMethod(line, result, "op_LogicalOr", new Expression[]{rhs});
			}

			return result;
		}
		
		// PrimaryExpression = Literal | Local | MethodCall | ParenthesizedExpression
		private Expression DoParsePrimaryExpression(List<string> locals)
		{
			Expression result = DoParseLiteral(locals);
			
			if (result == null)
			{
				if (m_scanner.Token.Kind == TokenKind.Identifier)
				{
					int line = m_scanner.Token.Line;
					if (locals.Contains(m_scanner.Token.Text()))
					{
						result = new Local(line, m_scanner.Token.Text());
						m_scanner.Advance();
					}
					else
					{
						result = DoParseMethodCall(line, locals, new SelfLiteral(line));
					}
				}
				else if (m_scanner.Token.IsPunct("("))
				{
					m_scanner.Advance();
					result = DoParseParenthesizedExpression(locals);
				}
				else
				{
					throw new ParserException(m_scanner.Token.Line, "Expected a literal, call, or parenthesized expression, but found '{0}'", m_scanner.Token.Text());
				}
			}
			
			return result;
		}
		
		// PropertyDeclaration := 'define' 'property' Identifier Statement* 'end'
		private Method DoParsePropertyDeclaration(List<string> locals)
		{
			string name = DoParseIdentifier("for the property name");
			
			var statements = DoParseStatements(locals);
			DoParseKeyword("end", string.Format("to end the {0} property", name));

			return new Method("get_" + name, new string[0], statements);
		}
		
		private void DoParsePunct(string symbol, string reason)
		{
			if (!m_scanner.Token.IsPunct(symbol))
				throw new ParserException(m_scanner.Token.Line, "Expected a '{0}' {1}, but found '{2}'", symbol, reason, m_scanner.Token.Text());
			
			m_scanner.Advance();
		}
		
		// RelationalExpression = AddExpresion ('is' Identifier)?
		private Expression DoParseRelationalExpression(List<string> locals)
		{
			Expression result;
			
			result = DoParseAddExpression(locals);
			
			if (m_scanner.Token.IsKeyword("is"))
			{
				int line = m_scanner.Token.Line;
				m_scanner.Advance();
				
				Expression type = new TypeName(line, DoParseIdentifier("for the is expression"));
				result = new InvokeMethod(line, result, "op_IsType", new Expression[]{type});
			}
			
			return result;
		}
		
		// ReturnStatement := 'return' Expression
		private Statement DoParseReturn(int line, List<string> locals)
		{
			Expression value = DoParseExpression(locals);
			
			return new Return(line, value);
		}
		
		// SequenceLiteral := '[' ExpressionList? ']'
		private Expression DoParseSequenceLiteral(int line, List<string> locals)
		{
			Expression[] elements = new Expression[0];
			
			if (!m_scanner.Token.IsPunct("]"))
			{
				elements = DoParseExpressionList(locals);
			}
			
			DoParsePunct("]", "to end the sequence literal");

			return new SequenceLiteral(line, elements);
		}
		
		// Statement := IfStatement | ForStatement | LetStatement | MethodStatement | ReturnStatement
		private Statement DoParseStatement(List<string> locals)
		{
			Statement result;
			
			int line = m_scanner.Token.Line;
			if (m_scanner.Token.IsKeyword("if"))
			{
				m_scanner.Advance();
				result = DoParseIf(line, locals);
			}
			else if (m_scanner.Token.IsKeyword("for"))
			{
				m_scanner.Advance();
				result = DoParseFor(line, locals);
			}
			else if (m_scanner.Token.IsKeyword("let"))
			{
				m_scanner.Advance();
				result = DoParseLet(line, locals);
			}
			else if (m_scanner.Token.IsKeyword("return"))
			{
				m_scanner.Advance();
				result = DoParseReturn(line, locals);
			}
			else
			{
				result = DoParseMethodStatement(locals);
			}

			return result;
		}

		private Statement[] DoParseStatements(List<string> locals)
		{			
			var statements = new List<Statement>();
			
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsKeyword("end") && !m_scanner.Token.IsKeyword("elif") && !m_scanner.Token.IsKeyword("else"))
			{
				statements.Add(DoParseStatement(locals));
			}

			return statements.ToArray();
		}
		
		// "foo #{e} bar #{f}" => "foo " + e + " bar " + f
		private Expression DoParseStringLiteral(List<string> locals)
		{
			int line = m_scanner.Token.Line;
			string text = m_scanner.Token.Text();				// TODO: should change this back to not including the delimiters
			text = text.Substring(1, text.Length - 2);
			m_scanner.Advance();
			
			if (text.Contains("#{") && text.Contains("}"))
			{
				string expr = DoInterpolateString(line, text);	// "foo " + (e) + " bar " + (f) + ""
				Parser parser = new Parser(expr, line);
				return parser.DoParseAddExpression(locals);
			}
			else
			{
				return new StringLiteral(line, text);
			}
		}
		
		// UnaryExpression := 'not'? CallExpression
		private Expression DoParseUnaryExpression(List<string> locals)
		{
			Expression result;
			
			bool notted = false;
			int line = m_scanner.Token.Line;
			if (m_scanner.Token.IsKeyword("not"))
			{
				m_scanner.Advance();
				notted = true;
			}

			result = DoParseCallExpression(locals);
			
			if (notted)
				result = new InvokeMethod(line, result, "op_LogicalComplement", new Expression[0]);
			
			return result;
		}

		// WhenExpression := OrExpression 'when' OrExpression 'else' OrExpression
		private Expression DoParseWhenExpression(List<string> locals, Expression trueValue, int line)
		{
			Expression predicate = DoParseOrExpression(locals);
			DoParseKeyword("else", "for the when expression");
			Expression falseValue = DoParseOrExpression(locals);
			
			return new When(line, predicate, trueValue, falseValue);
		}		
		#endregion

		#region Fields
		private Scanner m_scanner;
		#endregion
	} 
}
