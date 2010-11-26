// Copyright (C) 2009-2010 Jesse Jones
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
using Gear.Helpers;
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
		static ResolveName()
		{
			// These aren't perfect regexen but they are only used to match the rhs
			// of a 'var x = rhs' expression and a bad match just means that we will
			// try to auto-complete using a bad type.
			string[] expressions = new string[]{
				@"new \s+ TYPE \s* \(",										// new TYPE(
				@"as \s+ TYPE \s* $",											// as TYPE;
				@"\( \s* TYPE \s* \) \s* .+? $",								// (TYPE) xxx;
				@"[\w._]+? \s* \. \s* Get \s* < \s* TYPE \s* > \s* \(",	// xxx.Get<Type>(
			};
			
			string type = @"(\w+[\w._, <>\[\]?*]*)";
			
			string pattern = string.Empty;
			for (int i = 0; i < expressions.Length; ++i)
			{
				pattern += string.Format("(?: {0})", expressions[i]).Replace("TYPE", type);
				
				if (i + 1 < expressions.Length)
					pattern += " | ";
			}
			
			ms_re = new Regex(pattern, RegexOptions.IgnorePatternWhitespace);
		}
		
		public ResolveName(CsMember context, ITargetDatabase database, ICsLocalsParser locals, string text, int offset, CsGlobalNamespace globals)
		{
			Profile.Start("ResolveName::ctor");
			m_database = database;
			m_typeResolver = new ResolveType(database);
			m_globals = globals;
			m_offset = offset;
			m_context = context;
			
			Profile.Start("DoFindMember");
			m_member = DoFindMember(m_globals);
			Profile.Stop("DoFindMember");
			
			Profile.Start("DoGetVariables");
			m_variables = DoGetVariables(text, locals);
			Profile.Stop("DoGetVariables");
			
			Profile.Stop("ResolveName::ctor");
		}
		
		// Returns all of the names which may be used at the specified offset in the code.
		public Variable[] Variables
		{
			get {return m_variables;}
		}
		
		// May return null.
		public ResolvedTarget Resolve(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			ResolvedTarget result = null;
			Profile.Start("ResolveName::Resolve");
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---------------- resolving name");
			Log.WriteLine(TraceLevel.Verbose,"AutoComplete", "name: '{0}'", name);
			
			// this.
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying this keyword");
				result = DoHandleThis(name);
			}
			
			// base.
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying base keyword");
				result = DoHandleBase(name);
			}
			
			// name. (where name is a local, argument, etc)
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying variable name");
				result = DoHandleVariable(name);
			}
			
			// SomeType.
			if (result == null)
			{
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying type name");
				result = m_typeResolver.Resolve(name, m_context, m_globals, false, true);
			}
			
			// char literal (we don't complete integers because it's too annoying for the common case
			// of the dot being a floating point indicator instead of a method call)
			if (result == null)
			{
				if (name.Length >= 2 && name[0] == '\'' && name.Last() == '\'')
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying char literal name");
					result = m_typeResolver.Resolve("System.Char", m_context, m_globals, true, false);
				}
			}
				
			// string literal
			if (result == null)
			{
				if (name.Length >= 2 && name[0] == '"' && name.Last() == '"')
				{
					Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "trying string literal name");
					result = m_typeResolver.Resolve("System.String", m_context, m_globals, true, false);
				}
			}
			
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "---- name {0} -> {1}", name, result);
			
			Profile.Stop("ResolveName::Resolve");
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
						vars.Add(new Variable(m_member.DeclaringType.FullName, "this", null, "Locals"));
				
				// value
				CsProperty prop = m_member as CsProperty;
				if (prop != null && prop.SetterBody != null)
				{
					if (prop.SetterBody.First < m_offset && m_offset <= prop.SetterBody.Last)
						vars.Add(new Variable(prop.ReturnType, "value", null, "Locals"));
				}
				
				CsIndexer indexer = m_member as CsIndexer;
				if (indexer != null && indexer.SetterBody != null)
				{
					if (indexer.SetterBody.First < m_offset && m_offset <= indexer.SetterBody.Last)
						vars.Add(new Variable(indexer.ReturnType, "value", null, "Locals"));
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
						vars.Add(new Variable(candidates[i].Type, candidates[i].Name, candidates[i].Value, "Locals"));
				}
			}
			
			// args
			CsParameter[] parms = DoGetParameters();
			if (parms != null)
			{
				foreach (CsParameter p in parms)
				{
					if (!vars.Exists(v => v.Name == p.Name))
						vars.Add(new Variable(p.Type, p.Name, null, "Arguments"));
				}
			}
			
			// Fields and properties (note that we have to include these here so that the common
			// case of a property named after a type resolves to the property instead of the type)
			if (m_member != null && m_member.DeclaringType != null)
			{
				ResolvedTarget target = m_typeResolver.Resolve(m_member.DeclaringType.FullName, m_context, m_globals, true, true);
				if (target != null)
				{
					var resolveMembers = new ResolveMembers(m_database);
					Item[] items = resolveMembers.Resolve(m_context, target, m_globals);
					foreach (Item item in items)
					{
						if (!(item is MethodItem))
							if (!vars.Exists(v => v.Name == item.Text))
								vars.Add(new Variable(item.Type, item.Text, null, item.Type));
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
			
			if (m_member != null)
			{
				if (name == "this")
				{
					if ((m_member.Modifiers & MemberModifiers.Static) == 0)
					{
						result = m_typeResolver.Resolve(m_member.DeclaringType, true, false);
					}
				}
				else if (name == "<this>")
				{
					bool isInstance = (m_member.Modifiers & MemberModifiers.Static) == 0;
					bool isStatic = true;
					
					result = m_typeResolver.Resolve(m_member.DeclaringType, isInstance, isStatic);
				}
			}
			
			return result;
		}
		
		private ResolvedTarget DoHandleBase(string name)
		{
			ResolvedTarget result = null;
			
			if (name == "base")
			{
				if (m_member != null && (m_member.Modifiers & MemberModifiers.Static) == 0)
				{
					string[] bases = m_member.DeclaringType.Bases.Names;
					string b = (bases.Length > 0 && !CsHelpers.IsInterface(bases[0])) ? bases[0] : "System.Object";
					result = m_typeResolver.Resolve(b, m_context, m_globals, true, false);
					
					if (result != null)
						result.BaseKeyword = true;
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
						if (value.StartsWith("from "))		// these two don't have an explicit TYPE so we don't use ms_re for them
						{
							// var result = from ...;
							value = "System.Collections.Generic.IEnumerable`1";
						}
						else if (value.StartsWith("typeof"))
						{
							// var result = typeof(...);
							value = DoGetTypeofValue(value);
						}
						else
						{
							// var result = xxx TYPE yyy
							value = DoGetReValue(value);
						}
						
						if (result == null && !string.IsNullOrEmpty(value))
							result = Resolve(value);
						
						if (result != null)
						{
							result = new ResolvedTarget(result.TypeName, result.Type, true, false);	// we resolved a type, but it's used as an instance...
						}
					}
					else
					{
						result = m_typeResolver.Resolve(type, m_context, m_globals, true, false);
					}
				}
			}
			
			return result;
		}
		
		private string DoGetReValue(string value)
		{
			string type = value;
			
			Match m = ms_re.Match(value);
			if (m.Success)
			{
				for (int i = 1; i < m.Groups.Count; ++i)
				{
					Group g = m.Groups[i];
					if (g.Success)
					{
						type = g.Value.Replace(" ", string.Empty);		// the value string has extra spaces inserted...
						break;
					}
				}
			}
			
			if (type != null)
			{
				if (type.EndsWith("[ ]"))
					type = "array-type";
				
				else if (type.EndsWith("?"))
					type = "nullable-type";
				
				else if (type.EndsWith("*"))
					type = "pointer-type";
			}
			
			return type;
		}
		
		private string DoGetTypeofValue(string value)
		{
			string type = null;
			
			Match m = ms_typeofRE.Match(value);
			if (m.Success)
			{
				type = "System.Type";
			}
			
			return type;
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
			
			var types = new List<CsType>();
			types.AddRange(ns.Types);
			
			int j = 0;
			while (j < types.Count && member == null)
			{
				CsType type = types[j++];
					
				for (int k = 0; k < type.Members.Length && member == null; ++k)
				{
					CsMember candidate = type.Members[k];
					if (candidate.Offset <= m_offset && m_offset < candidate.Offset + candidate.Length)
					{
						member = candidate;
					}
				}
				
				types.AddRange(type.Types);
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
		private CsMember m_context;
		
		private static Regex ms_re;
		private Regex ms_typeofRE = new Regex(@"typeof \s* \( \s* [\w._]+ \s* \) \s* $", RegexOptions.IgnorePatternWhitespace);
		#endregion
	}
}
