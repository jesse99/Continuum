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
			var baseNames = new List<string>();
			var interfaceNames = new List<string>();
			string[] allNames = DoGetBases(target.TypeName, types, baseNames, interfaceNames);
			
			foreach (CsType type in types)
			{
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, members, type.FullName == target.TypeName);
			}
			
			// Note that we need to make two GetMembers queries to ensure that interface
			// methods are not used in place of base methods (this gives us better results
			// when the context menu is used to filter out methods associated with types).
			DoAddIfMissingRange("Fields:", members, m_database.GetFields(baseNames.ToArray(), target.IsInstance, target.IsStatic));
			DoAddIfMissingRange("Base Members:", members, m_database.GetMembers(baseNames.ToArray(), target.IsInstance, target.IsStatic));
			DoAddIfMissingRange("Interface Members:", members, m_database.GetMembers(interfaceNames.ToArray(), target.IsInstance, target.IsStatic));
			
			if (target.IsInstance)
			{
				var namespaces = new List<string>();
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
					namespaces.Add(globals.Namespaces[i].Name);
				
				for (int i = 0; i < globals.Uses.Length; ++i)
					namespaces.Add(globals.Uses[i].Namespace);
				
				DoAddIfMissingRange("Extension Methods:", members, m_database.GetExtensionMethods(target.TypeName, allNames, namespaces.ToArray()));
			}
			
			return members.ToArray();
		}
		
		// Usually this will return zero or one name, but it can return more (eg for explicit interface
		// implementations).
		public Member[] Find(ResolvedTarget target, CsGlobalNamespace globals, string name, int arity)
		{
			var members = new List<Member>();
			
			var types = new List<CsType>();
			var baseNames = new List<string>();
			var interfaceNames = new List<string>();
			string[] allNames = DoGetBases(target.TypeName, types, baseNames, interfaceNames);
			
			foreach (CsType type in types)
			{
				var candidates = new List<Member>();
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, candidates, type.FullName == target.TypeName);
				members = (from m in candidates where DoMatch(m, name, arity) select m).ToList();
			}
			
			// Note that we need to make two GetMembers queries to ensure that interface
			// methods are not used in place of base methods (this gives us better results
			// when the context menu is used to filter out methods associated with types).
			DoAddIfMissingRange("Fields:", members, m_database.GetFields(baseNames.ToArray(), target.IsInstance, target.IsStatic, name));
			DoAddIfMissingRange("Base Members:", members, m_database.GetMembers(baseNames.ToArray(), target.IsInstance, target.IsStatic, name, arity));
			DoAddIfMissingRange("Interface Members:", members, m_database.GetMembers(interfaceNames.ToArray(), target.IsInstance, target.IsStatic, name, arity));
			
			if (target.IsInstance)
			{
				var namespaces = new List<string>();
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
					namespaces.Add(globals.Namespaces[i].Name);
				
				for (int i = 0; i < globals.Uses.Length; ++i)
					namespaces.Add(globals.Uses[i].Namespace);
				
				DoAddIfMissingRange("Extension Methods:", members, m_database.GetExtensionMethods(target.TypeName, allNames, namespaces.ToArray(), name, arity));
			}
			
			return members.ToArray();
		}
		
		#region Private Methods
		public static void DoAddIfMissingRange(string mesg, List<Member> data, IEnumerable<Member> values)
		{
//			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", mesg);
			foreach (Member value in values)
			{
				if (data.IndexOf(value) < 0)
				{
//					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "    {0}      {1}", value.ToString(), value.Name);
					data.Add(value);
				}
			}
		}
		
		private bool DoMatch(Member member, string name, int numArgs)
		{
			bool matches = false;
			
			if (numArgs == 0)
			{
				matches = member.Text == name || member.Text == (name + "()");
			}
			else if (member.Arity == numArgs)
			{
				matches = member.Name == name;
			}
			
			return matches;
		}
		
		private string[] DoGetBases(string typeName, List<CsType> types, List<string> baseNames, List<string> interfaceNames)
		{
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
			
			var allNames = new List<string>();
			allNames.Add(typeName);
			
			int i = 0;
			while (i < allNames.Count)
			{
				string name = allNames[i++];
				
				// Note that we want to use CsType instead of the database where possible
				// because it should be more up to date.
				CsType type = parses.FindType(name);
				if (type != null)
					types.Add(type);
				
				if (type is CsEnum)
				{
					name = "System.Enum";
					type = null;
				}
				
				// If the type is partial then the parsed types (probably) do not include all
				// of the members so we need to include both the parsed and database
				// info to ensure we get everything.
				if (type == null || (type.Modifiers & MemberModifiers.Partial) != 0)
					if (CsHelpers.IsInterface(name))
						interfaceNames.AddIfMissing(name);
					else
						baseNames.AddIfMissing(name);
					
				// Note that we don't use CsType to get bases because it's difficult to get the
				// full names for them. Note also that interface names can be repeated.
				m_database.GetBases(name, baseNames, interfaceNames, allNames);
				Contract.Assert(allNames.Count < 1000);
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
							
							members.AddIfMissing(new Member(text, anames.Count(), method.ReturnType, type.FullName));
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
