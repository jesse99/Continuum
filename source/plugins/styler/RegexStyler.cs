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
using System.Threading;

namespace Styler
{
	internal class RegexStyler : IStyler, IStyleWith
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public virtual void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Boss b = ObjectModel.Create("Stylers");
			m_stylers = b.Get<IStylers>();
			
			m_timer = new Timer((o) => DoTimer(), null, Timeout.Infinite, Timeout.Infinite);
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
		
		public void Apply(Boss boss, Action callback)
		{
			Trace.Assert(boss != null, "boss is null");
			Trace.Assert(callback != null, "callback is null");
			
			var text = boss.Get<IText>();
			
			if (text.Text.Length < 128*1024)
			{
				lock (m_mutex)
				{
					m_text = text.Text;
					m_edit = text.EditCount;
					m_textBoss = boss;
					m_callback = callback;
					
					Unused.Value = m_timer.Change(0, Timeout.Infinite);
				}
			}
			else
			{
				// We need to ensure that the callback is always called because
				// TextController uses the call as a signal that it is OK to restore the
				// scroller.
				var styles = boss.Get<IStyles>();
				styles.Reset(text.EditCount, new StyleRun[0], null);
				
				NSApplication.sharedApplication().BeginInvoke(callback);
			}
		}
		
		public void Queue(Boss boss, Action callback)
		{
			Trace.Assert(boss != null, "boss is null");
			Trace.Assert(callback != null, "callback is null");
			
			lock (m_mutex)
			{
				m_text = null;
				m_edit = 0;
				m_textBoss = boss;
				m_callback = callback;
				
				Unused.Value = m_timer.Change(750, Timeout.Infinite);
			}
		}
		
		#region Protected Methods
		// TODO: This is a bit slow. The parser seems to be significantly faster than the mondo
		// regex so we might get a speedup for c# by reusing the scanner. Note that we'd still
		// have to use the regex for stuff like whitespace and preprocessor runs though.
		protected virtual CsGlobalNamespace OnComputeRuns(string text, int edit, List<StyleRun> runs)		// threaded
		{
			DoRegexMatch(text, runs);
			
			return null;
		}
		#endregion
		
		#region Private Methods
		private void DoQueuedApply()
		{
			Boss boss = null;
			Action callback = null;
			
			lock (m_mutex)
			{
				if (m_text == null)				// Apply may have been called before we landed here
				{
					boss = m_textBoss;
					callback = m_callback;
				}
			}
			
			if (boss != null)
				Apply(boss, callback);
		}
		
		private void DoTimer()			// threaded		TODO: might be better to use a low priority thread (tho mono 2.2 doesn't support them)
		{
			string text = null;
			int edit = 0;
			Boss boss = null;
			Action callback = null;
			
			lock (m_mutex)	
			{
				text = m_text;
				edit = m_edit;
				boss = m_textBoss;
				callback = m_callback;
			}
			
			if (text != null)
			{
				var runs = new List<StyleRun>(text.Length/50);
				CsGlobalNamespace globals = DoComputeRuns(text, edit, runs);
				
				var styles = boss.Get<IStyles>();
				styles.Reset(edit, runs.ToArray(), globals);

				NSApplication.sharedApplication().BeginInvoke(callback);
			}
			else
				NSApplication.sharedApplication().BeginInvoke(DoQueuedApply);
		}
				
		private CsGlobalNamespace DoComputeRuns(string text, int edit, List<StyleRun> runs)		// threaded
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
					if (StylesWhitespace && m_stylers.ShowSpaces)
						runs.Add(new StyleRun(i, count, StyleType.Spaces));
					else
						runs.Add(new StyleRun(i, count, StyleType.Default));
				else
					if (StylesWhitespace && m_stylers.ShowTabs)
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
		private Timer m_timer;
		private Language m_language;
		private Regex m_regex;
		private IStylers m_stylers;
		
		private object m_mutex = new object();
			private string m_text;
			private int m_edit;
			private Boss m_textBoss;
			private Action m_callback;
		#endregion
	}
}
