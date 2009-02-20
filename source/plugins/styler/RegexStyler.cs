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
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace Styler
{
	internal sealed class RegexStyler : Shared.Styler
	{
		public RegexStyler(XmlNode node)
		{
			m_name = node.Attributes["name"].Value;
			m_expr = DoBuildExpr(node);
			
			ActiveObjects.Add(this);
		}
		
		public override bool StylesWhitespace
		{
			get {return m_styleWhitespace;}
		}
		
		// TODO: This is a bit slow. The parser seems to be significantly faster than the mondo
		// regex so we might get a speedup for c# by reusing the scanner. Note that we'd still
		// have to use the regex for stuff like whitespace and preprocessor runs though.
		protected override void OnComputeRuns(string text, int edit, List<StyleRun> runs)		// threaded
		{
			if (m_regex == null && m_expr != null)
				DoCreateRE();
			
			if (m_regex != null)
			{
				Log.WriteLine(TraceLevel.Verbose, "Styler", "computing runs for edit {0} and {1} characters", edit, text.Length);
				
				DoRegexMatch(text, runs);
				
				if (m_name == "c#")
				{
					if (m_parser == null)
					{
						Boss boss = ObjectModel.Create("CsParser");
						m_parser = boss.Get<ICsParser>();
					}
					
					DoParseMatch(text, runs);
				}
				
				Log.WriteLine(TraceLevel.Verbose, "Styler", "    done computing runs for edit {0}", edit);
			}
		}
		
		#region Private Methods
		private void DoRegexMatch(string text, List<StyleRun> runs)		// threaded
		{
			int last = 0;
			
			MatchCollection matches = m_regex.Matches(text);
			foreach (Match match in matches)
			{
				GroupCollection groups = match.Groups;
				for (int i = 1; i <= m_indexTable.Count; ++i)
				{
					Group g = groups[i];
					if (g.Success)
					{
						if (g.Index > last)
							runs.Add(new StyleRun(last, g.Index - last, StyleType.Default));
						
						if (i == 1 && m_styleWhitespace)
							DoMatchWhitespace(text, g, runs);
						else
							runs.Add(new StyleRun(g.Index, g.Length, m_indexTable[i]));
						
						last = g.Index + g.Length;
						break;
					}
				}
			}
		}

		private void DoParseMatch(string text, List<StyleRun> runs)		// threaded
		{
			int offset, length;
			CsGlobalNamespace globals = m_parser.TryParse(text, out offset, out length);
			if (length > 0)
			{
				// We can't highlight control characters because they have zero width so 
				// we'll grow to the left until we find a non-control character.
				while (offset > 0 && char.IsControl(text, offset) && text[offset] != '\t')
				{
					--offset;
					++length;
				}
			
				runs.Add(new StyleRun(offset, length, StyleType.Error));
			}
			
			DoMatchScope(globals, runs);
		}
		
		private void DoMatchScope(CsTypeScope scope, List<StyleRun> runs)		// threaded
		{
			foreach (CsType type in scope.Types)
			{
				runs.Add(new StyleRun(type.NameOffset, type.Name.Length, StyleType.Type));
				
				foreach (CsMember member in type.Members)
				{
					if (!(member is CsField))
						if (member.Name != "<this>")
							runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Member));
						else
							runs.Add(new StyleRun(member.NameOffset, member.Name.Length - 2, StyleType.Member));
				}
				
				DoMatchScope(type, runs);
			}
			
			CsNamespace ns = scope as CsNamespace;
			if (ns != null)
			{
				// These are considered members not types, so we need to special case
				// them if they are within a namespace.
				foreach (CsMember member in ns.Delegates)
				{
					runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Type));
				}
				
				foreach (CsMember member in ns.Enums)
				{
					runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Type));
				}
				
				foreach (CsNamespace n in ns.Namespaces)
				{
					DoMatchScope(n, runs);
				}
			}
		}
		
		private void DoMatchWhitespace(string text, Group g, List<StyleRun> runs)		// threaded
		{
			int i = g.Index;
			while (i < g.Index + g.Length)
			{
				int count = DoFindContiguousCount(text, i);
				if (text[i] == ' ')
					if (m_styleWhitespace && ShowSpaces)
						runs.Add(new StyleRun(i, count, StyleType.Spaces));
					else
						runs.Add(new StyleRun(i, count, StyleType.Default));
				else
					if (m_styleWhitespace && ShowTabs)
						runs.Add(new StyleRun(i, count, StyleType.Tabs));
					else
						runs.Add(new StyleRun(i, count, StyleType.Default));
				
				i += count;
			}
		}
		
		private int DoFindContiguousCount(string text, int i)	// threaded
		{
			int count = 0;
			
			char ch = text[i];
			while (i + count < text.Length && text[i + count] == ch)
				++count;
			
			return count;
		}
		
		// Compiling regexen is expensive so we won't do it unless we need to.
		private void DoCreateRE()
		{
			try
			{
				m_regex = new Regex(m_expr, RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("failed to compile the regex for {0}:", m_name);
				Console.Error.WriteLine(e.Message);
				m_expr = null;
			}
		}
		
		private string DoBuildExpr(XmlNode node)
		{
			var exprs = new List<string>();
			
			int index = 1;
			
			m_styleWhitespace = node.Attributes["ignore_whitespace"].Value == "false" || node.Attributes["ignore_whitespace"].Value == "0";
			if (m_styleWhitespace)
			{
				m_indexTable.Add(index++, StyleType.Spaces);
				exprs.Add(@"((?: ^ [\t ]+) | (?: [\t ]+ $))");
			}
			
			foreach (XmlNode child in node.ChildNodes)	
			{
				for (int i = 0; i < child.InnerText.Length; ++i)
				{
					if (child.InnerText[i] == '(')
					{
						if ((i > 0 && child.InnerText[i - 1] == '\\') || child.InnerText[i + 1] == '?')
						{
							continue;
						}
						else
						{
							Console.Error.WriteLine("{0} should use a non-capturing group, .e.g '(?: foo )' instead of '(foo)'.", m_name);
							Console.Error.WriteLine("   {0}: {1}.", child.Name, child.InnerText);
						}
					}
				}
				
				m_indexTable.Add(index++, DoGetToken(child.Name));
				exprs.Add("( " + child.InnerText + " )");
			}
			
			return string.Join(" | ", exprs.ToArray());
		}
		
		private StyleType DoGetToken(string name) 
		{
			switch (name)
			{
				case "comment":
					return StyleType.Comment;
				
				case "keyword":
					return StyleType.Keyword;
				
				case "number":
					return StyleType.Number;
				
				case "other1":
					return StyleType.Other1;
				
				case "other2":
					return StyleType.Other2;
				
				case "preprocessor":
					return StyleType.Preprocessor;
				
				case "string":
					return StyleType.String;
				
				case "member":
					return StyleType.Member;
				
				case "type":
					return StyleType.Type;
				
				default:
					Trace.Fail("Bad name: " + name);
					break;
			}
			
			return 0;
		}
		#endregion
		
		#region Fields
		private string m_expr;
		private string m_name;
		private Regex m_regex;
		private ICsParser m_parser;
		private bool m_styleWhitespace;
		private Dictionary<int, StyleType> m_indexTable = new Dictionary<int, StyleType>();
		#endregion
	}
}
