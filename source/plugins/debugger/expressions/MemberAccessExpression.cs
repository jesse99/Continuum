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
	internal sealed class MemberAccessExpression : Expression
	{
		public MemberAccessExpression(Expression lhs, Expression rhs)
		{
			Contract.Requires(lhs != null);
			Contract.Requires(rhs != null);
			
			m_lhs = lhs;
			m_rhs = rhs;
		}
		
		public override ExtendedValue Evaluate(StackFrame frame)
		{
			Value target = m_lhs.Evaluate(frame).Value;
			string name = m_rhs.ToString();
			
			ExtendedValue result;
			if (target is ObjectMirror || target is StructMirror)
			{
				Value value = EvalMember.Evaluate(frame, target, name);
				if (value != null)
					result = new ExtendedValue(value);
				else if (target is ObjectMirror)
					throw new Exception(string.Format("Couldn't find a field or property for {0}.{1}", ((ObjectMirror) target).Type.FullName, name));
				else
					throw new Exception(string.Format("Couldn't find a field or property for {0}.{1}", ((StructMirror) target).Type.FullName, name));
			}
			else
			{
				throw new Exception("Member access target should be an object, struct, or type name, not a " + target.GetType());
			}
			
			return result;
		}
		
		public override string ToString()
		{
			return string.Format("{0}.{1}", m_lhs, m_rhs);
		}
		
		#region Fields
		private Expression m_lhs;
		private Expression m_rhs;
		#endregion
	}
}
