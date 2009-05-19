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

using Gear.Helpers;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace Styler
{
	internal sealed class Language
	{
		public Language(XmlNode node)
		{
			m_name = node.Attributes["name"].Value;
			m_initialTab = int.Parse(node.Attributes["initial_tab"].Value);
			m_expr = DoBuildExpr(node);
			
			ActiveObjects.Add(this);
		}
		
		public string Name
		{
			get {return m_name;}
		}
		
		public int InitialTab
		{
			get {return m_initialTab;}
		}
		
		// May return null.
		[ThreadModel(ThreadModel.Concurrent)]
		public Regex Regex
		{
			get
			{
				if (m_regex == null && m_expr != null)
					DoCreateRE();
					
				return m_regex;
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public int StyleCount
		{
			get {return m_indexTable.Count;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public StyleType Style(int index)
		{
			return m_indexTable[index];
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public bool StylesWhitespace
		{
			get {return m_styleWhitespace;}
		}
		
		#region Private Methods		
		// Compiling regexen is expensive so we won't do it unless we need to. Also
		// this method may execute concurrently but that should not cause any actual
		// harm.
		[ThreadModel(ThreadModel.Concurrent)]
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
					Contract.Assert(false, "Bad name: " + name);
					break;
			}
			
			return 0;
		}
		#endregion
		
		#region Fields
		private string m_expr;
		private string m_name;
		private int m_initialTab;
		private Regex m_regex;
		private bool m_styleWhitespace;
		private Dictionary<int, StyleType> m_indexTable = new Dictionary<int, StyleType>();
		#endregion
	}
}
