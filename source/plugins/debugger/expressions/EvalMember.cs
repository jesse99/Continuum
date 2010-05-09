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
	internal static class EvalMember
	{
		// Target should be a ObjectMirror or a StructMirror. Name should be the name of a
		// property (e.g. Count) or field (eg Empty). Returns null if a suitable member cannot 
		// be found.
		public static Value Evaluate(ThreadMirror thread, Value target, string name)
		{
			Contract.Requires(thread != null, "thread is null");
			Contract.Requires(target != null, "target is null");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Value result = null;
			
			// Try fields,
			if (target is ObjectMirror)
				result = DoGetField((ObjectMirror) target, name);
				
			else if (target is StructMirror)
				result = DoGetField((StructMirror) target, name);
				
			// and then properties.
			if (result == null)
			{
				if (target is ObjectMirror)
					result = DoEvaluateProperty(thread, (ObjectMirror) target, name);
					
				else if (target is StructMirror)
					result = DoEvaluateProperty(thread, (StructMirror) target, name);
			}
			
			return result;
		}
		
		#region Private Methods
		private static Value DoGetField(ObjectMirror mirror, string name)
		{
			Value result = null;
			
			FieldInfoMirror field = mirror.Type.ResolveField(name);
			if (field != null)
			{
				if (field.IsStatic)
					result = field.DeclaringType.GetValue(field);
				else
					result = mirror.GetValue(field);
			}
			
			return result;
		}
		
		private static Value DoGetField(StructMirror mirror, string name)
		{
			Value result = null;
			
			FieldInfoMirror field = mirror.Type.ResolveField(name);
			if (field != null)
			{
				if (field.IsStatic)
					result = mirror.Type.GetValue(field);
				else
					result = mirror[name];
			}
			
			return result;
		}
		
		private static Value DoEvaluateProperty(ThreadMirror thread, ObjectMirror obj, string name)
		{
			Value result = null;
			
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method != null)
			{
				result = obj.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
			}
			
			return result;
		}
		
		private static Value DoEvaluateProperty(ThreadMirror thread, StructMirror obj, string name)
		{
			Value result = null;
			
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method != null)
			{
				if (method.IsStatic)
					result = method.DeclaringType.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				else
					result = obj.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
			}
			
			return result;
		}
		#endregion
	}
}
