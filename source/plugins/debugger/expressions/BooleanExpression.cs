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

namespace Debugger
{
	internal abstract class BooleanExpression : Expression
	{
		protected BooleanExpression(Expression lhs, Expression rhs, string op)
		{
			Contract.Requires(lhs != null);
			Contract.Requires(rhs != null);
			Contract.Requires(!string.IsNullOrEmpty(op));
			
			m_lhs = lhs;
			m_rhs = rhs;
			m_op = op;
		}
		
		public override ExtendedValue Evaluate(StackFrame frame)
		{
			bool lhs = m_lhs.Evaluate(frame).Get<bool>();
			bool rhs = m_rhs.Evaluate(frame).Get<bool>();
			bool result = DoEvaluate(lhs, rhs);
			return new ExtendedValue(frame.VirtualMachine.CreateValue(result));
		}
		
		public override string ToString()
		{
			return string.Format("{0} {1} {2}", m_lhs, m_op, m_rhs);
		}
		
		protected abstract bool DoEvaluate(bool lhs, bool rhs);
		
		#region Fields
		protected Expression m_lhs;
		protected Expression m_rhs;
		protected string m_op;
		#endregion
	}
}
