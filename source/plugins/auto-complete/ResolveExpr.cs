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

using Gear.Helpers;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoComplete
{
	// Used to resolve expressions into types. Note that this will not resolve all
	// expressions, but should resolve the most common expressions (notably
	// chained method calls).
	internal sealed class ResolveExpr
	{
		public ResolveExpr(ITargetDatabase db, CsGlobalNamespace globals, ResolveName nameResolver)
		{
			m_globals = globals;
			m_nameResolver = nameResolver;
			m_typeResolver = new ResolveType(db);
			m_memberResolver = new ResolveMembers(db);
		}
		
		// Offset should point just after the expression to resolve. May return null.
		public ResolvedTarget Resolve(CsMember context, string text, int offset)
		{
			Contract.Requires(offset >= 0, "offset is negative");
			Contract.Requires(offset <= text.Length, "offset is too large");
			Profile.Start("ResolveExpr::Resolve");
			
			ResolvedTarget result = null;
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving expression");
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "text: '{0}'", text.Substring(Math.Max(0, offset - 48), offset - Math.Max(0, offset - 48)).EscapeAll());
			
			string expr = DoFindExpr(text, offset);
			Log.WriteLine("AutoComplete", "expression: '{0}'", expr);
			if (!string.IsNullOrEmpty(expr))
				result = m_nameResolver.Resolve(expr);
			
			if (result == null)
			{
				string[] operands = DoGetOperands(expr);
		
				int first = 0;
				result = DoGetOperandType(context, operands, ref first);
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "operand type: {0}", result);
				
				for (int i = first; i < operands.Length; ++i)
				{
					string operand = operands[i];
					ResolvedTarget old = result;
					result = DoResolveOperand(context, result, operand);
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "{0}::{1} resolved to {2}", old, operand, result);
					if (result == null)
						break;
				}
			}
			
			Log.WriteLine("AutoComplete", "---- expression {0} -> {1}", expr, result);
			
			Profile.Stop("ResolveExpr::Resolve");
			return result;
		}
		
		#region Private Methods
		// Handle cases like `System.String.Empty.`.
		private ResolvedTarget DoGetOperandType(CsMember context, string[] operands, ref int first)
		{
			ResolvedTarget result = null;
			
			int i = first;
			string type = string.Empty;
			while (i < operands.Length - 1)
			{
				type += type.Length == 0 ? operands[i] : ("." + operands[i]);
				ResolvedTarget candidate = m_typeResolver.Resolve(type, context, m_globals, false, true);;
				++i;
				if (candidate != null)
				{
					result = candidate;
					first = i;
				}
			}
			
			return result;
		}
		
		private string DoFindExpr(string text, int offset)
		{
			int i = offset;
			while (i - 1 >= 0)
			{
				if (text[i - 1] == '.')
				{
					--i;
				}
				else if (CsHelpers.CanContinueIdentifier(text[i - 1]))
				{
					--i;
				}
				else if (text[i - 1] == ')')
				{
					const int MaxMethodCallLength = 256;
					
					int first = Math.Max(i - MaxMethodCallLength, -1);
					i = TextHelpers.ReverseSkipBraces(text, i - 1, first, "()", "[]", "{}");	// technically we don't need to match square and curly braces, but that allows us to identify more syntax errors
					if (i > first && i > 0 && DoCanContinueIdentifier(text, i - 1))
						--i;					// skip the opening brace
					else
						break;
				}
				else if (text[i - 1] == '\'')
				{
					i -= 2;
					while (i >= 0)
					{
						if (text[i] == '\'')
							if (i == 0 || text[i - 1] != '\\')
								break;
							
						--i;
					}
				}
				else if (text[i - 1] == '"')		// TODO: probably should have verbatim strings as well
				{
					i -= 2;
					while (i >= 0)
					{
						if (text[i] == '"')
							if (i == 0 || text[i - 1] != '\\')
								break;
							
						--i;
					}
				}
				else
					break;
			}
			
			string result = string.Empty;
			if (i >= 0)
				if (CsHelpers.CanStartIdentifier(text[i]) || text[i] == '\'' || text[i] == '"')
					result = text.Substring(i, offset - i);
				
			return result;
		}
		
		private bool DoCanContinueIdentifier(string text, int i)
		{
			while (i >= 0 && char.IsWhiteSpace(text[i]))
				--i;
				
			return i >= 0 ? CsHelpers.CanContinueIdentifier(text[i]) : false;
		}
		
		// name.call(x, y).name
		private string[] DoGetOperands(string expr)
		{
			var operands = new List<string>();
			
			int i = 0;
			int j = 0;
			while (j < expr.Length)
			{
				if (expr[j] == '.')
				{
					operands.Add(expr.Substring(i, j - i));
					i = ++j;
				}
				else if ("([{".Contains(expr[j]))
				{
					j = TextHelpers.SkipBraces(expr, j, "()", "[]", "{}");
					if (j == expr.Length)
						return new string[0];
				}
				else if (expr[j] == '\'')
				{
					++j;
					while (j < expr.Length)
					{
						if (expr[j] == '\'')
							if (expr[j - 1] != '\\')
								break;
							
						++j;
					}
					if (j == expr.Length || expr[j] != '\'')
						return new string[0];
					++j;
				}
				else if (expr[j] == '"')
				{
					++j;
					while (j < expr.Length)
					{
						if (expr[j] == '"')
							if (expr[j - 1] != '\\')
								break;
						
						++j;
					}
					if (j == expr.Length || expr[j] != '"')
						return new string[0];
					++j;
				}
				else
					++j;
			}
			
			if (i < j)
				operands.Add(expr.Substring(i, j - i));
			
			return operands.ToArray();
		}
		
		private ResolvedTarget DoResolveOperand(CsMember context, ResolvedTarget target, string operand)
		{
			ResolvedTarget result = null;
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "    target: {0}, operand: '{1}'", target, operand);
			
			// Handle locals and args.
			if (target == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying local operand");
				result = m_nameResolver.Resolve(operand);
			}
			
			// Handle this calls (this will work for static calls too).
			if (target == null && result == null)
			{
				target = m_nameResolver.Resolve("this");
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "new target: {0}", target);
			}
			
			if (target != null && result == null)
			{
				int numArgs = DoGetNumArgs(operand);
				int i = operand.IndexOfAny(new char[]{'(', ' ', '\t', '\n', '\r'});
				string name = i >= 0 ? operand.Substring(0, i) : operand;
				
				if (name != null)
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying {0}({1} args) item operand", name, numArgs);
					
					Item[] candidates = m_memberResolver.Find(context, target, m_globals, name, numArgs);
					IEnumerable<Item> items = from c in candidates where c.Type != null select c;
					if (items.Any())
					{
						if (items.All(m => m.Type == items.First().Type))
						{
							result = m_typeResolver.Resolve(items.First().Type, context, m_globals, true, false);
						}
						else
						{
							Log.WriteLine("AutoComplete", "{0} has an ambiguous return type:", name);
							foreach (Item item in items)
							{
								Log.WriteLine("AutoComplete", "    {0} {1}", item.Type, item);
							}
						}
					}
				}
			}
			
			if (result == null)
				Log.WriteLine("AutoComplete", "failed to resolve {0}::{1}", target, operand);
			
			return result;
		}
		
		private int DoGetNumArgs(string operand)
		{
			int count = 0;
			
			int i = operand.IndexOfAny(new char[]{'('});
			if (i > 0)
			{
				++i;
				while (i < operand.Length && char.IsWhiteSpace(operand[i]))
					++i;
					
				if (operand[i] != ')')
					++count;
				
				while (i < operand.Length)
				{
					if (operand[i] == ',')
					{
						++count;
						++i;
					}
					else if ("([{".Contains(operand[i]))
					{
						i = TextHelpers.SkipBraces(operand, i, "()", "[]", "{}");
						if (i == operand.Length)
							count = -1;
					}
					else if (operand[i] == '<')
					{
						int j = TextHelpers.SkipBraces(operand, i, "<>");
						if (j == operand.Length)
							++i;
						else
							i = j + 1;
					}
					else
						++i;
				}
			}
			
			return count;
		}
		#endregion
		
		#region Fields
		private CsGlobalNamespace m_globals;
		private ResolveName m_nameResolver;
		private ResolveMembers m_memberResolver;
		private ResolveType m_typeResolver;
		#endregion
	}
}
