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
using Mono.Cecil;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

using Debug = Debugger;

namespace Debugger
{
	internal static class GetItemOverloads
	{
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, ArrayMirror value)
		{
			if (value.IsCollected)
			{
				return new Item(0, "garbage collected", value.Type.FullName);
			}
			else
			{
				string type = DoGetFullerName(parent, key, value.Type);
				return new Item(value.Length, string.Empty, type);
			}
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, bool value)
		{
			if (value)
				return new Item(0, "true", value.GetType().FullName);
			else
				return new Item(0, "false", value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Byte value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X1"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, LiveStackFrame value)
		{
			IEnumerable<FieldInfoMirror> fields = value.Method.DeclaringType.GetAllFields();
			int delta = fields.Any() ?  1 : 0;
			LocalVariable[] locals = value.Method.GetLocals();
			return new Item(locals.Length + delta, string.Empty, "LiveStackFrame");
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, char value)
		{
			if (value > 0x7F && VariableController.ShowUnicode)
				return new Item(0, "'" + new string(value, 1) + "'", value.GetType().FullName);
			else
				return new Item(0, "'" + CharHelpers.ToText(value) + "'", value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Double value)
		{
			if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
//		[GetItem.Overload]
//		public static Item GetItem(ThreadMirror thread, object parent, int key, EnumerableValue value)
//		{
//			value.Reload(thread);
//			return new Item(value.Length, string.Empty, value.Type.FullName);
//		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, EnumMirror value)
		{
			if (value.Type.Assembly.Metadata == null)
				value.Type.Assembly.Metadata = AssemblyCache.Load(value.Type.Assembly.Location, false);
			
			string text;
			if (value.Type.Metadata != null)
				text = CecilExtensions.ArgToString(value.Type.Metadata, value.Value, false, false);
			else
				text = value.StringValue;
			return new Item(0, text, value.Type.FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, InstanceValue value)
		{
			return new Item(value.Length, value.GetText(thread), value.Type.FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Int16 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X2"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Int32 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X4"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Int64 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X8"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, object value)
		{
			if (value != null)
				return new Item(0, value.ToString(), value.GetType().FullName);
			else
				return new Item(0, "null", string.Empty);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, ObjectMirror value)
		{
			Item item = new Item();
			
			if (value.IsCollected)
			{
				item = new Item(0, "garbage collected", value.Type.FullName);
			}
			else
			{
				if (!DoProcessSpecialObject(thread, value, ref item))
				{
					int numChildren = 0;
					numChildren += value.Type.GetAllProperties().Count(p => p.HasSimpleGetter());
					numChildren += value.Type.GetAllFields().Count(f => !f.Name.Contains("__BackingField"));
					
					string text = string.Empty;
					MethodMirror method = value.Type.FindMethod("ToString", 0);
					if (method.DeclaringType.FullName != "System.Object")
					{
						Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						StringMirror s = (StringMirror) v;
						text = s.Value;
					}
					
//					if (value.Type.FullName.StartsWith("System.Collections") && value.Type.FindMethod("GetEnumerator", 0) != null)	// TODO: better to use Is(ICollection) but TypeMirror does not expose interfaces
//						numChildren = 1;
					
					string type = DoGetFullerName(parent, key, value.Type);
					item = new Item(numChildren, text, type);
				}
			}
			
			return item;
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, PrimitiveValue value)
		{
			if (value.Value != null)
			{
				Item item = Debug::GetItem.Invoke(thread, parent, key, value.Value);
				Contract.Assert(item.Count == 0);
				return new Item(0, item.Text, item.Type);
			}
			else
			{
				string type = DoGetFullerName(parent, key, null);
				return new Item(0, "null", type);
			}
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, SByte value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X1"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, Single value)
		{
			if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, StringMirror value)
		{
			if (value.IsCollected)
				return new Item(0, "garbage collected", value.Type.FullName);
			else
				return new Item(value.Value.Length, DoStringToText(value.Value), "System.String");
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, StructMirror value)
		{
			Item item = new Item();
			
			string type = DoGetFullerName(parent, key, value.Type);
			if (!DoProcessSpecialStruct(thread, type, value, ref item))
			{
				string text = string.Empty;
				
				MethodMirror method = value.Type.FindMethod("ToString", 0);
				if (method.DeclaringType.FullName != "System.ValueType")
				{
					if (value.Type.IsPrimitive)
					{
						// Boxed primitive (we need this special case or InvokeMethod will hang).
						if (value.Fields.Length > 0 && (value.Fields[0] is PrimitiveValue))
							text = ((PrimitiveValue)value.Fields[0]).Value.Stringify();
					}
					else
					{
						Value v = new InvokeMethod().Invoke(thread, value, "ToString");
//						Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						StringMirror s = (StringMirror) v;
						text = s.Value;
					}
				}
				
				int numChildren = 0;
				numChildren += value.Type.GetAllProperties().Count(p => p.HasSimpleGetter());
				numChildren += value.Type.GetAllFields().Count(f => !f.Name.Contains("__BackingField"));
				item = new Item(numChildren, text, type);
			}
			
			return item;
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, TypeValue value)
		{
			return new Item(value.Length, value.GetText(thread), value.Type.FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, UInt16 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X2"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, UInt32 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X4"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		[GetItem.Overload]
		public static Item GetItem(ThreadMirror thread, object parent, object key, UInt64 value)
		{
			if (VariableController.ShowHex)
				return new Item(0, "0x" + value.ToString("X8"), value.GetType().FullName);
			else if (VariableController.ShowThousands)
				return new Item(0, value.ToString("N0"), value.GetType().FullName);
			else
				return new Item(0, value.ToString("G"), value.GetType().FullName);
		}
		
		#region Private Methods
		public static bool DoProcessSpecialObject(ThreadMirror thread, ObjectMirror value, ref Item item)
		{
			if (value.Type.IsType("System.MulticastDelegate"))
			{
				string text = "null";
				Value mv = EvalMember.Evaluate(thread, value, "Method");
				if (!mv.IsNull())
				{
					ObjectMirror mo = (ObjectMirror) mv;
					MethodMirror method = mo.Type.FindMethod("ToString", 0);
					
					Value v = mo.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					text = ((StringMirror) v).Value;
				}
				
				item = new Item(4, text, value.TypeName());
				return true;
			}
			
			return false;
		}
		
		public static bool DoProcessSpecialStruct(ThreadMirror thread, string type, StructMirror value, ref Item item)
		{
			if (type == "System.IntPtr" || type == "System.UIntPtr")
			{
				MethodMirror method = value.Type.FindMethod("ToString", 1);
				
				Value format = thread.Domain.CreateString("X4");		// TODO: not 32-bit safe
				Value v = value.InvokeMethod(thread, method, new Value[]{format}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				StringMirror s = (StringMirror) v;
				
				item = new Item(0, "0x" + s.Value, type);
				return true;
			}
			else if (type == "System.Nullable`1")
			{
				MethodMirror method = value.Type.FindMethod("ToString", 0);
				
				Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				StringMirror s = (StringMirror) v;
				string text = s.Value.Length > 0 ? s.Value : "null";
				
				item = new Item(0, text, type);
				return true;
			}
			else if (type == "System.DateTime" || type == "System.TimeSpan")
			{
				MethodMirror method = value.Type.FindMethod("ToString", 0);
				
				Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				StringMirror s = (StringMirror) v;
				
				item = new Item(0, s.Value, type);
				return true;
			}
			
			return false;
		}
		
		private static string DoGetFullerName(object parent, object key, TypeMirror value)
		{
			string type;
			
			if (value != null && value.Assembly.Metadata == null)
				value.Assembly.Metadata = AssemblyCache.Load(value.Assembly.Location, false);
				
			// If the value is null then we have to use the declaration type. If the declared type
			// is the same as the actual type then we also want to use the declaration type
			// because it will be the one with template arguments filled in. TODO: the later
			// deosn't work because we always get a TypeDefinition from the declaration (as
			// opposed to a GenericInstanceType. However MD has this working somehow...)
			if (!(parent == null && key == null))
			{
				TypeMirror declaredType = DeclaredType.Invoke(parent, key);
				if (value == null || (declaredType != null && declaredType.Metadata != null && declaredType.Metadata.HasGenericParameters && declaredType.Metadata.Name == value.Name))
					value = declaredType;
			}
			
			if (value.Metadata != null)
				type = value.Metadata.FullName;
			else
				type = value.FullName;
			
			return type;
		}
		
		private static string DoStringToText(string str)
		{
			var builder = new System.Text.StringBuilder(str.Length + 2);
			
			builder.Append('"');
			foreach (char ch in str)
			{
				if (ch > 0x7F && VariableController.ShowUnicode)
					builder.Append(ch);
				else if (ch == '\'')
					builder.Append(ch);
				else if (ch == '"')
					builder.Append("\\\"");
				else
					builder.Append(CharHelpers.ToText(ch));
					
				if (builder.Length > 256)
				{
					builder.Append(Shared.Constants.Ellipsis);
					break;
				}
			}
			builder.Append('"');
			
			return builder.ToString();
		}
		#endregion
	}
}
