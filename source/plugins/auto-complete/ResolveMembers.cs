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

using Gear;
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
		public ResolveMembers(ITargetDatabase db)
		{
			m_database = db;
		}
		
		public Member[] Resolve(ResolvedTarget target, CsGlobalNamespace globals)
		{
			var members = new List<Member>();
			
			// Get members of the type.
			string fullName = DoGetMembers(target, globals, members, true);
			
			// Get the members for the base types.	
			var resolveType = new ResolveType(m_database);
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
			if (type == null)
			{
				Boss boss = ObjectModel.Create("CsParser");
				var parses = boss.Get<IParses>();
				type = parses.FindType(fullName);
			}
			
			if (type != null)
			{
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, members, includePrivates);
				
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
			Member[] candidates = m_database.GetMembers(fullName, instanceCall, isStaticCall, globals);
			foreach (Member member in candidates)
			{
				members.AddIfMissing(member);
			}
		}
		
		private void DoGetExtensionMethods(string fullName, List<Member> members, CsGlobalNamespace globals)
		{
			Member[] candidates = m_database.GetExtensionMethods(fullName, globals);
			foreach (Member member in candidates)
			{
				members.AddIfMissing(member);
			}
		}
		
		private void DoGetParsedMembers(CsType type, bool isInstance, bool isStatic, List<Member> members, bool includePrivates)
		{
			CsEnum e = type as CsEnum;
			if (e != null)
			{
				if (isStatic)
				{
					var candidates = from n in e.Names select new Member(n, type.FullName, type.FullName);
					foreach (Member member in candidates)
					{
						members.AddIfMissing(member);
					}
				}
			}
			else
				DoGetParsedTypeMembers(type, isInstance, isStatic, members, includePrivates);
		}
		
		private bool DoShouldAdd(bool isInstance, bool isStatic, MemberModifiers modifiers)
		{
			if ((modifiers & MemberModifiers.Static) == 0)
				return isInstance;
			else
				return isStatic;
		}
		
		private void DoGetParsedTypeMembers(CsType type, bool isInstance, bool isStatic, List<Member> members, bool includePrivates)
		{
			foreach (CsField field in type.Fields)
			{
				if (DoShouldAdd(isInstance, isStatic, field.Modifiers))
					if (includePrivates || field.Access != MemberModifiers.Private)
						members.AddIfMissing(new Member(field.Name, field.Type, type.FullName));
			}
			
			foreach (CsMethod method in type.Methods)
			{
				if (!method.IsConstructor && !method.IsFinalizer)
				{
					if (DoShouldAdd(isInstance, isStatic, method.Modifiers))
					{
						if (includePrivates || method.Access != MemberModifiers.Private)
						{
							var anames = from p in method.Parameters select p.Name;
							string text = method.Name + "(" + string.Join(", ", (from p in method.Parameters select p.Type + " " + p.Name).ToArray()) + ")";
							
							members.AddIfMissing(new Member(text, anames.ToArray(), method.ReturnType, type.FullName));
						}
					}
				}
			}
			
			// Note that indexers are not counted because they are not preceded with a dot.
			foreach (CsProperty prop in type.Properties)
			{
				if (prop.HasGetter)
				{
					if (DoShouldAdd(isInstance, isStatic, prop.Modifiers))
						if (includePrivates || prop.Access != MemberModifiers.Private)
							members.AddIfMissing(new Member(prop.Name, prop.ReturnType, type.FullName));
				}
			}
		}
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		#endregion
	}
}
