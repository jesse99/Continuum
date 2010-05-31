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

using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;

#if UNUSED
namespace Debugger
{
	// Debugger type overloads.
	internal static class ValueTextOverloads2
	{
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, ArrayMirror owner, int value)
		{
			Value v = owner[value];
			
			string result = ValueText.Invoke(thread, owner, v);
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, CachedStackFrame owner, LocalVariable value)
		{
			Value v = owner.GetValue(value);
			
			string result = ValueText.Invoke(thread, owner, v);
			
			return result;
		}
		
		// Note that we need this overload or the ObjectMirror overload blows up.
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, ArrayMirror value)
		{
			return string.Empty;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, EnumMirror value)
		{
			return CecilExtensions.ArgToString(value.Type.Metadata, value.Value, false, false);
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, ObjectMirror value)
		{
			string result = string.Empty;
			
			MethodMirror method = value.Type.FindMethod("ToString", 0);
			if (method.DeclaringType.FullName != "System.Object")
			{
				// TODO: use a helper to do the invoke
				Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				StringMirror s = (StringMirror) v;
				result = s.Value;
			}
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, PrimitiveValue value)
		{
			if (value.Value == null)
				return "null";
			else
				return ValueText.Invoke(thread, value, value.Value);
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, StructMirror value)
		{
			string result = string.Empty;
			
			if (value.Type.FullName == "System.IntPtr" || value.Type.FullName == "System.UIntPtr")
			{
				MethodMirror method = value.Type.FindMethod("ToString", 1);
				StringMirror s = thread.Domain.CreateString("X8");		// TODO: not 64-bit ready
				Value v = value.InvokeMethod(thread, method, new Value[]{s}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				s = (StringMirror) v;
				result = "0x" + s.Value;
			}
			else if (value.TypeName() == "System.Nullable`1")
			{
				Value v = EvalMember.Evaluate(thread, value, "HasValue");
				PrimitiveValue p = v as PrimitiveValue;
				if (p != null && p.Value.ToString() == "False")
				{
					result = "null";
				}
				else
				{
					MethodMirror method = value.Type.FindMethod("ToString", 0);
					v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					result = s.Value;
				}
			}
			else
			{
				MethodMirror method = value.Type.FindMethod("ToString", 0);
				if (method.DeclaringType.FullName != "System.ValueType")
				{
					if (value.Type.IsPrimitive)
					{
						// Boxed primitive (we need this special case or InvokeMethod will hang).
						if (value.Fields.Length > 0 && (value.Fields[0] is PrimitiveValue))
							result = ((PrimitiveValue)value.Fields[0]).Value.Stringify();
					}
					else
					{
						// TODO: use a helper to do the invoke
						Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						StringMirror s = (StringMirror) v;
						result = s.Value;
					}
				}
			}
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, StringMirror value)
		{
			return value.Value;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, ObjectMirror owner, FieldInfoMirror value)
		{
			Value v = EvalMember.Evaluate(thread, owner, value.Name);
			
			string result = ValueText.Invoke(thread, value, v);
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, StructMirror owner, FieldInfoMirror value)
		{
			Value v = owner[value.Name];
			
			string result = ValueText.Invoke(thread, value, v);
			
			return result;
		}
	}
}
#endif
