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
			
			var types = new List<CsType>();
			var ids = new List<TypeId>();
			TypeId[] allNames = DoGetBases(target.TypeName, types, ids);
			
			foreach (CsType type in types)
			{
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, members, type.FullName == target.TypeName);
			}
			
			members.AddIfMissingRange(m_database.GetMembers(ids.ToArray(), target.IsInstance, target.IsStatic));
			
			if (target.IsInstance)
				members.AddIfMissingRange(m_database.GetExtensionMethods(allNames));
			
			return members.ToArray();
		}
		
		#region Private Methods
		private TypeId[] DoGetBases(string typeName, List<CsType> types, List<TypeId> ids)
		{
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
			
			var allNames = new List<TypeId>();
			allNames.Add(new TypeId(typeName, 0));
			
			int i = 0;
			while (i < allNames.Count)
			{
				TypeId name = allNames[i];
				
				// Note that we want to use CsType instead of the database where possible
				// because it should be more up to date.
				CsType type = parses.FindType(name.FullName);
				if (type != null)
					types.Add(type);
				
				if (type is CsEnum)
				{
					name = new TypeId("System.Enum", 0);
					type = null;
				}
				
				// If the type is partial then the parsed types (probably) do not include all
				// of the members so we need to include both the parsed and database
				// info to ensure we get everything.
				if (type == null || (type.Modifiers & MemberModifiers.Partial) != 0)
					ids.Add(name);
					
				// Note that we don't use CsType to get bases because it's difficult to get the
				// full names for them. Note also that interface names can be repeated.
				allNames.AddIfMissingRange(m_database.GetBases(name));
			}
			
			return allNames.ToArray();
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
