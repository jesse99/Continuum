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
	// Resolves a simple expression into a type. The expression can be things like
	// "this", a type name, a local variable, an argument, a field, etc.
	internal sealed class ResolveTarget
	{
		public ResolveTarget(ITargetDatabase database, ICsLocalsParser locals)
		{
			m_database = database;
			m_locals = locals;
			m_resolveType = new ResolveType(database);
		}
		
		// May return null.
		public ResolvedTarget Resolve(string text, string target, int offset, CsGlobalNamespace globals)
		{
			// this.
			CsMember member = DoFindMember(globals, offset);
			ResolvedTarget result = DoHandleThis(member, target);
			
			// value. (special case for setters)
			if (result == null)
				result = DoHandleValue(globals, member, target, offset);
			
			// SomeType.
			if (result == null)
			{
				result = m_resolveType.Resolve(target, globals, false);
if (result != null)
	Console.WriteLine("found type: {0}", result.FullName);
			}
			
			// name. (where name is a local, argument, or field)
			if (result == null)
				result = DoHandleVariable(text, globals, member, target, offset);
			
			return result;
		}
		
		#region Private Methods
		private ResolvedTarget DoHandleThis(CsMember member, string target)
		{
			ResolvedTarget result = null;
			
			if (target == "this")
			{
				if (member != null)
				{
					result = m_resolveType.Resolve(member.DeclaringType, true);
if (result != null)
	Console.WriteLine("found this: {0}", result.FullName);
				}
			}
			
			return result;
		}
		
		private ResolvedTarget DoHandleValue(CsGlobalNamespace globals, CsMember member, string target, int offset)
		{
			ResolvedTarget result = null;
			
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
					result = m_resolveType.Resolve(type, globals, true);
if (result != null)
	Console.WriteLine("found value: {0}", result.FullName);
				}
			}
			
			return result;
		}
		
		private ResolvedTarget DoHandleVariable(string text, CsGlobalNamespace globals, CsMember member, string target, int offset)
		{
			ResolvedTarget result = null;
			
			if (member != null)
			{
				if (result == null)
					result = DoFindLocalType(text, globals, member, target, offset);
				
				if (result == null)
					result = DoFindArgType(globals, member, target);
				
				if (result == null)
					result = DoFindFieldType(globals, member, target);
			}
			
			return result;
		}
		
		private ResolvedTarget DoFindLocalType(string text, CsGlobalNamespace globals, CsMember member, string name, int offset)
		{
			ResolvedTarget result = null;
			
			CsBody body = DoGetBody(member, offset);
			if (body != null)
			{
				Local[] locals = m_locals.Parse(text, body.Start, offset);
				for (int i = locals.Length - 1; i >= 0 && result == null; --i)		// note that we want to use the last match
				{
					if (locals[i].Name == name)
					{
						string type = locals[i].Type;		// TODO: need to handle "var" types
						result = m_resolveType.Resolve(type, globals, true);
if (result != null)
	Console.WriteLine("found local: {0}", result.FullName);
					}
				}
			}
			
			return result;
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
		
		private ResolvedTarget DoFindArgType(CsGlobalNamespace globals, CsMember member, string name)
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
			
			ResolvedTarget result = null;
			if (type != null)
			{
				result = m_resolveType.Resolve(type, globals, true);
if (result != null)
	Console.WriteLine("found arg: {0}", result.FullName);
			}
			
			return result;
		}
		
		private ResolvedTarget DoFindFieldType(CsGlobalNamespace globals, CsMember member, string name)
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
					ResolvedTarget baseTarget = m_resolveType.Resolve(baseType, globals, false);
					if (baseTarget != null)
					{
						type = m_database.FindFieldType(baseTarget.FullName, name);
					}
				}
			}
			
			ResolvedTarget result = null;
			if (type != null)
			{
				result = m_resolveType.Resolve(type, globals, true);
if (result != null)
	Console.WriteLine("found field: {0}", result.FullName);
			}
			
			return result;
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
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		private ICsLocalsParser m_locals;
		private ResolveType m_resolveType;
		#endregion
	}
}
