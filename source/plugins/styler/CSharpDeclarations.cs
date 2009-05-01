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
using MCocoa;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Styler
{
	internal sealed class CSharpDeclarations : IDeclarations
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Declaration[] Get(IText text, StyleRun[] runs)
		{
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
			
			var editor = text.Boss.Get<ITextEditor>();
			Parse parse = parses.Parse(editor.Path, text.EditCount, text.Text);
			
			var decs = new List<Declaration>();
			CsGlobalNamespace globals = parse.Globals;
			if (globals != null)
			{
				DoGetDeclarations(globals, string.Empty, decs);
				DoGetDirectives(globals.Preprocess, decs);
				
				decs.Sort((lhs, rhs) => lhs.Extent.location.CompareTo(rhs.Extent.location));
			}
			
			return decs.ToArray();
		}
		
		#region Private Methods
		private const string IndentLevel = "    ";
		
		private Declaration DoFindDeclaration(List<Declaration> decs, int offset)
		{
			Declaration d = new Declaration();
			
			foreach (Declaration candidate in decs)
			{
				if (candidate.Extent.Intersects(offset))
					d = candidate;						// note that we want to keep going so we get the innermost declaration that intersects the offset
			}
			
			return d;
		}
		
		[Pure]
		private int DoCountSpaces(string s)
		{
			int count = 0;
			
			for (int i = 0; i < s.Length && s[i] == ' '; ++i)
				++count;
				
			return count;
		}
		
		private void DoGetDirectives(CsPreprocess[] preprocess, List<Declaration> decs)
		{
			foreach (CsPreprocess p in preprocess)
			{
				if (p.Name == "region")
				{
					string name = p.Text;
					
					Declaration d = DoFindDeclaration(decs, p.Offset);
					if (d.Name != null)
						name = IndentLevel + new string(' ', DoCountSpaces(d.Name)) + name;
					
					decs.Add(new Declaration(
						name,
						new NSRange(p.Offset, p.Length),
						false, true));
				}
			}
		}
		
		private void DoGetDeclarations(CsTypeScope scope, string indent, List<Declaration> decs)
		{
			CsNamespace ns = scope as CsNamespace;
			if (ns != null)
			{
				foreach (CsNamespace n in ns.Namespaces)
				{
					DoGetDeclarations(n, indent, decs);
				}
				
				foreach (CsType nested in scope.Types)
				{
					DoGetDeclarations(nested, indent, decs);
				}
			} 
			else
			{
				CsType type = scope as CsType;
				bool isType = (type is CsClass) || (type is CsStruct) || (type is CsInterface)  || type.DeclaringType == null;
				decs.Add(new Declaration(
					indent + DoGetTypePrefix(type) + type.Name,
					new NSRange(type.Offset, type.Length),
					isType, false));
					
				string[] names = (from m in type.Members select DoGetShortName(m)).ToArray();
				for (int i = 0; i < names.Length - 1; ++i)
				{
					bool ambiguous = false;
					for (int j = i + 1; j < names.Length; ++j)
					{
						if (names[i] == names[j])
						{
							names[j] = DoGetFullName(type.Members[j]);
							ambiguous = true;
						}
					}
					
					if (ambiguous)
						names[i] = DoGetFullName(type.Members[i]);
				}
				
				for (int i = 0; i < names.Length; ++i)
				{
					if (!(type.Members[i] is CsField))
						decs.Add(new Declaration(
							indent + IndentLevel + names[i],
							new NSRange(type.Members[i].Offset, type.Members[i].Length),
							false, false));
				}
				
				foreach (CsType nested in scope.Types)
				{
					DoGetDeclarations(nested, indent + IndentLevel, decs);
				}
			}
		}
		
		private string DoGetTypePrefix(CsType type)
		{
			if (type is CsClass)
				return "class ";
			
			else if (type is CsStruct)
				return "struct ";
			
			else if (type is CsInterface)
				return "interface ";
			
			else if (type is CsDelegate)
				return "delegate ";
			
			else if (type is CsEnum)
				return "enum ";
			
			Contract.Assert(false, "bad type: " + type.GetType());
			return "?";
		}
		
		private string DoGetShortName(CsMember member)
		{
			string result;
			
			do
			{
				CsEvent v = member as CsEvent;
				if (v != null)
				{
					result = string.Format("event {0}", v.Name);
					break;
				}
				
				CsField f = member as CsField;
				if (f != null)
				{
					result = f.Name;
					break;
				}
				
				CsIndexer i = member as CsIndexer;
				if (i != null)
				{
					result = "this[...]";
					break;
				}
				
				CsMethod m = member as CsMethod;
				if (m != null)
				{
					result = m.Name;
					break;
				}
				
				CsOperator o = member as CsOperator;
				if (o != null)
				{
					if (o.IsImplicit)
						result = string.Format("implicit operator {0}", o.ReturnType);
					else if (o.IsExplicit)
						result = string.Format("explicit operator {0}", o.ReturnType);
					else
						result = string.Format("operator {0}", o.Name);
					break;
				}
				
				CsProperty p = member as CsProperty;
				if (p != null)
				{
					result = string.Format("{0}", p.Name);
					break;
				}
				
				Contract.Assert(false, "Unexpected member type: " + member.GetType());
				result = "?";
			}
			while (false);
			
			return result;
		}
		
		private string DoGetFullName(CsMember member)
		{
			string result;
			
			do
			{
				CsIndexer i = member as CsIndexer;
				if (i != null)
				{
					result = string.Format("this[{0}]", DoGetParams(i.Parameters));
					break;
				}
				
				CsMethod m = member as CsMethod;
				if (m != null)
				{
					if (m.GenericArguments != null)
						result = string.Format("{0}<{1}>({2})", m.Name, m.GenericArguments, DoGetParams(m.Parameters));
					else
						result = string.Format("{0}({1})", m.Name, DoGetParams(m.Parameters));
					break;
				}
				
				CsOperator o = member as CsOperator;
				if (o != null && !o.IsConversion)
				{
					result = string.Format("operator {0}({1})", o.Name, DoGetParams(o.Parameters));
					break;
				}
				
				result = DoGetShortName(member);
			}
			while (false);
			
			return result;
		}
		
		private string DoGetParams(CsParameter[] parms)
		{
			var builder = new StringBuilder();
			
			for (int i = 0; i < parms.Length; ++i)
			{
				if (parms[i].Modifier != 0)
				{
					builder.Append(parms[i].Modifier.ToString().ToLower());
					builder.Append(" ");
				}
				
				if (parms[i].IsParams)
					builder.Append("params ");
				
				builder.Append(parms[i].Type);
				builder.Append(" ");
				builder.Append(parms[i].Name);
					
				if (i + 1 < parms.Length)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
