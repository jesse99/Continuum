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
	
	internal static class TypeMirrorExtensions
	{
		public static MethodMirror FindMethod(this TypeMirror type, string name, int numArgs)
		{
//		Console.WriteLine("checking: {0}", type.FullName); Console.Out.Flush();
			MethodMirror method = type.GetMethods().FirstOrDefault(
				m => m.Name == name && m.GetParameters().Length == numArgs);
				
			if (method == null && type.BaseType != null)
				method = FindMethod(type.BaseType, name, numArgs);
				
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

		public static string TypeName(this Value v)
		{
			string result;
			
			do
			{
				var primitive = v as PrimitiveValue;
				if (primitive != null)
				{
					if (primitive.Value == null)
						result = "null";
					else
						result = primitive.Value.GetType().FullName;
					break;
				}
				
				var obj = v as ObjectMirror;
				if (obj != null)
				{
					result = obj.Type.FullName;
					break;
				}
				
				var strct = v as StructMirror;
				if (strct != null)
				{
					result = strct.Type.FullName;
					break;
				}
				
				Console.Error.WriteLine("bad type: {0}", v.GetType());
				result = string.Empty;
			}
			while (false);
			
			int index = result.IndexOf("[[");	// strip off stuff like "[[System.Int32, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"
			if (index > 0)
				result = result.Substring(0, index);
			
			return result;
		}
	}
}
