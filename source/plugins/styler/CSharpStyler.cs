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
using System.Threading;

namespace Styler
{
	internal sealed class CSharpStyler : IStyler, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Thread thread = new Thread(this.DoComputeStyles);
			thread.Name = "CSharpStyler.DoComputeStyles";
			thread.IsBackground = true;		// allow the app to quit even if the thread is still running
			thread.Start();
			
			Broadcaster.Register("parsed file", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public void PostProcess(StyleRuns runs)
		{
			lock (m_mutex)
			{
				Styles styles;
				if (!m_styles.TryGetValue(runs.Path, out styles))
				{
					styles = new Styles();
					m_styles.Add(runs.Path, styles);
				}
				styles.RegexStyles = runs;
				
				DoTryBroadcast(runs.Path);
			}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "parsed file":
					DoQueueJob((Parse) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoQueueJob(Parse parse)
		{
			lock (m_mutex)
			{
				Log.WriteLine(TraceLevel.Verbose, "Styler", "queuing parse {0} for edit {1}", System.IO.Path.GetFileName(parse.Path), parse.Edit);
				m_parses.Add(parse);
				Monitor.Pulse(m_mutex);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoComputeStyles()
		{
			while (true)
			{
				Parse parse = null;
				
				lock (m_mutex)
				{
					while (m_parses.Count == 0)
					{
						Unused.Value = Monitor.Wait(m_mutex);
					}
					
					parse = m_parses.Last();			// prefer the last parse added since that is presumbaby what the user is editing
					m_parses.RemoveAt(m_parses.Count - 1);
				}
				
				DoComputeRuns(parse);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoComputeRuns(Parse parse)
		{
			var runs = new List<StyleRun>();
			DoParseMatch(parse, runs);
			Log.WriteLine(TraceLevel.Verbose, "Styler", "computed runs for parse {0} edit {1}", System.IO.Path.GetFileName(parse.Path), parse.Edit);
			
			lock (m_mutex)
			{
				Styles styles;
				if (!m_styles.TryGetValue(parse.Path, out styles))
				{
					styles = new Styles();
					m_styles.Add(parse.Path, styles);
				}
				styles.ParsedStyles = new StyleRuns(m_boss, parse.Path, parse.Edit, runs.ToArray());
				
				DoTryBroadcast(parse.Path);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoTryBroadcast(string path)
		{
			Styles styles = m_styles[path];
			
			if (styles.RegexStyles != null && styles.ParsedStyles != null)
			{
				if (styles.RegexStyles.Edit == styles.ParsedStyles.Edit)
				{
					var runs = new List<StyleRun>(styles.RegexStyles.Runs.Length + styles.ParsedStyles.Runs.Length);
					runs.AddRange(styles.RegexStyles.Runs);
					runs.AddRange(styles.ParsedStyles.Runs);
					runs.Sort((lhs, rhs) => lhs.Offset.CompareTo(rhs.Offset));
					
					var data = new StyleRuns(styles.RegexStyles.Boss, styles.RegexStyles.Path,
						styles.RegexStyles.Edit, runs.ToArray());
					
					NSApplication.sharedApplication().BeginInvoke(
						() => Broadcaster.Invoke("computed style runs", data));
						
					m_styles.Remove(path);
				}
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoParseMatch(Parse parse, List<StyleRun> runs)
		{
			if (parse.ErrorLength > 0)
				runs.Add(new StyleRun(parse.ErrorIndex, parse.ErrorLength, StyleType.Error));
			
			if (parse.Globals != null)
				DoMatchScope(parse.Globals, runs);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoMatchScope(CsTypeScope scope, List<StyleRun> runs)
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
				foreach (CsNamespace n in ns.Namespaces)
				{
					DoMatchScope(n, runs);
				}
			}
		}
		#endregion
		
		#region Private Type
		[ThreadModel(ThreadModel.Concurrent)]
		private sealed class Styles
		{
			public StyleRuns RegexStyles {get; set;}
			public StyleRuns ParsedStyles {get; set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		
		private object m_mutex = new object();
			private List<Parse> m_parses = new List<Parse>();
			private Dictionary<string, Styles> m_styles = new Dictionary<string, Styles>();
		#endregion
	}
}
