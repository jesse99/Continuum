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
using Gear.Helpers;
using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Styler
{
	// Note that this is on a singleton boss.
	internal sealed class ComputeRegexStyles : IInterface, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Boss b = ObjectModel.Create("Stylers");
			m_white = b.Get<IWhitespace>();
			
			Thread thread = new Thread(this.DoComputeStyles);
			thread.Name = "ComputeRegexStyles.DoComputeStyles";
			thread.IsBackground = true;		// allow the app to quit even if the thread is still running
			thread.Start();
			
			Broadcaster.Register("text changed", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
				
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "text changed":
					DoQueueJob((TextEdit) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoQueueJob(TextEdit edit)
		{
			var editor = edit.Boss.Get<ITextEditor>();
			string key = editor.Key;
			Contract.Assert(!string.IsNullOrEmpty(key), "key is null or empty");
			
			if (edit.Language != null)
			{
				var text = edit.Boss.Get<IText>();
				lock (m_mutex)
				{
					Log.WriteLine(TraceLevel.Verbose, "Styler", "queuing {0} for edit {1}", System.IO.Path.GetFileName(key), text.EditCount);
					m_jobs[key] = new Job(text.EditCount, text.Text, edit.Language);
					Monitor.Pulse(m_mutex);
				}
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoComputeStyles()
		{
			while (true)
			{
				string key = null;
				Job job = null;
				
				lock (m_mutex)
				{
					while (m_jobs.Count == 0)
					{
						Unused.Value = Monitor.Wait(m_mutex);
					}
					
					key = m_jobs.Keys.First();
					job = m_jobs[key];
					m_jobs.Remove(key);
				}
				
				DoComputeRuns(key, job.Edit, job.Text, job.Language);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoComputeRuns(string key, int edit, string text, ILanguage language)
		{
			var runs = new List<StyleRun>();
			var sw = language.SafeBoss().Get<IStyleWith>();
			DoRegexMatch(text, sw.Language, runs);
			Log.WriteLine(TraceLevel.Verbose, "Styler", "computed {0} runs for {1} edit {2}", runs.Count, System.IO.Path.GetFileName(key), edit);
			
			var data = new StyleRuns(language.SafeBoss(), key, edit, runs.ToArray());
			
			if (language.SafeBoss().Has<IStyler>())
			{
				var post = language.SafeBoss().Get<IStyler>();
				post.PostProcess(data);
			}
			else
			{
				NSApplication.sharedApplication().BeginInvoke(
					() => Broadcaster.Invoke("computed style runs", data));
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoRegexMatch(string text, Language language, List<StyleRun> runs)
		{
			if (language.Regex != null)
			{
				MatchCollection matches = language.Regex.Matches(text);
				foreach (Match match in matches)
				{
					GroupCollection groups = match.Groups;
					for (int i = 1; i <= language.ElementCount; ++i)
					{
						Group g = groups[i];
						if (g.Success)
						{
							string type = language.ElementName(i);
							if (i == 1 && language.StylesWhitespace)
								DoMatchWhitespace(text, g, language, runs);
							else
								if (g.Length > 0)
									runs.Add(new StyleRun(g.Index, g.Length, type));
								else if (type != "ParseError")
									Log.WriteLine(TraceLevel.Error, "Styler", "{0} matched zero characters", type);
							break;
						}
					}
				}
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoMatchWhitespace(string text, Group g, Language language, List<StyleRun> runs)
		{
			int i = g.Index;
			while (i < g.Index + g.Length)
			{
				int count = DoFindContiguousCount(text, i);
				if (text[i] == ' ')
				{
					if (language.StylesWhitespace && m_white.ShowSpaces)
						runs.Add(new StyleRun(i, count, "text spaces color changed"));
//					else
//						runs.Add(new StyleRun(i, count, StyleType.Default));
				}
				else
				{
					if (language.StylesWhitespace && m_white.ShowTabs)
						runs.Add(new StyleRun(i, count, "text tabs color changed"));
//					else
//						runs.Add(new StyleRun(i, count, StyleType.Default));
				}
				
				i += count;
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private int DoFindContiguousCount(string text, int i)
		{
			int count = 0;
			
			char ch = text[i];
			while (i + count < text.Length && text[i + count] == ch)
				++count;
			
			return count;
		}
		#endregion
		
		#region Private Types
		[ThreadModel(ThreadModel.Concurrent)]
		private sealed class Job
		{
			public Job(int edit, string text, ILanguage language)
			{
				Edit = edit;
				Text = text;
				Language = language;
			}
			
			public int Edit {get; private set;}
			
			public string Text {get; private set;}
			
			public ILanguage Language {get; private set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private IWhitespace m_white;
		private object m_mutex = new object();
			private Dictionary<string, Job> m_jobs = new Dictionary<string, Job>();
		#endregion
	}
}
