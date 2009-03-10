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
		public ResolvedTarget Resolve(string type, CsGlobalNamespace globals, bool isInstance)
		{
			m_fullName = null;
			m_hash = null;
			m_type = null;
			
			type = DoGetTypeName(type);
			
			if (m_hash == null && m_type == null)
				DoHandleLocalType(globals, type);
			
			if (m_hash == null && m_type == null)
				DoHandleDatabaseType(globals, type);
			
			return m_hash != null || m_type != null
				? new ResolvedTarget(m_fullName, m_type, m_hash, isInstance)
				: null;
		}
		
		public ResolvedTarget Resolve(CsType type, bool isInstance)
		{
			m_type = type;
			m_fullName = DoGetTypeName(m_type.FullName);
			m_hash = m_database.FindAssembly(m_fullName);
			
			return new ResolvedTarget(m_fullName, m_type, m_hash, isInstance);
		}
		
		public ResolvedTarget Resolve(string fullName, bool isInstance)
		{
			m_fullName = DoGetTypeName(fullName);
			m_hash = m_database.FindAssembly(m_fullName);
			
			return m_hash != null
				? new ResolvedTarget(m_fullName, null, m_hash, isInstance)
				: null;
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
					m_hash = m_database.FindAssembly(m_fullName);
				}
				else
				{
					m_type = type;
					m_fullName = DoGetTypeName(m_type.FullName);
					m_hash = m_database.FindAssembly(m_fullName);
				}
			}
		}
		
		private void DoHandleDatabaseType(CsGlobalNamespace globals, string target)
		{
			string fullName, hash;
			DoFindFullNameAndHash(globals, target, out fullName, out hash);
			
			if (hash != null)
			{
				m_fullName = DoGetTypeName(fullName);
				m_hash = hash;
				m_type = DoFindLocalType(globals, m_fullName);
			}
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
		
		private void DoFindFullNameAndHash(CsGlobalNamespace globals, string target, out string fullName, out string hash)
		{
			fullName = DoGetAliasedName(target);
			hash = m_database.FindAssembly(fullName);
			
			for (int i = 0; i < globals.Uses.Length && hash == null; ++i)
			{
				fullName = globals.Uses[i].Namespace + "." + target;
				hash = m_database.FindAssembly(fullName);
			}
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
		
		// TODO: duplicate of FindInDatabase.DoGetRealName
		private string DoGetAliasedName(string name)
		{
			switch (name)
			{
				case "bool":
					return "System.Boolean";
					
				case "byte":
					return "System.Byte";
					
				case "char":
					return "System.Char";
					
				case "decimal":
					return "System.Decimal";
					
				case "double":
					return "System.Double";
					
				case "short":
					return "System.Int16";
					
				case "int":
					return "System.Int32";
					
				case "long":
					return "System.Int64";
				
				case "sbyte":
					return "System.SByte";
					
				case "object":
					return "System.Object";
					
				case "float":
					return "System.Single";
					
				case "string":
					return "System.String";
					
				case "ushort":
					return "System.UInt16";
					
				case "uint":
					return "System.UInt32";
					
				case "ulong":
					return "System.UInt64";
					
				case "void":
					return "System.Void";
					
				default:
					return name;
			}
		}
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		private string m_fullName;
		private string m_hash;
		private CsType m_type;
		#endregion
	}
}
