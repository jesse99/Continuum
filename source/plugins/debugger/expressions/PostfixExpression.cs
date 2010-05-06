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

using Mono.Debugger.Soft;
using MObjc.Helpers;
using System;
using System.Linq;

namespace Debugger
{
	internal sealed class PostfixExpression : Expression
	{
		public PostfixExpression(Identifier identifier, PostfixOperator[] suffixes)
		{
			m_identifier = identifier;
			m_suffixes = suffixes;
		}
		
		public override ExtendedValue Evaluate(StackFrame frame)
		{
			ExtendedValue result = m_identifier.Evaluate(frame);
			
			foreach (PostfixOperator op in m_suffixes)
			{
				op.Target = result;
				result = op.Evaluate(frame);
			}
			
			return result;
		}
		
		public override string ToString()
		{
			var builder = new System.Text.StringBuilder();
			
			builder.Append(m_identifier.ToString());
			
			foreach (PostfixOperator op in m_suffixes)
			{
				builder.Append(op.ToString());
			}
			
			return builder.ToString();
		}
		
		#region Fields
		private Identifier m_identifier;
		private PostfixOperator[] m_suffixes;
		#endregion
	}
}
