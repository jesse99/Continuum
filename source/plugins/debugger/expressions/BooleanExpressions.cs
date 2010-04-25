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
	internal sealed class AndExpression : BooleanExpression
	{
		public AndExpression(Expression lhs, Expression rhs) : base(lhs, rhs, "&&")
		{
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			return lhs && rhs;
		}
	}
	
	internal class EqualityExpression : BooleanExpression
	{
		public EqualityExpression(Expression lhs, Expression rhs) : base(lhs, rhs, "==")
		{
		}
		
		protected EqualityExpression(Expression lhs, Expression rhs, string op) : base(lhs, rhs, op)
		{
		}
		
		public override object Evaluate(StackFrame frame)
		{
			object lhs = m_lhs.Evaluate(frame);
			object rhs = m_rhs.Evaluate(frame);
		
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			bool result = lhs.Equals(rhs);
			return result;
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			Contract.Assert(false, "shouldn't be called");
			return false;
		}
	}
	
	internal sealed class ExclusiveOrExpression : BooleanExpression
	{
		public ExclusiveOrExpression(Expression lhs, Expression rhs) : base(lhs, rhs, "^")
		{
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			return lhs ^ rhs;
		}
	}
	
	internal class InequalityExpression : EqualityExpression
	{
		public InequalityExpression(Expression lhs, Expression rhs) : base(lhs, rhs, "!=")
		{
		}
		
		public override object Evaluate(StackFrame frame)
		{
			bool result = (bool) base.Evaluate(frame);
			return !result;
		}
	}
	
	internal sealed class OrExpression : BooleanExpression
	{
		public OrExpression(Expression lhs, Expression rhs) : base(lhs, rhs, "||")
		{
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			return lhs || rhs;
		}
	}
	
	internal sealed class RelationalExpression : BooleanExpression
	{
		public RelationalExpression(Expression lhs, Expression rhs, string op) : base(lhs, rhs, op.Trim())
		{
		}
		
		public override object Evaluate(StackFrame frame)
		{
			IComparable lhs = DoGetValue(frame, m_lhs);
			IComparable rhs = DoGetValue(frame, m_rhs);
			
			int x = lhs.CompareTo(rhs);
			switch (m_op)
			{
				case "<":
					return x < 0;
				
				case "<=":
					return x <= 0;
				
				case ">=":
					return x >= 0;
				
				case ">":
					return x > 0;
				
				default:
					Contract.Assert(false, "bad op: " + m_op);
					break;
			}
			
			return null;
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			Contract.Assert(false, "shouldn't be called");
			return false;
		}
		
		private IComparable DoGetValue(StackFrame frame, Expression expr)
		{
			object value = expr.Evaluate(frame);
			if (value is IComparable)
				return (IComparable) value;
				
			else if (value == null)
				throw new Exception(string.Format("Expected an IComparable but {0} is null", expr));
				
			else
				throw new Exception(string.Format("Expected an IComparable but {0} is a {1}", expr, value.GetType()));
		}
	}
}
