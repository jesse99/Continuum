// Copyright (C) 2009 Jesse Jones
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

using Gear.Helpers;
using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public static class CecilExtensions
	{
		public static string ToText(this CustomAttribute attr, bool includeNamespace)
		{
			var args = new List<string>();
			
			for (int i = 0; i < attr.ConstructorArguments.Count; ++i)
			{
				TypeReference ptype = attr.ConstructorArguments[i].Type;
				object pvalue = attr.ConstructorArguments[i].Value;
				
				args.Add(ArgToString(ptype, pvalue, includeNamespace, true));
			}
			
			TypeDefinition type = attr.Constructor.DeclaringType.Resolve();
			if (type != null)
			{
				foreach (CustomAttributeNamedArgument d in attr.Properties)
				{
					if (type.Properties.Count(p => p.Name == d.Name) == 1)
					{
						TypeReference ptype = d.Argument.Type;
						object pvalue = d.Argument.Value;
						
						args.Add(string.Format("{0} = {1}", d.Name, ArgToString(ptype, pvalue, includeNamespace, true)));
					}
					else
						args.Add(string.Format("{0} = {1}", d.Name, ArgToString(d.Argument.Value)));
				}
				
				foreach (CustomAttributeNamedArgument d in attr.Fields)
				{
					TypeReference ptype = d.Argument.Type;
					object pvalue = d.Argument.Value;
					
					args.Add(string.Format("{0} = {1}", d.Name, ArgToString(ptype, pvalue, includeNamespace, true)));
				}
			}
			else
			{
				foreach (CustomAttributeNamedArgument d in attr.Properties)
				{
					args.Add(string.Format("{0} = {1}", d.Name, ArgToString(d.Argument.Value)));
				}
				
				foreach (CustomAttributeNamedArgument d in attr.Fields)
				{
					args.Add(string.Format("{0} = {1}", d.Name, ArgToString(d.Argument.Value)));
				}
			}
			
			string name;
			if (includeNamespace)
				name = attr.Constructor.DeclaringType.FullName;
			else
				name = attr.Constructor.DeclaringType.Name;
			if (name.EndsWith("Attribute"))
				name = name.Substring(0, name.Length - "Attribute".Length);
			
			if (!includeNamespace && args.Count == 0)
				return string.Format("[{0}]", name);
			else
				return string.Format("[{0}({1})]", name, string.Join(", ", args.ToArray()));
		}
		
		public static bool HasAttribute(this Collection<CustomAttribute> attrs, string name)
		{
			foreach (CustomAttribute attr in attrs)
			{
				string fullName = attr.Constructor.DeclaringType.FullName;
				if (fullName == name)
					return true;
			}
			
			return false;
		}
		
		public static bool IsExtension(this MethodDefinition method)
		{
			string name = "System.Runtime.CompilerServices.ExtensionAttribute";
			return method.HasCustomAttributes && method.CustomAttributes.HasAttribute(name);
		}
		
		public static string GetParameterModifier(this MethodReference method, int index)
		{
			var builder = new System.Text.StringBuilder();
			
			MethodDefinition md = method as MethodDefinition;
			if (index == 0 && md != null && md.IsExtension())
				builder.Append("this ");
			
			ParameterDefinition p = method.Parameters[index];
			if (p.HasCustomAttributes && p.CustomAttributes.HasAttribute("System.ParamArrayAttribute"))
				builder.Append("params ");
			
			string typeName = p.ParameterType.FullName;
			if (typeName.EndsWith("&"))
			{
				if (p.IsOut)
					builder.Append("out ");
				else
					builder.Append("ref ");
			}
			
			return builder.ToString();
		}
		
		public static string ToText(this SecurityDeclaration sec, bool includeNamespace)
		{
			var builder = new System.Text.StringBuilder();
			
			if (includeNamespace)
				builder.AppendFormat("[System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.{0}]", sec.Action);
			else
				builder.AppendFormat("[SecurityPermission(SecurityAction.{0}]", sec.Action);
			builder.AppendLine();
				
			foreach (Mono.Cecil.SecurityAttribute sattr in sec.SecurityAttributes)
			{
				if (includeNamespace)
					builder.AppendFormat("[{0}.{1}(", sattr.AttributeType.Namespace, sattr.AttributeType.Name);
				else
					builder.AppendFormat("[{0}(", sattr.AttributeType.Name);
				
				for (int i = 0; i < sattr.Properties.Count; ++i)
				{
					CustomAttributeNamedArgument d = sattr.Properties[i];
					builder.AppendFormat(string.Format("{0} = {1}", d.Name, ArgToString(d.Argument.Type, d.Argument.Value, includeNamespace, true)));
					
					if (i + 1 < sattr.Properties.Count || sattr.Fields.Count > 0)
						builder.Append(", ");
				}
				
				for (int i = 0; i < sattr.Fields.Count; ++i)
				{
					CustomAttributeNamedArgument d = sattr.Fields[i];
					builder.AppendFormat(string.Format("{0} = {1}", d.Name, ArgToString(d.Argument.Type, d.Argument.Value, includeNamespace, true)));
					
					if (i + 1 < sattr.Fields.Count)
						builder.Append(", ");
				}
				builder.Append("]");
				builder.AppendLine();
			}
			
			return builder.ToString();
			
#if OLD_CECIL
			if (sec.PermissionSet.IsUnrestricted())
			{
				if (includeNamespace)
					return string.Format("[System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.{0}, Unrestricted = true)]", sec.Action);
				else
					return string.Format("[SecurityPermission(SecurityAction.{0}, Unrestricted = true)]", sec.Action);
			}
			else
			{
				var builder = new System.Text.StringBuilder();
				
				if (includeNamespace)
					builder.AppendFormat("[System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.{0}", sec.Action);
				else
					builder.AppendFormat("[SecurityPermission(SecurityAction.{0}", sec.Action);
				foreach (IPermission o in sec.PermissionSet)
				{
					// This outputs the permission as XML which is really ugly but there are zillions of 
					// IPermission implementators so it would be a lot of work to do something better. 
					// We will special case one or two of the most common attributes however.
					do
					{
						var sp = o as SecurityPermission;
						if (sp != null)
						{
							builder.AppendFormat(", {0} = true", sp.Flags);
							break;
						}
						
						var snp = o as StrongNameIdentityPermission;
						if (snp != null)
						{
							if (!string.IsNullOrEmpty(snp.Name))
								builder.AppendFormat(", Name = \"{0}\"", snp.Name);
							if (snp.Version != null && snp.Version != new Version(0, 0))
								builder.AppendFormat(", Version = {0}", snp.Version);
							if (snp.PublicKey != null)
								builder.AppendFormat(", PublicKey = {0}", snp.PublicKey);
							break;
						}
						
						builder.AppendFormat(", {0}", o);
					}
					while (false);
				}
				builder.AppendFormat(")]");
				
				return builder.ToString();
			}
#endif
		}
		
		public static string LayoutToText(this TypeDefinition type, bool includeNamespace)
		{
			var builder = new System.Text.StringBuilder();
			
			if (includeNamespace)
				builder.Append("[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.");
			else
				builder.Append("[StructLayout(LayoutKind.");
			
			TypeAttributes attrs = type.Attributes;
			switch (attrs & TypeAttributes.LayoutMask)
			{
				case TypeAttributes.AutoLayout:
					builder.Append("Auto");
					break;
					
				case TypeAttributes.SequentialLayout:
					builder.Append("Sequential");
					break;
					
				case TypeAttributes.ExplicitLayout:
					builder.Append("Explicit");
					break;
					
				default:
					Contract.Assert(false, "bad layout: " + (attrs & TypeAttributes.LayoutMask));
					break;
			}
			
			if (type.PackingSize != 0)
			{
				builder.AppendFormat(", Size = {0}", type.PackingSize);
			}
			
			if ((type.Attributes & TypeAttributes.StringFormatMask) != TypeAttributes.AnsiClass)
			{
				if (includeNamespace)
					builder.Append(", CharSet = System.Runtime.InteropServices.CharSet.");
				else
					builder.Append(", CharSet = CharSet.");
				
				switch (attrs & TypeAttributes.StringFormatMask)
				{
					case TypeAttributes.AnsiClass:
						builder.Append("Ansi");
						break;
						
					case TypeAttributes.UnicodeClass:
						builder.Append("Unicode");
						break;
						
					case TypeAttributes.AutoClass:
						builder.Append("Auto");
						break;
						
					default:
						Contract.Assert(false, "bad string format: " + (attrs & TypeAttributes.StringFormatMask));
						break;
				}
			}
			
			builder.Append(")]");
			
			return builder.ToString();
		}
		
		public static string ArgToString(TypeReference type, object value, bool includeNamespace, bool includeTypeName)
		{
			string result = null;
			
			TypeDefinition td = type.Resolve();
			if (td != null && td.IsEnum)
			{
				// First see if an enum value exists which exactly matches the integer value.
				foreach (FieldDefinition field in td.Fields)
				{
					if (field.Constant != null && field.Constant.Equals(value))
						if (includeNamespace && !string.IsNullOrEmpty(td.Namespace))
							if (includeTypeName)
								return td.Namespace + '.' + td.Name + '.' + field.Name;
							else
								return td.Namespace + '.' + field.Name;
						else
							if (includeTypeName)
								return td.Name + '.' + field.Name;
							else
								return field.Name;
				}
				
				// Then try to find a combination of values which match the enum (note that
				// we don't check for FlagsAttribute because it is not always used for enums
				// which can be combined).
				try
				{
					var names = new List<string>();
					ulong union = Convert.ToUInt64(value);
					
					foreach (FieldDefinition field in td.Fields)
					{
						if (field.Constant != null)
						{
							ulong operand = Convert.ToUInt64(field.Constant);
							if ((union & operand) == operand)
							{
								if (includeNamespace && !string.IsNullOrEmpty(td.Namespace))
									if (includeTypeName)
										names.Add(td.Namespace + '.' + td.Name + '.' + field.Name);
									else
										names.Add(td.Namespace + '.' + field.Name);
								else
									if (includeTypeName)
										names.Add(td.Name + '.' + field.Name);
									else
										names.Add(field.Name);
								
								union &= ~operand;
							}
						}
					}
					
					if (union == 0 && names.Count > 0)
						return string.Join(" | ", names.ToArray());
				}
				catch
				{
				}
			}
			
			return result ?? ArgToString(value);
		}
		
		public static string ArgToString(object arg)
		{
			if (arg == null)
				return string.Empty;
				
			else if (arg is bool)
				return (bool) arg ? " true" : "false";
				
			else if (arg is string)
				return string.Format("\"{0}\"", arg);
			
			else if (arg is Mono.Cecil.Cil.Instruction)
				return string.Format("{0:X4}", ((Mono.Cecil.Cil.Instruction) arg).Offset);
			
			else
				return arg.ToString();
		}
	}
}
