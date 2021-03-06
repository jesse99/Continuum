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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CsParser
{
	internal sealed class Parses : IParses, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Broadcaster.Register("built target", this);
			Broadcaster.Register("text changed", this);
			
			Thread thread = new Thread(this.DoThread);
			thread.Name = "Parses.DoThread";
			thread.IsBackground = true;		// allow the app to quit even if the thread is still running
			thread.Start();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "built target":
					DoClearClosed();
					break;
					
				case "text changed":
					DoQueueJob((TextEdit) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		private void DoQueueJob(TextEdit edit)
		{
			var editor = edit.Boss.Get<ITextEditor>();
			string key = editor.Key;
			Contract.Assert(!string.IsNullOrEmpty(key), "key is null or empty");
			
			if (edit.Language != null && "CsLanguage" == edit.Language.Name)
			{
				var text = edit.Boss.Get<IText>();
				lock (m_mutex)
				{
					Log.WriteLine(TraceLevel.Verbose, "Parser", "queuing {0} for edit {1}", System.IO.Path.GetFileName(key), text.EditCount);
					m_jobs[key] = new Job(text.EditCount, text.Text);
					Monitor.Pulse(m_mutex);
				}
			}
		}
		
		public Parse TryParse(string key)
		{
			Contract.Requires(!string.IsNullOrEmpty(key), "key is null or empty");
			Profile.Start("Parses::TryParse");
			
			Parse parse;
			lock (m_mutex)
			{
				Unused.Value = m_parses.TryGetValue(key, out parse);
			}
			
			Profile.Stop("Parses::TryParse");
			return parse;
		}
		
		public Parse Parse(string key, int edit, string text)
		{
			Contract.Requires(!string.IsNullOrEmpty(key), "key is null or empty");
			Contract.Requires(text != null, "text is null");
			
			Parse parse = null;
			
			lock (m_mutex)
			{
				while (!m_parses.ContainsKey(key) || m_parses[key].Edit != edit)
				{
					m_jobs[key] = new Job(edit, text);
					Monitor.Pulse(m_mutex);
					
					bool pulsed = Monitor.Wait(m_mutex, TimeSpan.FromSeconds(10));
					if (!pulsed)
						throw new Exception("Timed out trying to parse " + key);
				}
				
				parse = m_parses[key];
			}
			
			return parse;
		}
		
#if TEST
		public void AddParse(string key, CsGlobalNamespace globals)
		{
			var parse = new Parse(0, "test.cs", 0, 0, globals, new Token[0], new Token[0]);
			lock (m_mutex)
			{
				m_parses[key] = parse;
			}
		}
#endif
		
		public CsType FindType(string fullName)
		{
			Contract.Requires(fullName != null, "fullName is null");
			
			CsType result = null;
			if (fullName.Length > 0)
			{
				var types = new List<CsType>();
				lock (m_mutex)
				{
					foreach (Parse parse in m_parses.Values)
					{
						if (parse.Globals != null)
							DoFindTypes(parse.Globals, types);
					}
				}
				
				string name = fullName;
				int gcount = 0;
				int j = fullName.LastIndexOf('`');
				if (j > 0)
				{
					name = fullName.Substring(0, j);
					string gstr = fullName.Substring(j + 1);
					if (!gstr.All(c => char.IsDigit(c)))
						return null;			// TODO: can happen for nested generics
					gcount = int.Parse(gstr);
				}
				
				for (int i = 0; i < types.Count && result == null; ++i)
				{
					if (name == types[i].FullName)
					{
						if (gcount == 0 || (types[i].GenericArguments != null &&
							types[i].GenericArguments.Count(',') + 1 == gcount))
						{
							result = types[i];
						}
					}
				}
			}
			
			return result;
		}
		
		public CsType[] FindTypes(string ns, string stem)
		{
			Contract.Requires(stem != null, "stem is null");
			
			var types = new List<CsType>();
			lock (m_mutex)
			{
				foreach (Parse parse in m_parses.Values)
				{
					if (parse.Globals != null)
						DoFindTypes(parse.Globals, types);
				}
			}
			
			var result = new List<CsType>();
			for (int i = 0; i < types.Count; ++i)
			{
				if (stem.Length == 0 || types[i].Name.StartsWith(stem))
				{
					if (ns == null && (types[i].Namespace == null || types[i].Namespace.Name == "<globals>"))
						result.Add(types[i]);
					
					else if (types[i].Namespace != null && ns == types[i].Namespace.Name)
						result.Add(types[i]);
				}
			}
			
			return result.ToArray();
		}
		
		public string[] FindNamespaces(string ns)
		{
			var names = new List<string>();
			
			if (ns.Length > 0)
				ns += '.';
			
			lock (m_mutex)
			{
				foreach (Parse parse in m_parses.Values)
				{
					if (parse.Globals != null)
						DoFindNamespaces(ns, null, parse.Globals, names);
				}
			}
			
			return names.ToArray();
		}
		
		#region Private Methods
		private void DoFindNamespaces(string prefix, string parent, CsNamespace ns, List<string> names)
		{
			string name = null;
			if (ns.Name != "<globals>")
				name = parent != null ? (parent + "." + ns.Name) : ns.Name;
			
			if (name != null)
				if (prefix.Length > 0 && name.StartsWith(prefix))
					names.AddIfMissing(name.Substring(prefix.Length));	
				else if (prefix.Length == 0)
					names.AddIfMissing(name);	
			
			foreach (CsNamespace child in ns.Namespaces)
			{
				DoFindNamespaces(prefix, name, child, names);
			}
		}
		
		private void DoFindTypes(CsTypeScope scope, List<CsType> types)
		{
			CsType candidate = scope as CsType;
			if (candidate != null)
				types.Add(candidate);
			
			for (int i = 0; i < scope.Types.Length; ++i)
				DoFindTypes(scope.Types[i], types);
				
			CsNamespace ns = scope as CsNamespace;
			if (ns != null)
			{
				for (int i = 0; i < ns.Namespaces.Length; ++i)
					DoFindTypes(ns.Namespaces[i], types);
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoThread()
		{
			Parser parser = new Parser();
			
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
				
				Parse parse = DoParse(key, parser, job);
				lock (m_mutex)
				{
					m_parses[key] = parse;
					DoCheckHighwater();
					
					Log.WriteLine(TraceLevel.Verbose, "Parser", "computed parse for {0} and edit {1}", System.IO.Path.GetFileName(key), parse.Edit);
					NSApplication.sharedApplication().BeginInvoke(
						() => Broadcaster.Invoke("parsed file", parse));
					
					Monitor.Pulse(m_mutex);
				}
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private Parse DoParse(string key, Parser parser, Job job)
		{
			int index, length;
			CsGlobalNamespace globals;
			Token[] tokens, comments;
			parser.TryParse(job.Text, out index, out length, out globals, out tokens, out comments);
			
			return new Parse(job.Edit, key, index, length, globals, comments, tokens);
		}
		
		// We want to hang onto parses until the assembly they are within is
		// built so that auto-complete recognizes any edits that the user may
		// have made. But eventually we need to clear parses for windows which
		// are no longer open. For now we clear them when a build is done 
		// successfully which isn't entirely correct because the assemblies which
		// were built may not include all of the user's changes. TODO: might
		// instead want to listen for notifications from the object-model that
		// a type was parsed and clear the parses which include that type.
		private void DoClearClosed()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			lock (m_mutex)
			{
				var deathRow = new List<string>();
				foreach (string key in m_parses.Keys)
				{
					if (!DoIsOpen(windows, key))
						deathRow.Add(key);
				}
				
				foreach (string key in deathRow)
				{
					m_parses.Remove(key);
				}
			}
		}
		
		private bool DoIsOpen(IWindows windows, string key)
		{
			foreach (Boss boss in windows.All())
			{
				var editor = boss.Get<ITextEditor>();
				if (key == editor.Key)
					return true;
			}
			
			return false;
		}
		
		// It's very important that the parses be properly purged so we'll
		// log when they start stacking up.
		[Conditional("DEBUG")]
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoCheckHighwater()
		{
			if (m_parses.Count > m_highwater)
			{
				m_highwater = m_parses.Count;
				Log.WriteLine(TraceLevel.Info, "App", "Parses highwater mark is now {0}", m_highwater);
			}
		}
		#endregion
		
		#region Private Types
		[ThreadModel(ThreadModel.Concurrent)]
		private sealed class Job
		{
			public Job(int edit, string text)
			{
				Edit = edit;
				Text = text;
			}
			
			public int Edit {get; private set;}
			
			public string Text {get; private set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private object m_mutex = new object();
			private Dictionary<string, Job> m_jobs = new Dictionary<string, Job>();
			private Dictionary<string, Parse> m_parses = new Dictionary<string, Parse>();
			private int m_highwater = 20;
		#endregion
	}
}
