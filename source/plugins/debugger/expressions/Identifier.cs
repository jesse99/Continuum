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
		
		public override object Evaluate(StackFrame frame)
		{
			Value value = DoGetValue(frame);
			object result = DoGetObject(value);
			return result;
		}
		
		public override string ToString()
		{
			return m_name;
		}
		
		#region Private Methods
		private object DoGetObject(Value value)
		{
			var pv = value as PrimitiveValue;
			if (pv != null)
			{
				return pv.Value;
			}
			
			var sv = value as StringMirror;
			if (sv != null)
			{
				if (sv.IsCollected)
					throw new Exception(m_name + " has been garbage collected");
				return sv.Value;
			}
			
			throw new Exception("Conditional expressions may only use primitive and string types and " + m_name + " is an " + value.GetType());
		}
		
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
			
			// And finally fields.
			Value thisPtr = frame.GetThis();
			if (thisPtr is ObjectMirror)
				result = DoGetField((ObjectMirror) thisPtr);
				
			else if (thisPtr is StructMirror)
				result = DoGetField((StructMirror) thisPtr);
				
			if (result == null)
				throw new Exception("Couldn't find a local, argument, or field matching " + m_name);
				
			return result;
		}
		
		private Value DoGetField(ObjectMirror mirror)
		{
			Value result = null;
			
			FieldInfoMirror field = mirror.Type.GetFields().FirstOrDefault(f => f.Name == m_name);
			if (field != null)
			{
				if (field.IsStatic)
					result = mirror.Type.GetValue(field);
				else
					result = mirror.GetValue(field);
			}
			
			return result;
		}
		
		private Value DoGetField(StructMirror mirror)
		{
			Value result = null;
			
			FieldInfoMirror[] fields = mirror.Type.GetFields();
			for (int i = 0; i < fields.Length && result == null; ++i)
			{
				FieldInfoMirror field = fields[i];
				if (field.Name == m_name)
				{
					if (field.IsStatic)
						result = mirror.Type.GetValue(field);
					else
						result = mirror.Fields[i];
				}
			}
			
			return result;
		}
		#endregion
		
		#region Fields
		private string m_name;
		#endregion
	}
}
