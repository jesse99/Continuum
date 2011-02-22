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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Debugger
{
	internal static class CustomAttributeDataMirrorExtensions
	{
		// Modeled after code in MonoDevelop.
		public static T Instantiate<T>(this CustomAttributeDataMirror attr)
		{
			var args = new List<object>();
			foreach (CustomAttributeTypedArgumentMirror arg in attr.ConstructorArguments)
			{
				object val = arg.Value;
				if (val is TypeMirror)
				{
					// The debugger attributes that take a type as parameter of the constructor have
					// a corresponding constructor overload that takes a type name. We'll use that
					// constructor because we can't load target types in the debugger process.
					// So what we do here is convert the Type to a string.
					var tm = (TypeMirror) val;
					val = tm.FullName + ", " + tm.Assembly.ManifestModule.Name;
				}
				else if (val is EnumMirror)
				{
					EnumMirror em = (EnumMirror) val;
					val = em.Value;
				}
				args.Add(val);
			}
			
			Type type = typeof(T);
			object at = Activator.CreateInstance(type, args.ToArray());
			foreach (CustomAttributeNamedArgumentMirror arg in attr.NamedArguments)
			{
				object val = arg.TypedValue.Value;
				string postFix = string.Empty;
				if (arg.TypedValue.ArgumentType == typeof(Type))
					postFix = "TypeName";
				if (arg.Field != null)
					type.GetField(arg.Field.Name + postFix).SetValue(at, val);
				else if (arg.Property != null)
					type.GetProperty(arg.Property.Name + postFix).SetValue(at, val, null);
			}
			
			return (T) at;
		}
	}
		
	internal static class FieldMirrorExtensions
	{
		public static T GetAttribute<T>(this FieldInfoMirror field, bool inherit = true) where T : class
		{
			string fullName = typeof(T).FullName;
			
			foreach (CustomAttributeDataMirror attr in field.GetCustomAttributes(inherit))
			{
				if (attr.Constructor.DeclaringType.FullName == fullName)
					return attr.Instantiate<T>();
			}
			
			return null;
		}
		
		// Name should be something like System.Runtime.CompilerServices.CompilerGeneratedAttribute.
		public static bool HasCustomAttribute(this FieldInfoMirror field, string name)
		{
			name += ":.ctor";
			foreach (CustomAttributeDataMirror custom in field.GetCustomAttributes(false))
			{
				if (custom.Constructor.FullName.Contains(name))
					return true;
			}
			
			return false;
		}
		
		public static bool ShouldDisplay(this FieldInfoMirror field)
		{
			bool should = false;
			
			if (!field.HasCustomAttribute("System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
			{
				// All MonoMac objects have a bunch of static selectors that we don't want to display.
				if (!DoIsMonoMacSelector(field))
				{
					// They also have a bunch of fields for exports.
					if (!(field.Name.StartsWith("__mt_") && field.Name.EndsWith("_var")))
					{
						// And some annoying fields repeated in base classes.
						if (!(field.Name == "class_ptr"))
						{
							var attr = field.GetAttribute<DebuggerBrowsableAttribute>();
							should = attr == null || attr.State != DebuggerBrowsableState.Never;
						}
					}
				}
			}
			
			return should;
		}
		
		public static bool ShouldEvaluate(this FieldInfoMirror field)
		{
			bool should = true;
			
			if (field.HasCustomAttribute("System.ThreadStaticAttribute"))	// TODO: soft debugger crashes if we try to display these
			{
				should = false;
			}
			
			return should;
		}
		
		#region Private Methods
		private static bool DoIsMonoMacSelector(FieldInfoMirror field)
		{
			if (field.IsStatic)
			{
				if (field.Name.Length >= 3 && field.Name.StartsWith("sel") && char.IsUpper(field.Name[3]))
				{
					if (field.FieldType.FullName == "System.IntPtr")
						return true;
				}
			}
			
			return false;
		}
		#endregion
	}
	
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
	
	internal static class PropertyMirrorExtensions
	{
		public static T GetAttribute<T>(this PropertyInfoMirror prop, bool inherit = true) where T : class
		{
			string fullName = typeof(T).FullName;
			
			foreach (CustomAttributeDataMirror attr in prop.GetCustomAttributes(inherit))
			{
				if (attr.Constructor.DeclaringType.FullName == fullName)
					return attr.Instantiate<T>();
			}
			
			return null;
		}
		
		[Pure]
		public static bool HasSimpleGetter(this PropertyInfoMirror prop)
		{
			MethodMirror method = prop.GetGetMethod(true);
			return method != null && method.GetParameters().Length == 0 && !method.IsAbstract;
		}
		
		public static bool ShouldDisplay(this PropertyInfoMirror prop)
		{
			bool should = false;
			
			if (HasSimpleGetter(prop))
			{
				var attr1 = prop.GetAttribute<DebuggerBrowsableAttribute>();
				if (attr1 == null || attr1.State != DebuggerBrowsableState.Never)
				{
					var attr2 = prop.GetAttribute<DebuggerHiddenAttribute>();
					if (attr2 == null)
					{
						var attr3 = prop.GetAttribute<DebuggerNonUserCodeAttribute>();
						if (attr3 == null)
						{
							if (prop.Name != "ClassHandle")	// MonoMac repeated prop
							{
								should = true;
							}
						}
					}
				}
			}
			
			return should;
		}
		
		public static bool ShouldEvaluate(this PropertyInfoMirror prop)
		{
			bool should = true;
			
			// What we should do here is check for MonoMac.Foundation.ExportAttribute on
			// the getter, but MethodMirror doesn't provide a way to get the custom attributes...
			string name = "__mt_" + prop.Name + "_var";
			FieldInfoMirror field = prop.DeclaringType.GetField(name);
			if (field != null)
			{
				should = false;
			}
			
			return should;
		}
	}
	
	internal static class TypeMirrorExtensions
	{
		// numArgs does not include the this argument.
		public static MethodMirror FindMethod(this TypeMirror type, string name, int numArgs)
		{
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
		
//		public static IEnumerable<MethodMirror> GetAllMethods(this TypeMirror type)
//		{
//			while (type != null)
//			{
//				foreach (MethodMirror method in type.GetMethods())
//				{
//					yield return method;
//				}
//				
//				type = type.BaseType;
//			}
//		}
		
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
		
		public static T GetAttribute<T>(this TypeMirror type, bool inherit = true) where T : class
		{
			string fullName = typeof(T).FullName;
			
			if (type.FullName != "MonoMac.Foundation.NSObject+MonoMac_Disposer")
			{
				foreach (CustomAttributeDataMirror attr in type.GetCustomAttributes(inherit))
				{
					if (attr.Constructor.DeclaringType.FullName == fullName)
						return attr.Instantiate<T>();
				}
			}
			
			return null;
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
			
			IEnumerable<FieldInfoMirror> fields =
				from f in type.GetFields()
					where f.Name == fieldName
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
			if (prop != null && !prop.ShouldEvaluate())
				return null;
			
			if (prop != null)
				result = prop.GetGetMethod(true);
			
			if (result == null && type.BaseType != null)
				result = ResolveProperty(type.BaseType, propName);
			
			return result;
		}
	}
	
	internal static class ThreadMirrorExtensions
	{
		// Returns the TypeMirror associated with typeName. If the debuggee has not
		// loaded the type it will be force loaded.
		public static TypeMirror GetType(this ThreadMirror thread, string typeName)
		{
			// Loosely based on code from MonoDevelop.
			TypeMirror result = DoGetType(thread, typeName);
			
			if (result == null)
			{
				TypeMirror type = (TypeMirror) thread.Type.GetTypeObject().Type;
				MethodMirror method = type.FindMethod("GetType", 1);
				try
				{
					if (method != null)
					{
						StringMirror str = thread.Domain.CreateString(typeName);
						type.InvokeMethod(thread, method, new Value[]{str});
						
						result = DoGetType(thread, typeName);
					}
					else
					{
						Log.WriteLine(TraceLevel.Error, "Debugger", "LoadType> couldn't find a GetType method in {0}", thread.Type.GetTypeObject());
					}
				}
				catch (Exception e)
				{
					Log.WriteLine(TraceLevel.Error, "Debugger", "LoadType> {0}", e.Message);
				}
			}
			
			return result;
		}
		
		#region Private Methods
		// Returns null if the type is not found or not loaded.
		private static TypeMirror DoGetType(ThreadMirror thread, string name)
		{
			// Based on code from MonoDevelop.
			int i = name.IndexOf(',');
			if (i != -1)
			{
				// Find first comma outside brackets
				int nest = 0;
				for (int n=0; n<name.Length; n++)
				{
					char c = name [n];
					if (c == '[')
						nest++;
					else if (c == ']')
						nest--;
					else if (c == ',' && nest == 0) {
						name = name.Substring(0, n).Trim();
						break;
					}
				}
			}
			
			foreach (AssemblyMirror assembly in thread.Domain.GetAssemblies())
			{
				TypeMirror type = assembly.GetType(name, false, false);
				if (type != null)
					return type;
			}
			
			return null;
		}
		#endregion
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
		
//		public static bool IsType(this Value v, string typeName)
//		{
//			TypeMirror type = null;
//			var primitive = v as PrimitiveValue;
//			if (primitive != null)
//			{
//				if (typeName == "System.Object" || typeName == "System.ValueType")
//					return true;
//				
//				if (primitive.Value != null)
//					return primitive.Value.GetType().FullName == typeName;
//			}
//			
//			var obj = v as ObjectMirror;
//			if (obj != null)
//			{
//				type = obj.Type;
//			}
//			
//			var strct = v as StructMirror;
//			if (strct != null)
//			{
//				type = strct.Type;
//			}
//			
//			bool result = false;
//			while (!result && type != null)
//			{
//				result = type.FullName == typeName;
//				type = type.BaseType;
//			}
//			
//			return result;
//		}
		
		public static string ToDisplayText(this Value theValue, ThreadMirror thread, string type = null)
		{
			string result = string.Empty;
			
			if (theValue is PrimitiveValue)
			{
				var value = theValue as PrimitiveValue;
				
				if (value.Value == null)
					result = "null";
				else
					result = value.Value.ToString();
			}
			else if (theValue is EnumMirror)
			{
				var value = theValue as EnumMirror;
				
				result = DoGetDisplayText(thread, value, value.Type.GetAttribute<DebuggerDisplayAttribute>());
				if (result == null)
				{
					if (value.Type.Assembly.Metadata == null)
						value.Type.Assembly.Metadata = AssemblyCache.Load(value.Type.Assembly.Location, false);
					
					if (value.Type.Metadata != null)
						result = CecilExtensions.ArgToString(value.Type.Metadata, value.Value, false, false);
					else
						result = value.StringValue;
				}
			}
			else if (theValue is StringMirror)
			{
				var value = theValue as StringMirror;
				
				result = DoStringToText(value.Value);
			}
			
			if (result.Length == 0 && theValue is StructMirror)
			{
				var value = theValue as StructMirror;
				
				if (type == null)
					type = value.Type.FullName;
				
				if (type == "System.IntPtr" || type == "System.UIntPtr")
				{
					MethodMirror method = value.Type.FindMethod("ToString", 1);
					
					Value format = thread.Domain.CreateString("X4");		// TODO: not 32-bit safe
					Value v = value.InvokeMethod(thread, method, new Value[]{format}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					
					result = "0x" + s.Value;
				}
				else if (type == "System.Nullable`1")
				{
					MethodMirror method = value.Type.FindMethod("ToString", 0);
					
					Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					result = s.Value.Length > 0 ? s.Value : "null";
				}
				else if (type == "System.DateTime" || type == "System.TimeSpan")
				{
					MethodMirror method = value.Type.FindMethod("ToString", 0);
					
					Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					result = s.Value;
				}
				else
				{
					result = DoGetDisplayText(thread, value, value.Type.GetAttribute<DebuggerDisplayAttribute>());
					if (result == null)
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
								Value v = new InvokeMethod().Invoke(thread, value, "ToString");
//								Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
								StringMirror s = (StringMirror) v;
								result = s.Value;
							}
						}
					}
				}
			}
			else if (result.Length == 0 && theValue is ObjectMirror)
			{
				var value = theValue as ObjectMirror;
				
				result = DoGetDisplayText(thread, value, value.Type.GetAttribute<DebuggerDisplayAttribute>());
				if (result == null)
				{
					if (value.Type.IsType("System.MulticastDelegate"))
					{
						Value mv = EvalMember.Evaluate(thread, value, "Method");
						if (!mv.IsNull())
						{
							ObjectMirror mo = (ObjectMirror) mv;
							MethodMirror method = mo.Type.FindMethod("ToString", 0);
							
							Value v = mo.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
							result = ((StringMirror) v).Value;
						}
						else
						{
							result = "null";
						}
					}
					else
					{
						MethodMirror method = value.Type.FindMethod("ToString", 0);
						if (method.DeclaringType.FullName != "System.Object")
						{
							Value v = value.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
							StringMirror s = (StringMirror) v;
							result = s.Value;
						}
					}
				}
			}
			
			return result ?? string.Empty;
		}
		
		[Pure]
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
		
		public static string Interpolate(ThreadMirror thread, Value target, string text)
		{
			return ms_displayRe.Replace(text, m => DoInterpolateDisplayText(thread, target, m));
		}
		
		#region Private Methods
		private static string DoInterpolateDisplayText(ThreadMirror thread, Value target, Match match)
		{
			Value result = EvalMember.Evaluate(thread, target, match.Groups[1].Value);
			return result.ToDisplayText(thread);
		}
		
		// attr.Value will contain text to be displayed for the target. The text may contain a name
		// enclosed in curly braces. The name should be that of a field, property, or method.
		private static string DoGetDisplayText(ThreadMirror thread, Value target, DebuggerDisplayAttribute attr)
		{
			string text = null;
			
			if (attr != null && attr.Value != null)
				text = ms_displayRe.Replace(attr.Value, m => DoInterpolateDisplayText(thread, target, m));
			
			return text;
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
		
		#region Fields 
		private static Regex ms_displayRe = new Regex(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}");
		#endregion
	}
}
