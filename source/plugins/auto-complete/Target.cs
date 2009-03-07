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
	internal sealed class Target
	{
		public Target(ITargetDatabase database, ICsLocalsParser locals)
		{
			m_database = database;
			m_locals = locals;
		}
		
		// Given the name of a type, local, argument, or field return true if we 
		// can find its type.
		public bool FindType(string text, string target, int offset, CsGlobalNamespace globals)
		{
			m_fullName = null;
			m_hash = null;
			m_type = null;
			m_instanceCall = true;
			
			if (globals != null)
			{
				// this.
				CsMember member = DoFindMember(globals, offset);
				DoHandleThis(member, target);
				
				// value. (special case for setters)
				if (m_hash == null && m_type == null)
					DoHandleValue(globals, member, target, offset);
				
				// MyType. (where MyType is a type in globals)
				if (m_hash == null && m_type == null)
					DoHandleLocalType(globals, target);
				
				// IDisposable. (where type name is present in the database)
				if (m_hash == null && m_type == null)
					DoHandleType(globals, target);
				
				// name. (where name is a local, argument, or field)
				if (m_hash == null && m_type == null)
					DoHandleVariable(text, globals, member, target, offset);
			}
			
			return m_hash != null || m_type != null;
		}
		
		// May be null.
		public CsType Type
		{
			get {return m_type;}
		}
		
		// May be null.
		public string Hash
		{
			get {return m_hash;}
		}
		
		// Valid if Type or Hash is non-null.
		public string FullTypeName
		{
			get {return m_fullName;}
		}
		
		// True if the target is an instance of the type. False if the target
		// is a type.
		public bool IsInstanceCall
		{
			get {return m_instanceCall;}
		}
		
		#region Private Methods
		private void DoHandleThis(CsMember member, string target)
		{
			if (target == "this")
			{
				if (member != null)
				{
					m_type = member.DeclaringType;
					m_fullName = DoGetTypeName(m_type.FullName);
					m_hash = m_database.FindAssembly(m_fullName);
					m_instanceCall = true;
Console.WriteLine("this type: {0}", m_fullName);
				}
			}
		}
		
		private void DoHandleValue(CsGlobalNamespace globals, CsMember member, string target, int offset)
		{
			if (target == "value")
			{
				string type = null;
				
				CsProperty prop = member as CsProperty;
				if (prop != null && prop.HasSetter)
				{
					if (prop.SetterBody.First < offset && offset <= prop.SetterBody.Last)
						type = prop.ReturnType;
				}
				
				CsIndexer i = member as CsIndexer;
				if (type == null && i != null && i.HasSetter)
				{
					if (i.SetterBody.First < offset && offset <= i.SetterBody.Last)
						type = i.ReturnType;
				}
				
				if (type != null)
				{
					type = DoGetTypeName(type);
					
					string fullName, hash;
					DoFindFullNameAndHash(globals, type, out fullName, out hash);
					
					if (hash != null)
					{
						m_fullName = fullName;
						m_hash = hash;
						m_type = DoFindLocalType(globals, m_fullName);
						m_instanceCall = false;
Console.WriteLine("value type: {0}", m_fullName);
					}
				}
			}
		}
		
		private void DoHandleLocalType(CsGlobalNamespace globals, string target)
		{
			CsType type = DoFindLocalType(globals, target);
			if (type != null)
			{
				m_type = type;
				m_fullName = DoGetTypeName(m_type.FullName);
				m_hash = m_database.FindAssembly(m_fullName);
				m_instanceCall = false;
Console.WriteLine("local type: {0}", m_fullName);
			}
		}
		
		private void DoHandleType(CsGlobalNamespace globals, string target)
		{
			string fullName, hash;
			DoFindFullNameAndHash(globals, target, out fullName, out hash);
			
			if (hash != null)
			{
				m_fullName = DoGetTypeName(fullName);
				m_hash = hash;
				m_type = DoFindLocalType(globals, m_fullName);
				m_instanceCall = false;
Console.WriteLine("global type: {0}", m_fullName);
			}
		}
		
		private void DoHandleVariable(string text, CsGlobalNamespace globals, CsMember member, string target, int offset)
		{
			string type = DoFindVariableType(text, globals, member, target, offset);
			
			if (type != null)
			{
				type = DoGetTypeName(type);
				
				string fullName, hash;
				DoFindFullNameAndHash(globals, type, out fullName, out hash);
				
				if (hash != null)
				{
					m_fullName = fullName;
					m_hash = hash;
					m_type = DoFindLocalType(globals, m_fullName);
					m_instanceCall = true;
Console.WriteLine("variable type: {0}", m_fullName);
				}
				else
Console.WriteLine("couldn't find a hash for {0} {1}", target, type);
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
			foreach (CsType candidate in outer.Types)
			{
				if (candidate.Name == target || candidate.FullName == target)
					return candidate;
			}
			
			foreach (CsNamespace inner in outer.Namespaces)
			{
				CsType type = DoFindLocalType(inner, target);
				if (type != null)
					return type;
			}
			
			return null;
		}
		
		private string DoFindVariableType(string text, CsGlobalNamespace globals, CsMember member, string target, int offset)
		{
			string type = null;
			
			if (member != null)
			{
				if (type == null)
					type = DoFindLocalType(text, member, target, offset);
				
				if (type == null)
					type = DoFindArgType(member, target);
				
				if (type == null)
					type = DoFindFieldType(globals, member, target);
			}
			
			return type;
		}
		
		private string DoFindLocalType(string text, CsMember member, string name, int offset)
		{
			string type = null;
			
			CsBody body = DoGetBody(member, offset);
			if (body != null)
			{
				Local[] locals = m_locals.Parse(text, body.Start, offset);
				for (int i = locals.Length - 1; i >= 0 && type == null; --i)		// note that we want to use the last match
				{
					if (locals[i].Name == name)
					{
						type = locals[i].Type;		// TODO: need to handle "var" types
					}
				}
			}
			
			return type;
		}
		
		private CsBody DoGetBody(CsMember member, int offset)
		{
			CsBody body = null;
			
			CsIndexer i = member as CsIndexer;
			if (body == null && i != null)
			{
				if (i.SetterBody != null)
					if (i.SetterBody.First < offset && offset <= i.SetterBody.Last)
						body = i.SetterBody;
				
				if (body == null && i.GetterBody != null)
					if (i.GetterBody.First < offset && offset <= i.GetterBody.Last)
						body = i.GetterBody;
			}
			
			CsMethod method = member as CsMethod;
			if (body == null && method != null && method.Body != null)
			{
				if (method.Body.First < offset && offset <= method.Body.Last)
					body = method.Body;
			}
			
			CsOperator op = member as CsOperator;
			if (body == null && op != null && op.Body != null)
			{
				if (op.Body.First < offset && offset <= op.Body.Last)
					body = op.Body;
			}
			
			CsProperty prop = member as CsProperty;
			if (body == null && prop != null)
			{
				if (prop.SetterBody != null)
					if (prop.SetterBody.First < offset && offset <= prop.SetterBody.Last)
						body = prop.SetterBody;
				
				if (body == null && prop.GetterBody != null)
					if (prop.GetterBody.First < offset && offset <= prop.GetterBody.Last)
						body = prop.GetterBody;
			}
			
			return body;
		}
		
		private string DoFindArgType(CsMember member, string name)
		{
			string type = null;
			
			do
			{
				CsIndexer i = member as CsIndexer;
				if (i != null)
				{
					type = DoFindParamType(i.Parameters, name);
					break;
				}
				
				CsMethod m = member as CsMethod;
				if (m != null)
				{
					type = DoFindParamType(m.Parameters, name);
					break;
				}
				
				CsOperator o = member as CsOperator;
				if (o != null)
				{
					type = DoFindParamType(o.Parameters, name);
					break;
				}
			}
			while (false);
			
			return type;
		}
		
		private string DoFindFieldType(CsGlobalNamespace globals, CsMember member, string name)
		{
			string type = null;
			
			for (int i = 0; i < member.DeclaringType.Fields.Length && type == null; ++i)
			{
				CsField field = member.DeclaringType.Fields[i];
				if (field.Name == name)
					type = field.Type;
			}
			
			if (type == null && member.DeclaringType.Bases.Names.Length > 0)
			{
				string baseType = member.DeclaringType.Bases.Names[0];
				if (baseType[0] != 'I' || baseType.Length == 1 || char.IsLower(baseType[1]))
				{
					string fullName, hash;
					DoFindFullNameAndHash(globals, baseType, out fullName, out hash);
					
					type = m_database.FindFieldType(fullName, name);
				}
			}
			
			return type;
		}
		
		private string DoFindParamType(CsParameter[] parms, string name)
		{
			foreach (CsParameter p in parms)
			{
				if (p.Name == name)
					return p.Type;
			}
			
			return null;
		}
		
		// Find the last member offset intersects.
		private CsMember DoFindMember(CsNamespace ns, int offset)
		{
			CsMember member = null;
			
			for (int i = 0; i < ns.Namespaces.Length && member == null; ++i)
			{
				member = DoFindMember(ns.Namespaces[i], offset);
			}
			
			for (int i = 0; i < ns.Types.Length && member == null; ++i)
			{
				CsType type = ns.Types[i];
				
				for (int j = 0; j < type.Members.Length && member == null; ++j)
				{
					CsMember candidate = type.Members[j];
					if (candidate.Offset <= offset && offset < candidate.Offset + candidate.Length)
						member = candidate;
				}
			}
			
			return member;
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
		private bool m_instanceCall;
		private CsType m_type;
		private ICsLocalsParser m_locals;
		#endregion
	}
}
