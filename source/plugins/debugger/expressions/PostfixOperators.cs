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
	internal sealed class MemberAccessExpression : PostfixOperator
	{
		public MemberAccessExpression(string memberName)
		{
			Contract.Requires(!string.IsNullOrEmpty(memberName));
			
			m_memberName = memberName;
		}
		
		public override ExtendedValue Evaluate(LiveStackFrame frame)
		{
			ExtendedValue result;
			if (Target.Value is ObjectMirror || Target.Value is StructMirror)
			{
				Value value = EvalMember.Evaluate(frame.Thread, Target.Value, m_memberName);
				if (value != null)
					result = new ExtendedValue(value);
				else if (Target.Value is ObjectMirror)
					throw new Exception(string.Format("Couldn't find a field or property for {0}.{1}", ((ObjectMirror) Target.Value).Type.FullName, m_memberName));
				else
					throw new Exception(string.Format("Couldn't find a field or property for {0}.{1}", ((StructMirror) Target.Value).Type.FullName, m_memberName));
			}
			else
			{
				throw new Exception("Member access target should be an object, struct, or type name, not a " + Target.Value.GetType());
			}
			
			return result;
		}
		
		public override string ToString()
		{
			return string.Format(".{0}", m_memberName);
		}
		
		#region Fields
		private string m_memberName;
		#endregion
	}
	
	internal sealed class SubscriptExpression : PostfixOperator
	{
		public SubscriptExpression(int index)
		{
			Contract.Requires(index >= 0);
			
			m_index = index;
		}
		
		public override ExtendedValue Evaluate(LiveStackFrame frame)
		{
			if (Target.Value is ArrayMirror)
			{
				var array = (ArrayMirror) Target.Value;
				Value value = array[m_index];
				return new ExtendedValue(value);
			}
			else if (Target.Value is StringMirror)
			{
				var str = (StringMirror) Target.Value;
				Value value = frame.VirtualMachine.CreateValue(str.Value[m_index]);
				return new ExtendedValue(value);
			}
			else
			{
				throw new Exception("Expected an ArrayMirror or StringMirror, not " + Target.Value.GetType());
			}
		}
		
		public override string ToString()
		{
			return string.Format("[{0}]", m_index);
		}
		
		#region Fields
		private int m_index;
		#endregion
	}
}
