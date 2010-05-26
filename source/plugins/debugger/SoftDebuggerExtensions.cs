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
using System.Collections.Generic;
using System.Linq;

namespace Debugger
{
	internal static class MethodMirrorExtensions
	{
		// MethodMirror.FullName doesn't include parameters (with Mono 2.6) so
		// we'll roll our own. MonoDevelop also includes values for the arguments,
		// but that seems a little too busy to me (note that if we change our minds
		//  on this we'll need to pass in a fresh copy of the stack).
		public static string GetFullerName(this MethodMirror method)
		{
			var builder = new System.Text.StringBuilder();
			
			builder.Append(DoGetTypeName(method.ReturnType));
			builder.Append(' ');
			
			builder.Append(method.DeclaringType.Name);
			builder.Append('.');
			builder.Append(method.Name);
			
			builder.Append('(');
			ParameterInfoMirror[] args = method.GetParameters();
			for (int i = 0; i < args.Length; ++i)
			{
				builder.Append(DoGetTypeName(args[i].ParameterType));
				builder.Append(' ');
				builder.Append(args[i].Name);
				
				if (i + 1 < args.Length)
					builder.Append(", ");
			}
			builder.Append(')');
			
			return builder.ToString();
		}
		
		#region Private Methods
		private static string DoGetTypeName(TypeMirror type)
		{
			string result = CsHelpers.GetAliasedName(type.FullName);
			if (result == type.FullName)
				result = type.Name;
				
			return result;
		}
		#endregion
	}
	
	internal static class StackFrameExtensions
	{
		// Returns true if the two stack frames are the same.
		public static bool Matches(this StackFrame lhs, StackFrame rhs)
		{
			bool matches = false;
			
			if (lhs == rhs)					// this will use reference equality (as of mono 2.6)
			{
				matches = true;
			}
			else if (lhs != null && rhs != null)
			{
				if (lhs.Thread.Id == rhs.Thread.Id)			// note that Address can change after a GC
					if (lhs.Method.MetadataToken == rhs.Method.MetadataToken)
						if (lhs.Method.FullName == rhs.Method.FullName)	// this is kind of expensive, but we can't rely on just the metadata token (we need the assembly as well which we can't always get)
							matches = true;
			}
			
			return matches;
		}
		
		public static bool Matches(StackFrame[] lhs, StackFrame[] rhs)
		{
			bool matches = false;
			
			if (ReferenceEquals(lhs, rhs))
			{
				matches = true;
			}
			else if (lhs != null && rhs != null && lhs.Length == rhs.Length)
			{
				matches = Contract.ForAll(0, lhs.Length, i => lhs[i].Matches(rhs[i]));
			}
			
			return matches;
		}
	}
	
	internal static class TypeMirrorExtensions
	{
		public static MethodMirror FindMethod(this TypeMirror type, string name, int numArgs)
		{
			MethodMirror method = type.GetMethods().FirstOrDefault(
				m => m.Name == name && m.GetParameters().Length == numArgs);
				
			if (method == null && type.BaseType != null)
				method = FindMethod(type.BaseType, name, numArgs);
				
			return method;
		}
		
		public static MethodMirror FindLastMethod(this TypeMirror type, string name, int numArgs)
		{
			MethodMirror method = null;
			
			while (type != null)
			{
				MethodMirror result = type.GetMethods().FirstOrDefault(
					m => m.Name == name && m.GetParameters().Length == numArgs);
				if (result != null)
					method = result;
				
				type = type.BaseType;
			}
			
			return method;
		}
		
		public static IEnumerable<FieldInfoMirror> GetAllFields(this TypeMirror type)
		{
			while (type != null)
			{
				foreach (FieldInfoMirror field in type.GetFields())
				{
					yield return field;
				}
				
				type = type.BaseType;
			}
		}
		
		public static IEnumerable<MethodMirror> GetAllMethods(this TypeMirror type)
		{
			while (type != null)
			{
				foreach (MethodMirror method in type.GetMethods())
				{
					yield return method;
				}
				
				type = type.BaseType;
			}
		}
		
		public static IEnumerable<PropertyInfoMirror> GetAllProperties(this TypeMirror type)
		{
			while (type != null)
			{
				foreach (PropertyInfoMirror prop in type.GetProperties())
				{
					yield return prop;
				}
				
				type = type.BaseType;
			}
		}
		
		// Note that this does not check interfaces because TypeMirror does not currently
		// expose them.
		public static bool IsType(this TypeMirror type, string fullName)
		{
			Contract.Requires(!string.IsNullOrEmpty(fullName));
			
			if (type != null)
			{
				if (type.FullName == fullName)
					return true;
					
				return type.BaseType.IsType(fullName);
			}
			
			return false;
		}
		
		public static FieldInfoMirror ResolveField(this TypeMirror type, string fieldName)
		{
			FieldInfoMirror field = null;
			
			string autoName = "<" + fieldName + ">";	// auto-props look like "<Command>k_BackingField"
			IEnumerable<FieldInfoMirror> fields =
				from f in type.GetFields()
					where f.Name == fieldName || f.Name.StartsWith(autoName)
				select f;
			
			if (fields.Any())
				field = fields.First();
			else if (type.BaseType != null)
				field = ResolveField(type.BaseType, fieldName);
			
			return field;
		}
		
		public static MethodMirror ResolveProperty(this TypeMirror type, string propName)
		{
			MethodMirror result = null;
			
			PropertyInfoMirror prop = type.GetProperty(propName);
			if (prop != null)
				result = prop.GetGetMethod(true);
			
			if (result == null && type.BaseType != null)
				result = ResolveProperty(type.BaseType, propName);
			
			return result;
		}
	}
	
	internal static class ValueExtensions
	{
		public static bool IsNull(this Value v)
		{
			var primitive = v as PrimitiveValue;
			if (primitive != null)
			{
				return primitive.Value == null;
			}
			
			return false;
		}
		
		public static bool IsType(this Value v, string typeName)
		{
			TypeMirror type = null;
			var primitive = v as PrimitiveValue;
			if (primitive != null)
			{
				if (typeName == "System.Object" || typeName == "System.ValueType")
					return true;
				
				if (primitive.Value != null)
					return primitive.Value.GetType().FullName == typeName;
			}
			
			var obj = v as ObjectMirror;
			if (obj != null)
			{
				type = obj.Type;
			}
			
			var strct = v as StructMirror;
			if (strct != null)
			{
				type = strct.Type;
			}
			
			bool result = false;
			while (!result && type != null)
			{
				result = type.FullName == typeName;
				type = type.BaseType;
			}
			
			return result;
		}
		
		public static string Stringify(this Value value, ThreadMirror thread)
		{
			string text = string.Empty;
			
			do
			{
				if (value == null)		// this will happen for NullValueItem
				{
					text = "null";
					break;
				}
				
				// this has to appear first
				var obj = value as ObjectMirror;
				if (obj != null)
				{
					if (obj.IsCollected)
					{
						text = "garbage collected";
					}
					else if (!(value is StringMirror) && !obj.Type.IsArray)
					{
						MethodMirror method = obj.Type.FindMethod("ToString", 0);
						if (method.DeclaringType.FullName != "System.Object")
						{
							Value v = obj.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
							StringMirror s = (StringMirror) v;
							text = s.Value;
						}
					}
					if (text.Length > 0)
						break;
				}
				
				var enm = value as EnumMirror;
				if (enm != null)
				{
					text = CecilExtensions.ArgToString(enm.Type.Metadata, enm.Value, false, false);
					break;
				}
				
				var strct = value as StructMirror;
				if (strct != null)
				{
					if (strct.Type.FullName == "System.IntPtr" || strct.Type.FullName == "System.UIntPtr")
					{
						MethodMirror method = strct.Type.FindMethod("ToString", 1);
						StringMirror s = thread.Domain.CreateString("X8");		// TODO: not 64-bit ready
						Value v = strct.InvokeMethod(thread, method, new Value[]{s}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						s = (StringMirror) v;
						text = "0x" + s.Value;
					}
					else
					{
						MethodMirror method = strct.Type.FindMethod("ToString", 0);
						if (method.DeclaringType.FullName != "System.ValueType")
						{
							if (strct.Type.IsPrimitive)
							{
								// Boxed primitive (we need this special case or InvokeMethod will hang).
								if (strct.Fields.Length > 0 && (strct.Fields[0] is PrimitiveValue))
									return ((PrimitiveValue)strct.Fields[0]).Value.Stringify();
							}
							else
							{
								Value v = strct.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
								StringMirror s = (StringMirror) v;
								text = s.Value;
							}
						}
					}
					if (text.Length > 0)
						break;
				}
				
				var primitive = value as PrimitiveValue;
				if (primitive != null)
				{
					text = primitive.Value.Stringify();
					break;
				}
				
				var str = value as StringMirror;
				if (str != null)
				{
					text = DoStringToText(str.Value);
					break;
				}
			}
			while (false);
			
			return text;
		}
		
		public static string TypeName(this Value v)
		{
			var primitive = v as PrimitiveValue;
			if (primitive != null)
			{
				if (primitive.Value == null)
					return "null";
				else
					return primitive.Value.GetType().FullName;
			}
			
			var obj = v as ObjectMirror;
			if (obj != null)
			{
				return obj.Type.FullName;
			}
			
			var strct = v as StructMirror;
			if (strct != null)
			{
				return strct.Type.FullName;
			}
			
			Console.Error.WriteLine("bad type: {0}", v.GetType());
			return string.Empty;
		}
		
		#region Private Methods
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
					builder.Append(Constants.Ellipsis);
					break;
				}
			}
			builder.Append('"');
			
			return builder.ToString();
		}
		#endregion
	}
}
