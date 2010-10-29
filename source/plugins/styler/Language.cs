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
using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Styler
{
	internal sealed class Settings
	{
		public Settings()
		{
			Globs = new string[0];
			Word = string.Empty;
			Shebangs = string.Empty;
			IgnoreWhitespace = "false";
		}
		
		public string Name {get; set;}
		
		public string[] Globs {get; set;}
		
		public string TabStops {get; set;}
		
		public string Word {get; set;}
		
		public string Shebangs {get; set;}
		
		public string IgnoreWhitespace {get; set;}
	}
	
	internal sealed class Language
	{
		public Language(string path, Settings settings, List<KeyValuePair<string, string>> elements)
		{
			m_path = path;
			m_name = settings.Name;
			
			m_styleWhitespace = settings.IgnoreWhitespace == "false";
			m_shebangs = settings.Shebangs.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
			
			string[] stops = (settings.TabStops ?? string.Empty).Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
			m_tabStops = (from s in stops select int.Parse(s)).ToArray();
			
			m_expr = DoBuildExpr(elements);
			if (settings.Word.Length > 0)
				DoBuildWordRe(settings.Word);
			
			ActiveObjects.Add(this);
		}
		
		public string Path
		{
			get {return m_path;}
		}
		
		public string FriendlyName
		{
			get {return m_name;}
		}
		
		public string[] Shebangs
		{
			get {return m_shebangs;}
		}
		
		public int[] TabStops
		{
			get {return m_tabStops;}
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
		
		// Note that elements may have alternatives so this will, in general, be larger than the
		// number of element names.
		[ThreadModel(ThreadModel.Concurrent)]
		public int ElementCount
		{
			get {return m_indexTable.Count;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public string ElementName(int index)
		{
			return m_indexTable[index];
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public bool StylesWhitespace
		{
			get {return m_styleWhitespace;}
		}
		
		public Regex Word
		{
			get
			{
				if (m_word == null)
					m_word = DoMakeWordRe(@"[\w_] [\w_]*");
				
				return m_word;
			}
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
				m_regex = new Regex(m_expr, ReOptions);
			}
			catch (Exception e)
			{
				DoWriteError("failed to compile the regex for {0}:", m_name);
				DoWriteError(e.Message);
				m_expr = null;
			}
		}
		
		private string DoBuildExpr(List<KeyValuePair<string, string>> elements)
		{
			var exprs = new List<string>();
			
			int index = 1;
			
			if (m_styleWhitespace)
			{
				m_indexTable.Add(index++, "text spaces color changed");
				exprs.Add(@"((?: ^ [\t ]+) | (?: [\t ]+ $))");
			}
			
			foreach (var element in elements)
			{
				DoValidateRegex(element.Key, element.Value);
				
				m_indexTable.Add(index++, element.Key);
				exprs.Add("( " + element.Value + " )");
			}
			
			return string.Join(" | ", exprs.ToArray());
		}
		
		private void DoBuildWordRe(string words)
		{
			var word = new List<string>();
			foreach (string w in words.Split(new char[]{'\t'}, StringSplitOptions.RemoveEmptyEntries))
			{
				DoValidateRegex("Word", w);
				
				word.Add("( " + w + " )");
			}
			
			if (word.Count > 0)
			{
				string re = "(" + string.Join(" | ", word.ToArray()) + ")";
				m_word = DoMakeWordRe(re);
			}
		}
		
		private void DoValidateRegex(string name, string expr)
		{
			for (int i = 0; i < expr.Length; ++i)
			{
				if (expr[i] == '(')
				{
					if ((i > 0 && expr[i - 1] == '\\') || expr[i + 1] == '?')
					{
						continue;
					}
					else
					{
						// TODO: use the transcript
						DoWriteError("{0} in {1} should use a non-capturing group, .e.g '(?: foo )' instead of '(foo)'.", m_path, m_name);
						DoWriteError("   {0}: {1}.", name, expr);
					}
				}
			}
		}
		
		private Regex DoMakeWordRe(string re)
		{
			re = string.Format(@"{0} ([\u0000-\uFFFF]+? {0})*", re);
			return new Regex(re, ReOptions);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoWriteError(string format, params object[] args)
		{
			if (NSApplication.sharedApplication().InvokeRequired)
				NSApplication.sharedApplication().BeginInvoke(() => DoNonThreadedError(string.Format(format, args)));
			else
				DoNonThreadedError(string.Format(format, args));
		}
		
		private void DoNonThreadedError(string text)
		{
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			transcript.Show();
			transcript.WriteLine(Output.Error, text);
		}
		#endregion
		
		#region Fields
		private const RegexOptions ReOptions = RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled;
		
		private string m_expr;
		private string m_path;
		private string m_name;
		private string[] m_shebangs;
		private int[] m_tabStops;
		private Regex m_regex;
		private Regex m_word;
		private bool m_styleWhitespace;
		private Dictionary<int, string> m_indexTable = new Dictionary<int, string>();
		#endregion
	}
}
