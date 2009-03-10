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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AutoComplete
{
	// Resolves a target into a list of member names which may be used on it.
	internal sealed class ResolveMembers
	{
		public ResolveMembers(Database db)
		{
			m_database = db;
		}
		
		public string[] Resolve(ResolvedTarget target, CsGlobalNamespace globals)
		{
			var members = new List<string>();
			
			// Get members of the type.
			string fullName = DoGetMembers(target, globals, members, true);
			
			// Get the members for the base types.			
			var resolveType = new ResolveType(new TargetDatabase(m_database));
			foreach (string name in resolveType.GetBases(globals, fullName))
			{
				ResolvedTarget b = resolveType.Resolve(name, globals, target.IsInstance);
				if (b != null)
					DoGetMembers(b, globals, members, false);
			}
			
			return members.ToArray();
		}
		
		#region Private Methods		
		private string DoGetMembers(ResolvedTarget target, CsGlobalNamespace globals, List<string> members, bool includePrivates)
		{
			// Get the members for the target type. Note that we don't use the
			// database if we have the parsed type because it is likely out of date.
			string fullName = target.FullName;
			
			string hash = target.Hash;
			CsType type = target.Type;
			if (type != null)
			{
				DoGetParsedMembers(target, members, includePrivates);
				
				if (type is CsEnum)
				{
					fullName = "System.Enum";
					hash = "dummy";
					type = null;
				}
			}
			
			if (hash != null && (type == null || (type.Modifiers & MemberModifiers.Partial) != 0))
				DoGetDatabaseMembers(fullName, target.IsInstance, members, includePrivates);
				
			return fullName;
		}
		
		private void DoGetDatabaseMembers(string fullName, bool instanceCall, List<string> members, bool includePrivates)
		{
			DoGetDatabaseMethods(fullName, instanceCall, members, includePrivates);
			DoGetDatabaseFields(fullName, instanceCall, members, includePrivates);
		}
		
		private void DoGetDatabaseMethods(string fullName, bool instanceCall, List<string> members, bool includePrivates)
		{
			string sql = string.Format(@"
				SELECT name, arg_types, arg_names, attributes
					FROM Methods 
				WHERE declaring_type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var methods = from r in rows
				where DoIsValidMethod(r[0], ushort.Parse(r[3]), instanceCall, includePrivates)
				select DoGetMethodName(r[0], r[1], r[2]);
			
			foreach (string name in methods)
				members.AddIfMissing(name);
		}
		
		private void DoGetDatabaseFields(string fullName, bool instanceCall, List<string> members, bool includePrivates)
		{
			string sql = string.Format(@"
				SELECT name, attributes
					FROM Fields 
				WHERE declaring_type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var fields = from r in rows
				where DoIsValidField(r[0], ushort.Parse(r[1]), instanceCall, includePrivates)
				select r[0];
			
			foreach (string name in fields)
				members.AddIfMissing(name);
		}
		
		private void DoGetParsedMembers(ResolvedTarget target, List<string> members, bool includePrivates)
		{
			CsEnum e = target.Type as CsEnum;
			if (e != null)
			{
				if (!target.IsInstance)
					members.AddRange(e.Names);
			}
			else
				DoGetParsedTypeMembers(target, members, includePrivates);
		}
		
		private void DoGetParsedTypeMembers(ResolvedTarget target, List<string> members, bool includePrivates)
		{
			foreach (CsField field in target.Type.Fields)
			{
				if (target.IsInstance == ((field.Modifiers & MemberModifiers.Static) == 0))
					if (includePrivates || field.Access != MemberModifiers.Private)
						members.AddIfMissing(field.Name);
			}
			
			foreach (CsMethod method in target.Type.Methods)
			{
				if (!method.IsConstructor && !method.IsFinalizer)
				{
					if (target.IsInstance == ((method.Modifiers & MemberModifiers.Static) == 0))
						if (includePrivates || method.Access != MemberModifiers.Private)
							members.AddIfMissing(method.Name + "(" + string.Join(", ", (from p in method.Parameters select p.Type + " " + p.Name).ToArray()) + ")");
				}
			}
			
			// Note that indexers are not counted because they are not preceded with a dot.
			foreach (CsProperty prop in target.Type.Properties)
			{
				if (prop.HasGetter)
				{
					if (target.IsInstance == ((prop.Modifiers & MemberModifiers.Static) == 0))
						if (includePrivates || prop.Access != MemberModifiers.Private)
							members.AddIfMissing(prop.Name);
				}
			}
		}
		
		private bool DoIsValidMethod(string name, ushort attributes, bool instanceCall, bool includePrivates)
		{
			bool valid;
			
			if (instanceCall)
				valid = (attributes & 0x0010) == 0;
			else
				valid = (attributes & 0x0010) != 0;
			
			if (valid && !includePrivates)
				valid = (attributes & 0x0001) == 0;
				
			if (valid && name.Contains(".ctor"))
				valid = false;
			
			if (valid && name.Contains(".cctor"))
				valid = false;
			
			if (valid && name.Contains("set_"))
				valid = false;
			
			if (valid && name.Contains("op_"))
				valid = false;
				
			if (valid && name.Contains("add_"))
				valid = false;
				
			if (valid && name.Contains("remove_"))
				valid = false;
				
			if (valid && name == "Finalize")
				valid = false;
				
			return valid;
		}
		
		private bool DoIsValidField(string name, ushort attributes, bool instanceCall, bool includePrivates)
		{
			bool valid;
			
			if (instanceCall)
				valid = (attributes & 0x0010) == 0;
			else
				valid = (attributes & 0x0010) != 0;
			
			if (valid && !includePrivates)
				valid = (attributes & 0x0001) == 0;
				
			return valid;
		}
		
		private string DoGetMethodName(string mname, string argTypes, string argNames)
		{
			var builder = new StringBuilder(mname.Length + argTypes.Length + argNames.Length);
			
			if (mname.StartsWith("get_"))
			{
				builder.Append(mname.Substring(4));
			}
			else
			{
				builder.Append(mname);
				
				builder.Append('(');
				string[] types = argTypes.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				string[] names = argNames.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < types.Length; ++i)
				{
					string type = types[i];
					if (ms_aliases.ContainsKey(type))
					{
						type = ms_aliases[type];
					}
					else
					{
						type = DoTrimNamespace(type);
						type = DoTrimGeneric(type);
					}
					builder.Append(type);
					
					builder.Append(' ');
					
					string name = names[i];
					builder.Append(name);
					
					if (i + 1 < types.Length)
						builder.Append(", ");
				}
				builder.Append(')');
			}
			
			return builder.ToString();
		}
		
		private string DoTrimNamespace(string type)
		{
			int i = type.LastIndexOf('.');
			if (i >= 0)
				type = type.Substring(i + 1);
			
			return type;
		}
		
		private string DoTrimGeneric(string type)
		{
			while (true)
			{
				int i = type.IndexOf('`');
				if (i < 0)
					break;
					
				int count = 1;
				while (i + count < type.Length && char.IsDigit(type[i + count]))
					++count;
					
				if (count > 1)
				{
					type = type.Substring(0, i) + type.Substring(i + count);
				}
			}
			
			return type;
		}
		#endregion
		
		#region Fields
		private Database m_database;
		
		private static Dictionary<string, string> ms_aliases = new Dictionary<string, string>	// TODO: ShortForm.cs has the same list
		{
			{"System.Boolean", "bool"},
			{"System.Byte", "byte"},
			{"System.Char", "char"},
			{"System.Decimal", "decimal"},
			{"System.Double", "double"},
			{"System.Int16", "short"},
			{"System.Int32", "int"},
			{"System.Int64", "long"},
			{"System.SByte", "sbyte"},
			{"System.Object", "object"},
			{"System.Single", "float"},
			{"System.String", "string"},
			{"System.UInt16", "ushort"},
			{"System.UInt32", "uint"},
			{"System.UInt64", "ulong"},
			{"System.Void", "void"},
			
			{"System.Boolean[]", "bool[]"},
			{"System.Byte[]", "byte[]"},
			{"System.Char[]", "char[]"},
			{"System.Decimal[]", "decimal[]"},
			{"System.Double[]", "double[]"},
			{"System.Int16[]", "short[]"},
			{"System.Int32[]", "int[]"},
			{"System.Int64[]", "long[]"},
			{"System.SByte[]", "sbyte[]"},
			{"System.Object[]", "object[]"},
			{"System.Single[]", "float[]"},
			{"System.String[]", "string[]"},
			{"System.UInt16[]", "ushort[]"},
			{"System.UInt32[]", "uint[]"},
			{"System.UInt64[]", "ulong[]"},
			{"System.Void[]", "void[]"},
		};
		#endregion
	}
}
