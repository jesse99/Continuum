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
using System.Text.RegularExpressions;

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
		public Tuple2<ResolvedTarget, Variable[]> Resolve(string text, string target, int offset, CsGlobalNamespace globals)
		{
			// this.
			CsMember member = DoFindMember(globals, offset);
			Variable[] vars = DoGetVariables(text, member, globals, offset);
			ResolvedTarget result = DoHandleThis(member, target);
			
			// value. (special case for setters)
			if (result == null && target == "value")
				result = DoHandleVariable(text, offset, target, vars, globals);
			
			// SomeType.
			if (result == null)
			{
				result = m_resolveType.Resolve(target, globals, false, true);
				if (result != null)
					Log.WriteLine("AutoComplete", "found type: {0}", result.FullName);
			}
			
			// name. (where name is a local, argument, field, or property)
			if (result == null)
				result = DoHandleVariable(text, offset, target, vars, globals);
			
			return Tuple.Make(result, vars);
		}
				
		#region Private Methods
		// This is a bit overkill if all we want to do is identify the target, but we also
		// need this info to do arg completion.
		private Variable[] DoGetVariables(string text, CsMember member, CsGlobalNamespace globals, int offset)
		{
			var vars = new List<Variable>();
			
			if (member != null)
			{
				// this
				if (member.DeclaringType != null)
					if ((member.Modifiers & MemberModifiers.Static) == 0)
						vars.Add(new Variable(member.DeclaringType.FullName, "this", null));
				
				// value
				CsProperty prop = member as CsProperty;
				if (prop != null && prop.SetterBody != null)
				{
					if (prop.SetterBody.First < offset && offset <= prop.SetterBody.Last)
						vars.Add(new Variable(prop.ReturnType, "value", null));
				}
				
				CsIndexer indexer = member as CsIndexer;
				if (indexer != null && indexer.SetterBody != null)
				{
					if (indexer.SetterBody.First < offset && offset <= indexer.SetterBody.Last)
						vars.Add(new Variable(indexer.ReturnType, "value", null));
				}
			}
			
			// locals
			CsBody body = DoGetBody(member, offset);
			if (body != null)
			{
				Local[] locals = m_locals.Parse(text, body.Start, offset);
				for (int i = locals.Length - 1; i >= 0; --i)
				{
					if (!vars.Exists(v => v.Name == locals[i].Name))
						vars.Add(new Variable(locals[i].Type, locals[i].Name, locals[i].Value));
				}
			}
			
			// args
			CsParameter[] parms = DoGetParameters(member);
			if (parms != null)
			{
				foreach (CsParameter p in parms)
				{
					if (!vars.Exists(v => v.Name == p.Name))
						vars.Add(new Variable(p.Type, p.Name, null));
				}
			}
			
			// fields and properties
			if (member != null && member.DeclaringType != null)
			{
				ResolvedTarget type = m_resolveType.Resolve(member.DeclaringType.FullName, globals, true, false);
				if (type != null)
				{
					DoFindFields(globals, type, vars, member);
					DoFindProperties(globals, type, vars, member);
				}
				
				if (member.DeclaringType.Bases.HasBaseClass)
				{
					foreach (ResolvedTarget t in m_resolveType.GetBases(globals, member.DeclaringType.FullName, true, false))
					{
						DoFindFields(globals, t, vars, member);
						DoFindProperties(globals, t, vars, member);
					}
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
		
		private void DoFindFields(CsGlobalNamespace globals, ResolvedTarget type, List<Variable> vars, CsMember member)
		{
			if (type.Type != null)
			{
				for (int i = 0; i < type.Type.Fields.Length; ++i)
				{
					CsField field = type.Type.Fields[i];
					if (member == null || (member.Modifiers & MemberModifiers.Static) == 0 || (field.Modifiers & MemberModifiers.Static) != 0)
						if (!vars.Exists(v => v.Name == field.Name))
							vars.Add(new Variable(field.Type, field.Name, null));
				}
			}
			else if (type.Hash != null)
			{
				var names = m_database.FindFields(type.FullName, member == null || (member.Modifiers & MemberModifiers.Static) == 0);
				foreach (var name in names)
				{
					if (!vars.Exists(v => v.Name == name.Second))
						vars.Add(new Variable(name.First, name.Second, null));
				}
			}
		}
		
		private void DoFindProperties(CsGlobalNamespace globals, ResolvedTarget type, List<Variable> vars, CsMember member)
		{
			if (type.Type != null)
			{
				for (int i = 0; i < type.Type.Properties.Length; ++i)
				{
					CsProperty prop = type.Type.Properties[i];
					if (prop.HasGetter)
						if (member == null || (member.Modifiers & MemberModifiers.Static) == 0 || (prop.Modifiers & MemberModifiers.Static) != 0)
							if (!vars.Exists(v => v.Name == prop.Name))
								vars.Add(new Variable(prop.ReturnType, prop.Name, null));
				}
			}
			else if (type.Hash != null)
			{
				var names = m_database.FindMethodsWithPrefix(type.FullName, "get_", 0, member == null || (member.Modifiers & MemberModifiers.Static) == 0);
				foreach (var name in names)
				{
					if (!vars.Exists(v => v.Name == name.Second))
						vars.Add(new Variable(name.First, name.Second, null));
				}
			}
		}
		
		private CsParameter[] DoGetParameters(CsMember member)
		{
			CsIndexer indexer = member as CsIndexer;
			if (indexer != null)
				return indexer.Parameters;
			
			CsMethod method = member as CsMethod;
			if (method != null)
				return method.Parameters;
			
			CsOperator op = member as CsOperator;
			if (op != null)
				return op.Parameters;
				
			return null;
		}
		
		private ResolvedTarget DoHandleThis(CsMember member, string target)
		{
			ResolvedTarget result = null;
			
			if (target == "this" || target == "<this>")
			{
				if (member != null && (member.Modifiers & MemberModifiers.Static) == 0)
				{
					bool isInstance = (member.Modifiers & MemberModifiers.Static) == 0;
					bool isStatic = target == "<this>";
					
					result = m_resolveType.Resolve(member.DeclaringType, isInstance, isStatic);
					if (result != null)
						Log.WriteLine("AutoComplete", "found this: {0}", result.FullName);
				}
			}
			
			return result;
		}
		
		private ResolvedTarget DoHandleVariable(string text, int offset, string target, Variable[] vars, CsGlobalNamespace globals)
		{
			ResolvedTarget result = null;
			
			for (int i = 0; i < vars.Length && result == null; ++i)
			{
				if (vars[i].Name == target)
				{
					string type = vars[i].Type;
					if (type == "var" && vars[i].Value != null)
					{
						string value = vars[i].Value;
						if (value.StartsWith("new"))
						{
							value = DoGetNewValue(value);
						}
						else if (value.StartsWith("from "))
						{
							value = "System.Collections.Generic.IEnumerable`1";
						}
						else
						{
							Match m = ms_getRE.Match(value);
							if (m.Success)
							{
								value = m.Groups[1].Value;	// TODO: need something more general here
							}
						}
						
						result = Resolve(text, value, offset, globals).First;
						
						if (result != null)
						{
							result = new ResolvedTarget(result.FullName, result.Type, result.Hash, true, false);	// we resolved a type, but it's used as an instance...
							Log.WriteLine("AutoComplete", "found var local: {0}", result.FullName);
						}
					}
					else
					{
						result = m_resolveType.Resolve(type, globals, true, false);
						if (result != null)
							Log.WriteLine("AutoComplete", "found local: {0}", result.FullName);
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
				else if (value[i + count] == '_')
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

		private Regex ms_getRE = new Regex(@"\w+ \. Get \s* < \s* (\w+) \s* > \s* \( \s* \)", RegexOptions.IgnorePatternWhitespace);
		#endregion
	}
}
