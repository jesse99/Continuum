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
using System.Collections.Generic;
using System.Diagnostics;

namespace CsRefactor.Script
{
	// Class used for user defined methods.
	internal sealed class Method
	{
		public Method(string name, string[] argNames, Statement[] statements)	
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			Trace.Assert(argNames != null, "argNames is null");
			Trace.Assert(statements != null, "statements is null");
			
			Name = name;
			m_argNames = argNames;
			m_statements = statements;
		}
		
		public string Name {get; private set;}
		
		public object Evaluate(int line, Context context, object[] args)
		{
			object result = null;
			
			if (args.Length != m_argNames.Length)
				throw new EvaluateException(line, "{0} method takes {1}, not {2}.", Name, DoGetArgsStr(m_argNames.Length), DoGetArgsStr(args.Length));
			
			int depth = context.PushLocals();
			if (depth > 256)
				throw new EvaluateException(line, "Method calls have recursed more than 256 times");
			
			for (int i = 0; i < args.Length; ++i)
				context.AddLocal(m_argNames[i], args[i]);
				
			foreach (Statement statement in m_statements)
			{
				try
				{
					result = statement.Evaluate(context);
				}
				catch (ReturnException e)
				{
					result = e.Result;
					break;
				}
			}
			
			context.PopLocals();
			
			return result;
		}
				
		public void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append("define ");
			buffer.Append(Name);
			buffer.Append('(');
			
			for (int i = 0; i < m_argNames.Length; ++i)
			{
				buffer.Append(m_argNames[i]);
				if (i + 1 < m_argNames.Length)
					buffer.Append(", ");
			}
			
			buffer.AppendLine(")");
			
			foreach (Statement statement in m_statements)
			{
				statement.Print(buffer, "\t");
			}
		}
		
		#region Private Methods
		private string DoGetArgsStr(int count)
		{
			if (count == 1)
				return "1 argument";
			else
				return count + " arguments";
		}
		#endregion
		
		#region Fields
		private string[] m_argNames;
		private Statement[] m_statements;
		#endregion
	} 
}
