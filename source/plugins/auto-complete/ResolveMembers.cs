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
			}
			
			// Get extension methods for the interfaces.
			if (target.IsInstance)
			{
				foreach (string name in resolveType.GetAllInterfaces(globals, fullName, bases, target.IsInstance))
				{
					DoGetExtensionMethods(name, members, globals);
				}
			}
			
			return members.ToArray();
		}
		
		#region Private Methods
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
			{
				DoGetDatabaseMembers(fullName, target.IsInstance, target.IsStatic, members, globals);
			}
				
			return fullName;
		}
		
		private void DoGetDatabaseMembers(string fullName, bool instanceCall, bool isStaticCall, List<Member> members, CsGlobalNamespace globals)
		{
			string sql;
			if (instanceCall && isStaticCall)
				sql = string.Format(@"
					SELECT text, return_type, arg_names, namespace
						FROM Members 
					WHERE type = '{0}'", fullName);
			else
				sql = string.Format(@"
					SELECT text, return_type, arg_names, namespace
						FROM Members 
					WHERE type = '{0}' AND is_static = '{1}'", fullName, isStaticCall ? "1" : "0");
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				string[] names = r[2].Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				if (r[3].Length == 0)
				{
					members.AddIfMissing(new Member(r[0], names, r[1]));
				}
				else if (DoIsValidExtensionMethod(r[3], globals))
				{
					string[] last = new string[names.Length - 1];
					Array.Copy(names, 1, last, 0, last.Length);
					
					members.AddIfMissing(new Member(r[0], last, r[1]));
				}
			}
		}
		
		private void DoGetExtensionMethods(string fullName, List<Member> members, CsGlobalNamespace globals)
		{
			string sql = string.Format(@"
				SELECT text, return_type, arg_names, namespace
					FROM Members 
				WHERE type = '{0}' AND length(namespace) > 0", fullName);
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				if (DoIsValidExtensionMethod(r[3], globals))
				{
					string[] names = r[2].Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);

					string[] last = new string[names.Length - 1];
					Array.Copy(names, 1, last, 0, last.Length);
					
					members.AddIfMissing(new Member(r[0], last, r[1]));
				}
			}
		}
		
		private void DoGetParsedMembers(ResolvedTarget target, List<Member> members, bool includePrivates)
		{
			CsEnum e = target.Type as CsEnum;
			if (e != null)
			{
				if (target.IsStatic)
					members.AddRange(from n in e.Names select new Member(n, target.Type.FullName));
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
						members.AddIfMissing(new Member(field.Name, field.Type));
			}
			
			foreach (CsMethod method in target.Type.Methods)
			{
				if (!method.IsConstructor && !method.IsFinalizer)
				{
					if (DoShouldAdd(target, method.Modifiers))
					{
						if (includePrivates || method.Access != MemberModifiers.Private)
						{
							var anames = from p in method.Parameters select p.Name;
							string text = method.Name + "(" + string.Join(", ", (from p in method.Parameters select p.Type + " " + p.Name).ToArray()) + ")";
							
							members.AddIfMissing(new Member(text, anames.ToArray(), method.ReturnType));
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
							members.AddIfMissing(new Member(prop.Name, prop.ReturnType));
				}
			}
		}
				
		private bool DoIsValidExtensionMethod(string ns, CsGlobalNamespace globals)
		{
			bool valid = false;
			
			for (int i = 0; i < globals.Uses.Length && !valid; ++i)
			{
				valid = globals.Uses[i].Namespace == ns;
			}
			
			return valid;
		}
		#endregion
		
		#region Fields
		private Database m_database;
		#endregion
	}
}
