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

namespace AutoComplete
{
	// Resolves a type name using globals and the database.
	internal sealed class ResolveType
	{
		public ResolveType(ITargetDatabase database)
		{
			m_database = database;
		}
		
		// May return null.
		public ResolvedTarget Resolve(string type, CsGlobalNamespace globals, bool isInstance, bool isStatic)
		{
			Trace.Assert(!string.IsNullOrEmpty(type), "type is null or empty");
			
			m_fullName = null;
			m_type = null;
			
			type = DoGetTypeName(type);
			
			if (m_type == null)
				DoHandleLocalType(globals, type);
			
			if (m_type == null)
				DoHandleDatabaseType(globals, type);
			
			return m_type != null
				? new ResolvedTarget(m_fullName, m_type, isInstance, isStatic)
				: null;
		}
		
		public ResolvedTarget Resolve(CsType type, bool isInstance, bool isStatic)
		{
			Trace.Assert(type != null, "type is null or empty");
			
			m_type = type;
			m_fullName = DoGetTypeName(m_type.FullName);
			
			return new ResolvedTarget(m_fullName, m_type, isInstance, isStatic);
		}
		
		public ResolvedTarget Resolve(string fullName, bool isInstance, bool isStatic)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			m_fullName = DoGetTypeName(fullName);
			
			return new ResolvedTarget(m_fullName, null, isInstance, isStatic);
		}
		
		public IEnumerable<ResolvedTarget> GetBases(CsGlobalNamespace globals, string fullName, bool isInstance, bool isStatic)
		{
			if (fullName == "xxxx")
				yield return null;
				
#if false
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			ResolvedTarget target = Resolve(fullName, globals, isInstance, isStatic);
			if (target != null && CsHelpers.IsInterface(target.TypeName))
			{
				var names = new List<string>();
				names.AddRange(m_database.FindInterfaces(target.FullName));
				names.Add("System.Object");
				
				while (names.Count > 0)
				{
					string name = names[names.Count - 1];
					names.RemoveLast();
					names.AddRange(m_database.FindInterfaces(name));
					
					target = Resolve(name, globals, isInstance, isStatic);
					yield return target;
				}
			}
			else
			{
				while (target != null && target.FullName != "System.Object")
				{
					if (target.Type != null)
					{
						if (target.Type.Bases.HasBaseClass)
							target = Resolve(target.Type.Bases.Names[0], globals, isInstance, isStatic);
						else
							target = Resolve("System.Object", globals, isInstance, isStatic);
					}
					else if (target.Type == null && target.Hash != null)
					{
						string name = m_database.FindBaseType(target.FullName);
						if (!string.IsNullOrEmpty(name))
							target = Resolve(name, globals, isInstance, isStatic);
						else
							target = Resolve("System.Object", globals, isInstance, isStatic);
					}
					else
						target = Resolve("System.Object", globals, isInstance, isStatic);
						
					if (target != null)
						yield return target;
				}
			}
#endif
		}
		
		#region Private Methods
		private void DoHandleLocalType(CsGlobalNamespace globals, string target)
		{
			CsType type = DoFindLocalType(globals, target);
			if (type != null)
			{
				if (type is CsDelegate)
				{
					m_fullName = "System.Delegate";
				}
				else
				{
					m_type = type;
					m_fullName = DoGetTypeName(m_type.FullName);
				}
			}
		}
		
		private void DoHandleDatabaseType(CsGlobalNamespace globals, string target)
		{
			string fullName = DoFindFullName(globals, target);
			
			m_fullName = DoGetTypeName(fullName);
			m_type = DoFindLocalType(globals, m_fullName);
		}
		
		private string DoGetTypeName(string fullName)
		{
			if (fullName.EndsWith("[]"))
				return "System.Array";
			
			else if (fullName.EndsWith("?"))
				return "System.Nullable`1";
			
			// generic names should be Foo`1 not Foo`<T>
			else if (fullName.Contains("`"))
				return DoGetGenericName1(fullName);
				
			// generic names should be Foo`1 not Foo<T>
			else if (fullName.Contains("<"))
				return DoGetGenericName2(fullName);
				
			return fullName;
		}
		
		private string DoGetGenericName1(string name)	// TODO: duplicate of GetTypeName from object-model
		{
			int i = name.IndexOf('`');
			
			if (i > 0)
			{
				++i;
				while (i < name.Length && char.IsDigit(name[i]))
				{
					++i;
				}
			}
			
			if (i > 0)
				name = name.Substring(0, i);
				
			return name;
		}
		
		private string DoGetGenericName2(string name)
		{
			int i = name.IndexOf('<');
			int j = name.LastIndexOf('>');
			
			if (i > 0 && j > i)
			{
				string args = name.Substring(i + 1, j - i - 1);
				int count = args.Split(',').Length;
				name = name.Substring(0, i) + "`" + count;
			}
			
			return name;
		}
		
		private string  DoFindFullName(CsGlobalNamespace globals, string target)
		{
			string fullName = null;
			
			target = CsHelpers.GetRealName(target);
			
			for (int i = 0; i < globals.Namespaces.Length && fullName == null; ++i)
			{
				string candidate = globals.Namespaces[i].Name + "." + target;
				if (m_database.HasType(candidate))
					fullName = candidate;
			}
			
			for (int i = 0; i < globals.Uses.Length && fullName == null; ++i)
			{
				string candidate = globals.Uses[i].Namespace + "." + target;
				if (m_database.HasType(candidate))
					fullName = candidate;
			}
			
			if (fullName == null)
				fullName = target;
			
			return fullName;
		}
		
		private CsType DoFindLocalType(CsNamespace outer, string target)
		{
			CsType result = DoFindType(outer, target);
			
			for (int i = 0; i < outer.Namespaces.Length && result == null; ++i)
				result = DoFindLocalType(outer.Namespaces[i], target);
			
			return result;
		}
		
		private CsType DoFindType(CsTypeScope scope, string target)
		{
			CsType result = null;
			
			CsType candidate = scope as CsType;
		
			if (candidate != null && (candidate.Name == target || candidate.FullName == target))
				result = candidate;
			
			for (int i = 0; i < scope.Types.Length && result == null; ++i)
				result = DoFindType(scope.Types[i], target);
			
			return result;
		}
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		private string m_fullName;
		private CsType m_type;
		#endregion
	}
}
