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
			
#if !TEST
			Boss boss = ObjectModel.Create("CsParser");
			m_parses = boss.Get<IParses>();
#endif
		}
		
		// May return null.
		public ResolvedTarget Resolve(string type, CsGlobalNamespace globals, bool isInstance, bool isStatic)
		{
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			Profile.Start("ResolveType::Resolve1");
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving type");
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "type: {0}", type);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isInstance: {0}", isInstance);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isStatic: {0}", isStatic);
			
#if TEST
			CsParser.Parses parses = new CsParser.Parses();
			parses.AddParse(type, globals);
			m_parses = parses;
#endif
			
			ResolvedTarget result = null;
			
			m_fullName = null;
			m_type = null;
			
			type = DoGetTypeName(type);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "used type: {0}", type);
			
			DoResolve(globals, type);
			if (m_fullName != null || m_type != null)
				result = new ResolvedTarget(m_fullName, m_type, isInstance, isStatic);
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---- type {0} -> {1}", type, result);
			
			Profile.Stop("ResolveType::Resolve1");
			return result;
		}
		
		public ResolvedTarget Resolve(CsType type, bool isInstance, bool isStatic)
		{
			Contract.Requires(type != null, "type is null or empty");
			Profile.Start("ResolveType::Resolve2");
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving type");
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "cs type: {0}", type);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isInstance: {0}", isInstance);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isStatic: {0}", isStatic);
			
			m_type = type;
			m_fullName = DoGetTypeName(m_type.FullName);
			
			ResolvedTarget result = new ResolvedTarget(m_fullName, m_type, isInstance, isStatic);
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---- cs type {0} -> {1}", type, result);
			
			Profile.Stop("ResolveType::Resolve2");
			return result;
		}
		
		public ResolvedTarget Resolve(string fullName, bool isInstance, bool isStatic)
		{
			Contract.Requires(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Profile.Start("ResolveType::Resolve3");
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving type");
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "fullName: {0}", fullName);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isInstance: {0}", isInstance);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "isStatic: {0}", isStatic);
			
			m_fullName = DoGetTypeName(fullName);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "used type: {0}", m_fullName);
			
			ResolvedTarget result = new ResolvedTarget(m_fullName, null, isInstance, isStatic);
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---- fullName type {0} -> {1}", fullName, result);
			
			Profile.Stop("ResolveType::Resolve3");
			return result;
		}
		
		#region Private Methods
		private void DoResolve(CsGlobalNamespace globals, string target)
		{
			string name = target;
			name = DoGetTypeName(name);
			name = CsHelpers.GetRealName(name);
			
			m_fullName = null;
			m_type = null;
			DoFindType(name);
			
			for (int i = 0; i < globals.Namespaces.Length && m_fullName == null; ++i)
			{
				string candidate = globals.Namespaces[i].Name + "." + name;
				DoFindType(candidate);
			}
			
			for (int i = 0; i < globals.Uses.Length && m_fullName == null; ++i)
			{
				string candidate = globals.Uses[i].Namespace + "." + name;
				DoFindType(candidate);
			}
			
			if (m_fullName != null)
				if (m_type != null)
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "ResolveType is using parsed {0}", m_fullName);
				else
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "ResolveType is using db {0}", m_fullName);
		}
		
		private void DoFindType(string fullName)
		{
			Contract.Assert(m_fullName == null, "m_fullName is not null");
			Contract.Assert(m_type == null, "m_fullName is not null");
			
			CsType type = m_parses.FindType(fullName);
			if (type != null)
			{
				if (type is CsDelegate)
				{
					fullName = "System.Delegate";
				}
				else
				{
					m_type = type;
					m_fullName = DoGetTypeName(m_type.FullName);
				}
			}
			
			if (m_fullName == null && m_database.HasType(fullName))
			{
				m_fullName = DoGetTypeName(fullName);
			}
		}
		
		private string DoGetTypeName(string name)
		{
			if (name.EndsWith("[]"))
				return "array-type";
			
			else if (name.EndsWith("?"))
				return "nullable-type";
			
			else if (name.EndsWith("*"))
				return "pointer-type";
			
			// generic names should be Foo`1 not Foo`<T>
			else if (name.Contains("`"))
				return DoGetGenericName1(name);
				
			// generic names should be Foo`1 not Foo<T>
			else if (name.Contains("<"))
				return DoGetGenericName2(name);
				
			return name;
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
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		private string m_fullName;
		private CsType m_type;
		private IParses m_parses;
		#endregion
	}
}
