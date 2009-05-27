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
			
#if !TEST
			Boss boss = ObjectModel.Create("CsParser");
			m_parses = boss.Get<IParses>();
#endif
		}
		
		public Item[] Resolve(CsMember context, ResolvedTarget target, CsGlobalNamespace globals)
		{
			Profile.Start("ResolveMembers::Resolve");
			var items = new List<Item>();
			
			var types = new List<CsType>();
			var baseNames = new List<string>();
			var interfaceNames = new List<string>();
			string[] allNames = DoGetBases(globals, target.TypeName, types, baseNames, interfaceNames);
			
			bool includeProtected = (context != null && allNames.Contains(context.DeclaringType.FullName)) || target.BaseKeyword;
			foreach (CsType type in types)
			{
				bool includePrivate = context != null && type.FullName == context.DeclaringType.FullName;
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, items, includePrivate, includeProtected);
			}
			
			// Note that we need to make two GetMembers queries to ensure that interface
			// methods are not used in place of base methods (this gives us better results
			// when the context menu is used to filter out methods associated with types).
			DoAddIfMissingRange("Fields:", items, m_database.GetFields(baseNames.ToArray(), target.IsInstance, target.IsStatic, includeProtected));
			DoAddIfMissingRange("Base Members:", items, m_database.GetMembers(baseNames.ToArray(), target.IsInstance, target.IsStatic, includeProtected));
			DoAddIfMissingRange("Interface Members:", items, m_database.GetMembers(interfaceNames.ToArray(), target.IsInstance, target.IsStatic, includeProtected));
			
			if (target.IsInstance)
			{
				var namespaces = new List<string>();
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
					namespaces.Add(globals.Namespaces[i].Name);
				
				for (int i = 0; i < globals.Uses.Length; ++i)
					namespaces.Add(globals.Uses[i].Namespace);
				
				DoGetParsedExtensions(globals, target.TypeName, items);
				DoAddIfMissingRange("extension methods:", items, m_database.GetExtensionMethods(allNames, namespaces.ToArray()));
			}
			
			Profile.Stop("ResolveMembers::Resolve");
			return items.ToArray();
		}
		
		// Usually this will return zero or one name, but it can return more (eg for explicit interface
		// implementations).
		public Item[] Find(CsMember context, ResolvedTarget target, CsGlobalNamespace globals, string name, int arity)
		{
			Profile.Start("ResolveMembers::Find");
			var items = new List<Item>();
			
			var types = new List<CsType>();
			var baseNames = new List<string>();
			var interfaceNames = new List<string>();
			string[] allNames = DoGetBases(globals, target.TypeName, types, baseNames, interfaceNames);
			
			bool includeProtected = context != null && allNames.Contains(context.DeclaringType.FullName);
			foreach (CsType type in types)
			{
				var candidates = new List<Item>();
				
				bool includePrivate = context != null && type.FullName == context.DeclaringType.FullName;
				DoGetParsedMembers(type, target.IsInstance, target.IsStatic, candidates, includePrivate, includeProtected);
				items = (from m in candidates where DoMatch(m, name, arity) select m).ToList();
			}
			
			// Note that we need to make two GetMembers queries to ensure that interface
			// methods are not used in place of base methods (this gives us better results
			// when the context menu is used to filter out methods associated with types).
			DoAddIfMissingRange("Fields:", items, m_database.GetFields(baseNames.ToArray(), target.IsInstance, target.IsStatic, name, includeProtected));
			DoAddIfMissingRange("Base Members:", items, m_database.GetMembers(baseNames.ToArray(), target.IsInstance, target.IsStatic, name, arity, includeProtected));
			DoAddIfMissingRange("Interface Members:", items, m_database.GetMembers(interfaceNames.ToArray(), target.IsInstance, target.IsStatic, name, arity, includeProtected));
			
			if (target.IsInstance)
			{
				var namespaces = new List<string>();
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
					namespaces.Add(globals.Namespaces[i].Name);
				
				for (int i = 0; i < globals.Uses.Length; ++i)
					namespaces.Add(globals.Uses[i].Namespace);
				
				DoAddIfMissingRange("extension methods:", items, m_database.GetExtensionMethods(allNames, namespaces.ToArray(), name, arity));
			}
			
			Profile.Stop("ResolveMembers::Find");
			return items.ToArray();
		}
		
		#region Private Methods
		public static void DoAddIfMissingRange(string mesg, List<Item> data, IEnumerable<Item> values)
		{
//			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", mesg);
			foreach (Item value in values)
			{
				if (data.IndexOf(value) < 0)
				{
//					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "    {0}      {1}", value.ToString(), value.Name);
					data.Add(value);
				}
			}
		}
		
		private bool DoMatch(Item item, string name, int numArgs)
		{
			bool matches = false;
			
			MethodItem method = item as MethodItem;
			if (numArgs == 0)
			{
				if (method != null)
					matches = method.Name == name;
				else
					matches = item.Text == name;
			}
			else if (method != null && method.Arity == numArgs)
			{
				matches = method.Name == name;
			}
			
			return matches;
		}
		
		private string[] DoGetBases(CsGlobalNamespace globals, string typeName, List<CsType> types, List<string> baseNames, List<string> interfaceNames)
		{
#if TEST
			var parses = new CsParser.Parses();
#else
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
#endif
			
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
				// of the items so we need to include both the parsed and database
				// info to ensure we get everything.
				if (type == null || (type.Modifiers & MemberModifiers.Partial) != 0)
					if (CsHelpers.IsInterface(name))
						interfaceNames.AddIfMissing(name);
					else
						baseNames.AddIfMissing(name);
					
				if (type != null)
					DoGetParsedBases(globals, type, baseNames, interfaceNames, allNames);
				else
					m_database.GetBases(name, baseNames, interfaceNames, allNames);
			}
			
			return allNames.ToArray();
		}
		
		private void DoGetParsedBases(CsGlobalNamespace globals, CsType type, List<string> baseNames, List<string> interfaceNames, List<string> allNames)
		{
			baseNames.AddIfMissing("System.Object");
			allNames.AddIfMissing("System.Object");
			
			foreach (string name in type.Bases.Names)
			{
				string fullName = DoGetFullParsedName(globals, name);
				
				if (fullName != null)
				{
					if (CsHelpers.IsInterface(fullName))
						interfaceNames.AddIfMissing(fullName);
					else
						baseNames.AddIfMissing(fullName);
					
					allNames.AddIfMissing(fullName);
				}
			}
		}
		
		private string DoGetFullParsedName(CsGlobalNamespace globals, string inName)
		{
			string typeName = null;
			string name = DoGetRootName(inName);
			
			string candidate = name;
			if (DoHasType(candidate))
				typeName = candidate;
				
			for (int i = 0; i < globals.Namespaces.Length && typeName == null; ++i)
			{
				candidate = globals.Namespaces[i].Name + "." + name;
				if (DoHasType(candidate))
					typeName = candidate;
			}
			
			for (int i = 0; i < globals.Uses.Length && typeName == null; ++i)
			{
				candidate = globals.Uses[i].Namespace + "." + name;
				if (DoHasType(candidate))
					typeName = candidate;
			}
			
			return typeName;
		}
		
		private string DoGetRootName(string name)
		{
			string root = CsHelpers.GetRealName(name);
			
			// TODO: Improve this for the (rare) cause where we inherit from a bound generic
			// and one of the bound arguments is also a generic. If we wrote a real type parser
			// we could also use an in-memory database for parsed types which would make the
			// code simpler and better...
			if (name.Count(c => c == '<') == 1 && name.Count(c => c == '>') == 1)
			{
				int i = name.IndexOf('<');
				int j = name.IndexOf('>');
				
				int count = name.Substring(i, j- i).Count(c => c == ',') + 1;
				root = name.Substring(0, i) + '`' + count;
			}
			
			return root;
		}
		
		private bool DoHasType(string name)
		{
			bool has = false;
			
			if (m_database.HasType(name))
			{
				has = true;
			}
#if !TEST
			else if (m_parses != null)
			{
				has = m_parses.FindType(name) != null;
			}
#endif
			
			return has;
		}
		
		private void DoGetParsedMembers(CsType type, bool isInstance, bool isStatic, List<Item> items, bool includePrivates, bool includeProtected)
		{
			CsEnum e = type as CsEnum;
			if (e != null)
			{
				if (isStatic)
				{
					var candidates = from n in e.Names select new NameItem(n, type.FullName + ' ' + n, type.FullName, type.FullName);
					foreach (Item item in candidates)
					{
						items.AddIfMissing(item);
					}
				}
			}
			else
				DoGetParsedTypeMembers(type, isInstance, isStatic, items, includePrivates, includeProtected);
		}
		
		private void DoGetParsedExtensions(CsGlobalNamespace globals, string targetType, List<Item> items)
		{
			CsType[] types = DoGetAllParsedTypes(globals);
			
			foreach (CsType type in types)
			{
				foreach (CsMethod method in type.Methods)
				{
					if (method.IsExtension)
					{
						string fullName = DoGetFullParsedName(globals, method.Parameters[0].Type);
						
						if (targetType == fullName)
						{
							List<string> argTypes = (from p in method.Parameters select p.ModifiedType).ToList();
							List<string> argNames = (from p in method.Parameters select p.Name).ToList();
							
							argTypes.RemoveAt(0);
							argNames.RemoveAt(0);
							
							// TODO: should add gargs if they cannot be deduced
							items.AddIfMissing(new MethodItem(method.ReturnType, method.Name, null, argTypes.ToArray(), argNames.ToArray(), method.ReturnType, "extension methods"));
						}
					}
				}
			}
		}
		
		private CsType[] DoGetAllParsedTypes(CsGlobalNamespace globals)
		{
			var types = new List<CsType>();
			
#if !TEST
			types.AddRange(m_parses.FindTypes(null, string.Empty));
			
			for (int i = 0; i < globals.Namespaces.Length; ++i)
				types.AddRange(m_parses.FindTypes(globals.Namespaces[i].Name, string.Empty));
			
			for (int i = 0; i < globals.Uses.Length; ++i)
				types.AddRange(m_parses.FindTypes(globals.Uses[i].Namespace, string.Empty));
#endif
			
			return types.ToArray();
		}
		
		private bool DoShouldAdd(bool isInstance, bool isStatic, MemberModifiers modifiers)
		{
			if ((modifiers & MemberModifiers.Static) == 0 && (modifiers & MemberModifiers.Const) == 0)
				return isInstance;
			else
				return isStatic;
		}
		
		private void DoGetParsedTypeMembers(CsType type, bool isInstance, bool isStatic, List<Item> items, bool includePrivates, bool includeProtected)
		{
			foreach (CsField field in type.Fields)
			{
				if (DoShouldAdd(isInstance, isStatic, field.Modifiers))
					if (includePrivates || field.Access != MemberModifiers.Private)
						if (includeProtected || field.Access != MemberModifiers.Protected)
							items.AddIfMissing(new NameItem(field.Name, field.Type + ' ' + field.Name, type.FullName, field.Type));
			}
			
			foreach (CsMethod method in type.Methods)
			{
				if (!method.IsConstructor && !method.IsFinalizer)
				{
					if (DoShouldAdd(isInstance, isStatic, method.Modifiers))
					{
						if (includePrivates || method.Access != MemberModifiers.Private)
						{
							if (includeProtected || method.Access != MemberModifiers.Protected)
							{
								string[] argTypes = (from p in method.Parameters select p.ModifiedType).ToArray();
								string[] argNames = (from p in method.Parameters select p.Name).ToArray();
								
								// TODO: should add gargs if they cannot be deduced
								items.AddIfMissing(new MethodItem(method.ReturnType, method.Name, null, argTypes, argNames, method.ReturnType, type.FullName));
							}
						}
					}
				}
			}
			
			// Note that indexers are not counted because they are not preceded with a dot.
			foreach (CsProperty prop in type.Properties)
			{
				if (prop.HasGetter || prop.HasSetter)
				{
					if (DoShouldAdd(isInstance, isStatic, prop.Modifiers))
					{
						if (includePrivates || prop.Access != MemberModifiers.Private)
						{
							if (includeProtected || prop.Access != MemberModifiers.Protected)
							{
								string rtype = prop.HasGetter ? prop.ReturnType : "void";
								items.AddIfMissing(new NameItem(prop.Name, rtype + ' ' + prop.Name, type.FullName, rtype));
							}
						}
					}
				}
			}
		}
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
#if !TEST
		private IParses m_parses;
#endif
		#endregion
	}
}
