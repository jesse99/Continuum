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
using System.Text.RegularExpressions;

namespace AutoComplete
{
	// Used to resolve a simple name (e.g. locals in scope, arguments, etc).
	internal sealed class ResolveName
	{
		public ResolveName(ITargetDatabase database, ICsLocalsParser locals, string text, int offset, CsGlobalNamespace globals)
		{
			m_database = database;
			m_typeResolver = new ResolveType(database);
			m_globals = globals;
			m_offset = offset;
			
			m_member = DoFindMember(m_globals);
			m_variables = DoGetVariables(text, locals);
		}
		
		// Returns all of the names which may be used at the specified offset in the code.
		public Variable[] Variables
		{
			get {return m_variables;}
		}
		
		// May return null.
		public ResolvedTarget Resolve(string name)
		{
			ResolvedTarget result = null;
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving name");
			Log.WriteLine("AutoComplete", "name: {0}", name);
			
			// this.
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying this name");
				result = DoHandleThis(name);
			}
			
			// SomeType.
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying type name");
				result = m_typeResolver.Resolve(name, m_globals, false, true);
			}
			
			// name. (where name is a local, argument, etc)
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying variable name");
				result = DoHandleVariable(name);
			}
			
			// char literal (we don't complete integers because it's too annoying for the common case
			// of the dot being a floating point indicator instead of a method call)
			if (result == null)
			{
				if (name.Length >= 2 && name[0] == '\'' && name.Last() == '\'')
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying char literal name");
					result = m_typeResolver.Resolve("System.Char", m_globals, true, false);
				}
			}
				
			// string literal
			if (result == null)
			{
				if (name.Length >= 2 && name[0] == '"' && name.Last() == '"')
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying string literal name");
					result = m_typeResolver.Resolve("System.String", m_globals, true, false);
				}
			}
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---- name {0} -> {1}", name, result);
			
			return result;
		}
		
		#region Private Methods
		// This is a bit overkill if all we want to do is identify the name, but we also
		// need this info to do expression completion.
		private Variable[] DoGetVariables(string text, ICsLocalsParser locals)
		{
			var vars = new List<Variable>();
			
			if (m_member != null)
			{
				// this
				if (m_member.DeclaringType != null)
					if ((m_member.Modifiers & MemberModifiers.Static) == 0)
						vars.Add(new Variable(m_member.DeclaringType.FullName, "this", null));
				
				// value
				CsProperty prop = m_member as CsProperty;
				if (prop != null && prop.SetterBody != null)
				{
					if (prop.SetterBody.First < m_offset && m_offset <= prop.SetterBody.Last)
						vars.Add(new Variable(prop.ReturnType, "value", null));
				}
				
				CsIndexer indexer = m_member as CsIndexer;
				if (indexer != null && indexer.SetterBody != null)
				{
					if (indexer.SetterBody.First < m_offset && m_offset <= indexer.SetterBody.Last)
						vars.Add(new Variable(indexer.ReturnType, "value", null));
				}
			}
			
			// locals
			CsBody body = DoGetBody();
			if (body != null)
			{
				Local[] candidates = locals.Parse(text, body.Start, m_offset);
				for (int i = candidates.Length - 1; i >= 0; --i)
				{
					if (!vars.Exists(v => v.Name == candidates[i].Name))
						vars.Add(new Variable(candidates[i].Type, candidates[i].Name, candidates[i].Value));
				}
			}
			
			// args
			CsParameter[] parms = DoGetParameters();
			if (parms != null)
			{
				foreach (CsParameter p in parms)
				{
					if (!vars.Exists(v => v.Name == p.Name))
						vars.Add(new Variable(p.Type, p.Name, null));
				}
			}
			
			if (Log.IsEnabled(TraceLevel.Verbose, "AutoComplete"))
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "Variables:");
				foreach (Variable v in vars)
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "    {0}   {1}", v.Type, v.Name);
				}
			}
			
			return vars.ToArray();
		}
		
		private CsParameter[] DoGetParameters()
		{
			CsIndexer indexer = m_member as CsIndexer;
			if (indexer != null)
				return indexer.Parameters;
			
			CsMethod method = m_member as CsMethod;
			if (method != null)
				return method.Parameters;
			
			CsOperator op = m_member as CsOperator;
			if (op != null)
				return op.Parameters;
				
			return null;
		}
		
		private ResolvedTarget DoHandleThis(string name)
		{
			ResolvedTarget result = null;
			
			if (name == "this" || name == "<this>")
			{
				if (m_member != null && (m_member.Modifiers & MemberModifiers.Static) == 0)
				{
					bool isInstance = (m_member.Modifiers & MemberModifiers.Static) == 0;
					bool isStatic = name == "<this>" || (m_member.Modifiers & MemberModifiers.Static) != 0;
					
					result = m_typeResolver.Resolve(m_member.DeclaringType, isInstance, isStatic);
				}
			}
			
			return result;
		}
		
		private ResolvedTarget DoHandleVariable(string name)
		{
			ResolvedTarget result = null;
			
			for (int i = 0; i < m_variables.Length && result == null; ++i)
			{
				if (m_variables[i].Name == name)
				{
					string type = m_variables[i].Type;
					if (type == "var" && m_variables[i].Value != null)
					{
						string value = m_variables[i].Value;
						if (value.StartsWith("new"))
						{
							// var result = new Type(...);
							value = DoGetNewValue(value);
						}
						else if (value.StartsWith("from "))
						{
							// var result = from ...;
							value = "System.Collections.Generic.IEnumerable`1";
						}
						else if (CsHelpers.IsIdentifier(value))
						{
							// var result = name; where name is a property or field.
							ResolvedTarget target = m_typeResolver.Resolve(m_member.DeclaringType.FullName, m_globals, true, true);
							if (target != null)
							{
								var resolveMembers = new ResolveMembers(m_database);
								Member[] members = resolveMembers.Find(target, m_globals, value, 0);
								if (members.Length > 0)
								{
									if (members.All(m => m.Type == members[0].Type))
									{
										result = m_typeResolver.Resolve(members[0].Type, m_globals, target.IsInstance, target.IsStatic);
									}
									else
									{
										Log.WriteLine("AutoComplete", "{0} has an ambiguous return type:", value);
										foreach (Member member in members)
										{
											Log.WriteLine("AutoComplete", "    {0} {1}", member.Type, member);
										}
									}
								}
							}
						}
						else
						{
							// var result = Get<Type>();
							// TODO: need something more general here 
							Match m = ms_getRE.Match(value);
							if (m.Success)
							{
								value = m.Groups[1].Value;	
							}
						}
						
						if (result == null)
							result = Resolve(value);
						
						if (result != null)
						{
							result = new ResolvedTarget(result.TypeName, result.Type, true, false);	// we resolved a type, but it's used as an instance...
						}
					}
					else
					{
						result = m_typeResolver.Resolve(type, m_globals, true, false);
					}
				}
			}
			
			return result;
		}
		
		private string DoGetNewValue(string value)
		{
			int i = 3;
			while (i < value.Length && char.IsWhiteSpace(value[i]))
				++i;
			
			int count = 0;
			while (i + count < value.Length)
			{
				if (char.IsLetterOrDigit(value[i + count]))
				{
					++count;
				}
				else if (value[i + count] == '_' || value[i + count] == '.')
				{
					++count;
				}
				else if (value[i + count] == '<')
				{
					int num = 1;
					++count;
					
					while (i + count < value.Length && num > 0)
					{
						if (value[i + count] == '<')
							++num;
						else if (value[i + count] == '>')
							--num;
						
						++count;
					}
				}
				else
					break;
			}
			
			return value.Substring(i, count);
		}
		
		private CsBody DoGetBody()
		{
			CsBody body = null;
			
			CsIndexer i = m_member as CsIndexer;
			if (body == null && i != null)
			{
				if (i.SetterBody != null)
					if (i.SetterBody.First < m_offset && m_offset <= i.SetterBody.Last)
						body = i.SetterBody;
				
				if (body == null && i.GetterBody != null)
					if (i.GetterBody.First < m_offset && m_offset <= i.GetterBody.Last)
						body = i.GetterBody;
			}
			
			CsMethod method = m_member as CsMethod;
			if (body == null && method != null && method.Body != null)
			{
				if (method.Body.First < m_offset && m_offset <= method.Body.Last)
					body = method.Body;
			}
			
			CsOperator op = m_member as CsOperator;
			if (body == null && op != null && op.Body != null)
			{
				if (op.Body.First < m_offset && m_offset <= op.Body.Last)
					body = op.Body;
			}
			
			CsProperty prop = m_member as CsProperty;
			if (body == null && prop != null)
			{
				if (prop.SetterBody != null)
					if (prop.SetterBody.First < m_offset && m_offset <= prop.SetterBody.Last)
						body = prop.SetterBody;
				
				if (body == null && prop.GetterBody != null)
					if (prop.GetterBody.First < m_offset && m_offset <= prop.GetterBody.Last)
						body = prop.GetterBody;
			}
			
			return body;
		}
		
		// Find the last m_member offset intersects.
		private CsMember DoFindMember(CsNamespace ns)
		{
			CsMember member = null;
			
			for (int i = 0; i < ns.Namespaces.Length && member == null; ++i)
			{
				member = DoFindMember(ns.Namespaces[i]);
			}
			
			for (int i = 0; i < ns.Types.Length && member == null; ++i)
			{
				CsType type = ns.Types[i];
				
				for (int j = 0; j < type.Members.Length && member == null; ++j)
				{
					CsMember candidate = type.Members[j];
					if (candidate.Offset <= m_offset && m_offset < candidate.Offset + candidate.Length)
						member = candidate;
				}
			}
			
			return member;
		}
		#endregion
		
		#region Fields
		private ITargetDatabase m_database;
		private ResolveType m_typeResolver;
		private Variable[] m_variables;
		private int m_offset;
		private CsMember m_member;
		private CsGlobalNamespace m_globals;
		
		private Regex ms_getRE = new Regex(@"\w+ \. Get \s* < \s* (\w+) \s* > \s* \( \s* \)", RegexOptions.IgnorePatternWhitespace);
		#endregion
	}
}
