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
//using System.Threading;

namespace Styler
{
	internal class RegexComputer : IComputeRuns, IStyleWith
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public virtual void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Boss b = ObjectModel.Create("Stylers");
			m_white = b.Get<IWhitespace>();
		}
		
		public virtual CsGlobalNamespace ComputeRuns(string text, int edit, List<StyleRun> runs)	// threaded
		{
			CsGlobalNamespace globals = null;
			
			if (m_regex != null)
			{
				Log.WriteLine(TraceLevel.Verbose, "Styler", "computing runs for edit {0} and {1} characters", edit, text.Length);
				
				globals = OnComputeRuns(text, edit, runs);
				
				Log.WriteLine(TraceLevel.Verbose, "Styler", "    done computing runs for edit {0}", edit);
			}
			
			return globals;
		}
		
		public bool StylesWhitespace
		{
			get {return m_language.StylesWhitespace;}
		}
		
		public Language Language
		{
			get {return m_language;}
			set {m_language = value; m_regex = value.Regex;}
		}
		
		#region Protected Methods
		// TODO: This is a bit slow. The parser seems to be significantly faster than the mondo
		// regex so we might get a speedup for c# by reusing the scanner. Note that we'd still
		// have to use the regex for stuff like whitespace and preprocessor runs though.
		protected virtual CsGlobalNamespace OnComputeRuns(string text, int edit, List<StyleRun> runs)	// threaded
		{
			DoRegexMatch(text, runs);
			
			return null;
		}
		#endregion
		
		#region Private Methods
		private void DoRegexMatch(string text, List<StyleRun> runs)		// threaded
		{
			int last = 0;
			
			MatchCollection matches = m_regex.Matches(text);
			foreach (Match match in matches)
			{
				GroupCollection groups = match.Groups;
				for (int i = 1; i <= m_language.StyleCount; ++i)
				{
					Group g = groups[i];
					if (g.Success)
					{
						if (g.Index > last)
							runs.Add(new StyleRun(last, g.Index - last, StyleType.Default));
						
						if (i == 1 && StylesWhitespace)
							DoMatchWhitespace(text, g, runs);
						else
							runs.Add(new StyleRun(g.Index, g.Length, m_language.Style(i)));
						
						last = g.Index + g.Length;
						break;
					}
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
					if (StylesWhitespace && m_white.ShowSpaces)
						runs.Add(new StyleRun(i, count, StyleType.Spaces));
					else
						runs.Add(new StyleRun(i, count, StyleType.Default));
				else
					if (StylesWhitespace && m_white.ShowTabs)
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
		#endregion
		
		#region Fields
		private Boss m_boss;
		private Language m_language;
		private Regex m_regex;
		private IWhitespace m_white;
		#endregion
	}
}
