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
using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public static class CecilExtensions
	{
		public static string ToText(this CustomAttribute attr)
		{
			var args = new List<string>();
			
			attr.Resolve ();						// we need to do this so things like the enums used within arguments show up correctly
			foreach (object o in attr.ConstructorParameters)
			{
				args.Add(DoArgToString(o));
			}
			
			foreach (System.Collections.DictionaryEntry d in attr.Properties)
			{
				args.Add(string.Format("{0} = {1}", d.Key, DoArgToString(d.Value)));
			}
			
			foreach (System.Collections.DictionaryEntry d in attr.Fields)
			{
				args.Add(string.Format("{0} = {1}", d.Key, DoArgToString(d.Value)));
			}
			
			string name = attr.Constructor.DeclaringType.FullName;
			if (name.EndsWith("Attribute"))
				name = name.Substring(0, name.Length - "Attribute".Length);
			return string.Format("[{0}({1})]", name, string.Join(", ", args.ToArray()));
		}
		
		public static bool HasAttribute(this CustomAttributeCollection attrs, string name)
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
		
		public static string ToText(this SecurityDeclaration sec)
		{
			if (sec.PermissionSet.IsUnrestricted())
			{
				return string.Format("[System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.{0}, Unrestricted = true)]", sec.Action);
			}
			else
			{
				var builder = new System.Text.StringBuilder();
				
				builder.AppendFormat("[System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.{0}", sec.Action);
				foreach (IPermission o in sec.PermissionSet)
				{
					// This outputs the permission as XML which is really ugly but there are zillions of 
					// IPermission implementators so it would be a lot of work to do something better. 
					// We will special case one or two of the most common attributes however.
					SecurityPermission sp = o as SecurityPermission;
					if (sp != null)
					{
						builder.AppendFormat(", {0} = true", sp.Flags);
					}
					else
						builder.AppendFormat(", {0}", o);
				}
				builder.AppendFormat(")]");
				
				return builder.ToString();
			}
		}
		
		private static string DoArgToString(object arg)
		{
			if (arg == null)
				return string.Empty;
			
			else if (arg is string)
				return string.Format("\"{0}\"", arg);
			
			else
				return arg.ToString();
		}
	}
}
