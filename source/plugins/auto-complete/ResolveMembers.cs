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
		
		public Member[] Resolve(ResolvedTarget target, CsGlobalNamespace globals)
		{
			var members = new List<Member>();
			
			// Get members of the type.
			string fullName = DoGetMembers(target, globals, members, true);
			
			// Get the members for the base types.	
			var resolveType = new ResolveType(new TargetDatabase(m_database));
			IEnumerable<ResolvedTarget> bases = resolveType.GetBases(globals, fullName, target.IsInstance, target.IsStatic);
			foreach (ResolvedTarget t in bases)
			{
				DoGetMembers(t, globals, members, false);
				if (target.IsInstance)
					DoGetExtensionMethods(t.FullName, globals, members);
			}
			
			// Get extension methods.
			if (target.IsInstance)
			{
				DoGetExtensionMethods(target.FullName, globals, members);
				
				foreach (string name in resolveType.GetAllInterfaces(globals, fullName, bases, target.IsInstance))
				{
					DoGetExtensionMethods(name, globals, members);
				}
			}
			
			return members.ToArray();
		}
		
		#region Private Methods
		private void DoGetExtensionMethods(string fullName, CsGlobalNamespace globals, List<Member> members)
		{
			string sql = string.Format(@"
				SELECT name, arg_types, arg_names, attributes, namespace
					FROM Methods 
				INNER JOIN ExtensionMethods
					ON Methods.method = ExtensionMethods.method AND Methods.hash = ExtensionMethods.hash
				WHERE type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var methods = from r in rows
				where DoIsValidExtensionMethod(r[4], ushort.Parse(r[3]), globals)
				select DoGetMethodName(r[0], r[1], r[2], 1);
			
			foreach (Member name in methods)
				members.AddIfMissing(name);
		}
		
		private string DoGetMembers(ResolvedTarget target, CsGlobalNamespace globals, List<Member> members, bool includePrivates)
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
				DoGetDatabaseMembers(fullName, target.IsInstance, target.IsStatic, members, includePrivates);
				
			return fullName;
		}
		
		private void DoGetDatabaseMembers(string fullName, bool instanceCall, bool isStaticCall, List<Member> members, bool includePrivates)
		{
			DoGetDatabaseMethods(fullName, instanceCall, isStaticCall, members, includePrivates);
			DoGetDatabaseFields(fullName, instanceCall, isStaticCall, members, includePrivates);
		}
		
		private void DoGetDatabaseMethods(string fullName, bool instanceCall, bool isStaticCall, List<Member> members, bool includePrivates)
		{
			string sql = string.Format(@"
				SELECT name, arg_types, arg_names, attributes
					FROM Methods 
				WHERE declaring_type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var methods = from r in rows
				where DoIsValidMethod(r[0], ushort.Parse(r[3]), instanceCall, isStaticCall, includePrivates)
				select DoGetMethodName(r[0], r[1], r[2], 0);
			
			foreach (Member name in methods)
				members.AddIfMissing(name);
		}
		
		private void DoGetDatabaseFields(string fullName, bool instanceCall, bool isStaticCall, List<Member> members, bool includePrivates)
		{
			string sql = string.Format(@"
				SELECT name, attributes
					FROM Fields 
				WHERE declaring_type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var fields = from r in rows
				where DoIsValidField(r[0], ushort.Parse(r[1]), instanceCall, isStaticCall, includePrivates)
				select r[0];
			
			foreach (string name in fields)
				members.AddIfMissing(new Member(name));
		}
		
		private void DoGetParsedMembers(ResolvedTarget target, List<Member> members, bool includePrivates)
		{
			CsEnum e = target.Type as CsEnum;
			if (e != null)
			{
				if (target.IsStatic)
					members.AddRange(from n in e.Names select new Member(n));
			}
			else
				DoGetParsedTypeMembers(target, members, includePrivates);
		}
		
		private bool DoShouldAdd(ResolvedTarget target, MemberModifiers modifiers)
		{
			if ((modifiers & MemberModifiers.Static) == 0)
				return target.IsInstance;
			else
				return target.IsStatic;
		}
		
		private void DoGetParsedTypeMembers(ResolvedTarget target, List<Member> members, bool includePrivates)
		{
			foreach (CsField field in target.Type.Fields)
			{
				if (DoShouldAdd(target, field.Modifiers))
					if (includePrivates || field.Access != MemberModifiers.Private)
						members.AddIfMissing(new Member(field.Name));
			}
			
			foreach (CsMethod method in target.Type.Methods)
			{
				if (!method.IsConstructor && !method.IsFinalizer)
				{
					if (DoShouldAdd(target, method.Modifiers))
					{
						if (includePrivates || method.Access != MemberModifiers.Private)
						{
							var atypes = from p in method.Parameters select p.Type;
							var anames = from p in method.Parameters select p.Name;
							string text = method.Name + "(" + string.Join(", ", (from p in method.Parameters select p.Type + " " + p.Name).ToArray()) + ")";
							
							members.AddIfMissing(new Member(text, atypes.ToArray(), anames.ToArray()));
						}
					}
				}
			}
			
			// Note that indexers are not counted because they are not preceded with a dot.
			foreach (CsProperty prop in target.Type.Properties)
			{
				if (prop.HasGetter)
				{
					if (DoShouldAdd(target, prop.Modifiers))
						if (includePrivates || prop.Access != MemberModifiers.Private)
							members.AddIfMissing(new Member(prop.Name));
				}
			}
		}
		
		private bool DoIsValidMethod(string name, ushort attributes, bool instanceCall, bool isStaticCall, bool includePrivates)
		{
			bool valid = true;
			
			if (!(instanceCall && isStaticCall))
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
		
		private bool DoIsValidExtensionMethod(string ns, ushort attributes, CsGlobalNamespace globals)
		{
			bool valid = false;
			
			for (int i = 0; i < globals.Uses.Length && !valid; ++i)
			{
				valid = globals.Uses[i].Namespace == ns;
			}
			
			if (valid)
				valid = (attributes & 0x0001) == 0;	// no private methods
				
			return valid;
		}
		
		private bool DoIsValidField(string name, ushort attributes, bool instanceCall, bool isStaticCall, bool includePrivates)
		{
			bool valid = true;
			
			if (!(instanceCall && isStaticCall))
				if (instanceCall)
					valid = (attributes & 0x0010) == 0;
				else
					valid = (attributes & 0x0010) != 0;
			
			if (valid && !includePrivates)
				valid = (attributes & 0x0001) == 0;
				
			return valid;
		}
		
		private Member DoGetMethodName(string mname, string argTypes, string argNames, int firstArg)
		{
			var builder = new StringBuilder(mname.Length + argTypes.Length + argNames.Length);
			var atypes = new List<string>();
			var anames = new List<string>();
			
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
				for (int i = firstArg; i < types.Length; ++i)
				{
					string type = CsHelpers.GetAliasedName(types[i]);
					if (type == types[i])
					{
						type = DoTrimNamespace(type);
						type = DoTrimGeneric(type);
					}
					atypes.Add(type);
					builder.Append(type);
					
					builder.Append(' ');
					
					string name = names[i];
					anames.Add(name);
					builder.Append(name);
					
					if (i + 1 < types.Length)
						builder.Append(", ");
				}
				builder.Append(')');
			}
			
			return new Member(builder.ToString(), atypes.ToArray(), anames.ToArray());
		}
		
		// System.Collections.Generic.IEnumerable`1<TSource>:System.Func`2<TSource,System.Boolean>
		private string DoTrimNamespace(string type)
		{
			while (true)
			{
				int j = type.IndexOf('.');
				if (j < 0)
					break;
					
				int i = j;
				while (i > 0 && char.IsLetter(type[i - 1]))
					--i;
					
				type = type.Substring(0, i) + type.Substring(j + 1);
			}
			
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
		#endregion
	}
}
