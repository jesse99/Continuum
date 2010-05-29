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

namespace Debugger
{
	internal static class ValueTextOverloads
	{
		// Note that we need this overload or the ObjectMirror overload blows up.
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, ArrayMirror item)
		{
			return string.Empty;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, bool item)
		{
			if (item)
				return "true";
			else
				return "false";
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Byte item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X1");
			else
				return item.ToString("N0");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, char item)
		{
			if (item > 0x7F && VariableController.ShowUnicode)
				return "'" + new string(item, 1) + "'";
			else
				return "'" + CharHelpers.ToText(item) + "'";
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Double item)
		{
			if (VariableController.ShowThousands)
				return item.ToString("N");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, EnumMirror item)
		{
			return CecilExtensions.ArgToString(item.Type.Metadata, item.Value, false, false);
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int16 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X2");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int32 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X4");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int64 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X8");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, object item)
		{
			return item.ToString();
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, ObjectMirror item)
		{
			string result = string.Empty;
			
			MethodMirror method = item.Type.FindMethod("ToString", 0);
			if (method.DeclaringType.FullName != "System.Object")
			{
				// TODO: use a helper to do the invoke
				Value v = item.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				StringMirror s = (StringMirror) v;
				result = s.Value;
			}
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, PrimitiveValue item)
		{
			if (item.Value == null)
				return "null";
			else
				return ValueText.Invoke(thread, item, item.Value);
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, SByte item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X1");
			else
				return item.ToString("N0");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Single item)
		{
			if (VariableController.ShowThousands)
				return item.ToString("N");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, StructMirror item)
		{
			string result = string.Empty;
			
			if (item.Type.FullName == "System.IntPtr" || item.Type.FullName == "System.UIntPtr")
			{
				MethodMirror method = item.Type.FindMethod("ToString", 1);
				StringMirror s = thread.Domain.CreateString("X8");		// TODO: not 64-bit ready
				Value v = item.InvokeMethod(thread, method, new Value[]{s}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				s = (StringMirror) v;
				result = "0x" + s.Value;
			}
			else if (item.TypeName() == "System.Nullable`1")
			{
				Value v = EvalMember.Evaluate(thread, item, "HasValue");
				PrimitiveValue p = v as PrimitiveValue;
				if (p != null && p.Value.ToString() == "False")
				{
					result = "null";
				}
				else
				{
					MethodMirror method = item.Type.FindMethod("ToString", 0);
					v = item.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					result = s.Value;
				}
			}
			else
			{
				MethodMirror method = item.Type.FindMethod("ToString", 0);
				if (method.DeclaringType.FullName != "System.ValueType")
				{
					if (item.Type.IsPrimitive)
					{
						// Boxed primitive (we need this special case or InvokeMethod will hang).
						if (item.Fields.Length > 0 && (item.Fields[0] is PrimitiveValue))
							result = ((PrimitiveValue)item.Fields[0]).Value.Stringify();
					}
					else
					{
						// TODO: use a helper to do the invoke
						Value v = item.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						StringMirror s = (StringMirror) v;
						result = s.Value;
					}
				}
			}
			
			return result;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, StringMirror item)
		{
			return item.Value;
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt16 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X2");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt32 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X4");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt64 item)
		{
			if (VariableController.ShowHex)
				return "0x" + item.ToString("X8");
			else if (VariableController.ShowThousands)
				return item.ToString("N0");
			else
				return item.ToString("G");
		}
	}
}
