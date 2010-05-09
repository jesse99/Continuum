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
	internal sealed class Identifier : Expression
	{
		public Identifier(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			m_name = name;
		}
		
		public override ExtendedValue Evaluate(StackFrame frame)
		{
			Value value = DoGetValue(frame);
			return new ExtendedValue(value);
		}
		
		public override string ToString()
		{
			return m_name;
		}
		
		#region Private Methods
		private Value DoGetValue(StackFrame frame)
		{
			Value result = null;
			
			// First try locals.
			LocalVariable[] locals = frame.Method.GetLocals();
			LocalVariable local = locals.FirstOrDefault(l => l.Name == m_name);
			if (local != null)
			{
				result = frame.GetValue(local);
			}
			
			// Then parameters.
			ParameterInfoMirror parm = frame.Method.GetParameters().FirstOrDefault(p => p.Name == m_name);
			if (parm != null)
			{
				result = frame.GetValue(parm);
			}
			
			// And finally fields and properties.
			if (result == null)
			{
				Value thisPtr = frame.GetThis();
				result = EvalMember.Evaluate(frame.Thread, thisPtr, m_name);
			}
			
			if (result == null)
				throw new Exception("Couldn't find a local, argument, field, or property named " + m_name);
				
			return result;
		}
		#endregion
		
		#region Fields
		private string m_name;
		#endregion
	}
}
