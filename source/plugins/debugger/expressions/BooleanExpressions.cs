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
		
		public override ExtendedValue Evaluate(StackFrame frame)
		{
			ExtendedValue left = m_lhs.Evaluate(frame);
			ExtendedValue right = m_rhs.Evaluate(frame);
			
			if (left.IsNull && right.IsNull)
			{
				switch (m_op)
				{
					case "<=":
					case ">=":
					case "==":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(true));
					
					case "<":
					case ">":
					case "!=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(false));
					
					default:
						Contract.Assert(false, "bad op: " + m_op);
						break;
				}
			}
			else if (left.IsNull)
			{
				switch (m_op)
				{
					case "<":
					case "<=":
					case "!=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(true));
					
					case ">":
					case ">=":
					case "==":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(false));
					
					default:
						Contract.Assert(false, "bad op: " + m_op);
						break;
				}
			}
			else if (right.IsNull)
			{
				switch (m_op)
				{
					case ">=":
					case ">":
					case "!=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(true));
					
					case "<":
					case "<=":
					case "==":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(false));
					
					default:
						Contract.Assert(false, "bad op: " + m_op);
						break;
				}
			}
			else
			{
				object leftValue = DoGetPrimitive(left);
				object rightValue = DoGetPrimitive(right);
				Type type = DoFindCommonType(leftValue, rightValue);
				
				IComparable lhs = (IComparable) Convert.ChangeType(leftValue, type);
				IComparable rhs = (IComparable) Convert.ChangeType(rightValue, type);
				
				int x = lhs.CompareTo(rhs);
				switch (m_op)
				{
					case "<":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x < 0));
					
					case "<=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x <= 0));
					
					case ">=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x >= 0));
					
					case ">":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x > 0));
					
					case "==":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x == 0));
					
					case "!=":
						return new ExtendedValue(frame.VirtualMachine.CreateValue(x != 0));
					
					default:
						Contract.Assert(false, "bad op: " + m_op);
						break;
				}
			}
			
			return null;
		}
		
		protected override bool DoEvaluate(bool lhs, bool rhs)
		{
			Contract.Assert(false, "shouldn't be called");
			return false;
		}
		
		private object DoGetPrimitive(ExtendedValue value)
		{
			if (value.Value is PrimitiveValue)
			{
				var pv = (PrimitiveValue) value.Value;
				return pv.Value;
			}
			else if (value.Value is StringMirror)
			{
				var sv = (StringMirror) value.Value;
				return sv.Value;
			}
			else
			{
				throw new Exception("Expected a PrimitiveValue or StringMirror not " + value.Value.GetType());
			}
		}
		
		private Type DoFindCommonType(object lhs, object rhs)
		{
			if (lhs is decimal || rhs is decimal)
				return typeof(decimal);
			
			else if (DoIsFloat(lhs) || DoIsFloat(rhs))
				return typeof(double);
			
			else if (DoIsNegativeInt(lhs) || DoIsNegativeInt(rhs))
				return typeof(long);
			
			else if (DoIsNonNegativeInt(lhs) || DoIsNonNegativeInt(rhs))
				return typeof(ulong);
				
			else if (lhs.GetType() == rhs.GetType())
				if (lhs is IComparable)
					return lhs.GetType();
				else
					throw new Exception(string.Format("{0} is not an IComparable.", lhs.GetType()));
				
			throw new Exception(string.Format("Can't compare {0} and {1}.", lhs.GetType(), rhs.GetType()));
		}
		
		private bool DoIsFloat(object value)
		{
			Type type = value.GetType();
			return type == typeof(float) || type == typeof(double);
		}
		
		private bool DoIsNegativeInt(object value)
		{
			Type type = value.GetType();
			if (type == typeof(SByte) || type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64))
			{
				long temp = (long) Convert.ChangeType(value, typeof(long));
				return temp < 0;
			}
			
			return false;
		}
		
		private bool DoIsNonNegativeInt(object value)
		{
			Type type = value.GetType();
			if (type == typeof(SByte) || type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64))
			{
				long temp = (long) Convert.ChangeType(value, typeof(long));
				return temp >= 0;
			}
			else
			{
				return type == typeof(char) || type == typeof(Byte) || type == typeof(UInt16) || type == typeof(UInt32) || type == typeof(UInt64);
			}
		}
	}
}
