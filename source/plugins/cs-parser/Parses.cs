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
using System.Threading;

namespace CsParser
{
	internal sealed class Parses : IParses, IObserver
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
			
			Broadcaster.Register("built target", this);
			
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
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void OnEdit(string language, string path, int edit, string text)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			Contract.Requires(text != null, "text is null");
			
			if (language == "CsLanguage")
			{
				lock (m_mutex)
				{
					m_jobs[path] = new Job(edit, text);
					Monitor.Pulse(m_mutex);
				}
			}
		}
		
		public Parse TryParse(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			Parse parse;
			lock (m_mutex)
			{
				Unused.Value = m_parses.TryGetValue(path, out parse);
			}
			
			return parse;
		}
		
		public Parse Parse(string path, int edit, string text)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			Contract.Requires(text != null, "text is null");
			
			Parse result = TryParse(path);
			if (result == null || result.Edit != edit)
			{
				Job job = new Job(edit, text);
				Parser parser = Thread.CurrentThread.ManagedThreadId == 1 ? m_parser : new Parser();
				result = DoParse(parser, job);
				
				lock (m_mutex)
				{
					m_parses[path] = result;
				}
				
				if (Thread.CurrentThread.ManagedThreadId == 1)
					Broadcaster.Invoke("parsed file", path);
				else
					NSApplication.sharedApplication().BeginInvoke(() => Broadcaster.Invoke("parsed file", path));
			}
			
			return result;
		}
		
		public CsType FindType(string fullName)
		{
			CsType type = null;
			
			lock (m_mutex)
			{
				foreach (Parse parse in m_parses.Values)
				{
					type = DoFindType(parse.Globals, fullName);
					if (type != null)
						break;
				}
			}
			
			return type;
		}
		
		#region Private Methods		
		private CsType DoFindType(CsTypeScope scope, string fullName)
		{
			CsType result = null;
			
			CsType candidate = scope as CsType;
		
			if (candidate != null && candidate.FullName == fullName)
				result = candidate;
			
			for (int i = 0; i < scope.Types.Length && result == null; ++i)
				result = DoFindType(scope.Types[i], fullName);
				
			CsNamespace ns = scope as CsNamespace;
			if (ns != null)
			{
				for (int i = 0; i < ns.Namespaces.Length && result == null; ++i)
					result = DoFindType(ns.Namespaces[i], fullName);
			}
			
			return result;
		}
		
		private void DoThread()		// threaded
		{
			Parser parser = new Parser();
			
			while (true)
			{
				string path = null;
				Job job = null;
				
				lock (m_mutex)
				{
					while (m_jobs.Count == 0)
					{
						Unused.Value = Monitor.Wait(m_mutex);
					}
					
					path = m_jobs.Keys.First();
					job = m_jobs[path];
					m_jobs.Remove(path);
				}
				
				Parse parse = DoParse(parser, job);
				lock (m_mutex)
				{
					Parse last;
					Unused.Value = m_parses.TryGetValue(path, out last);
					if (last == null || last.Edit < parse.Edit)
					{
						m_parses[path] = parse;
						
						NSApplication.sharedApplication().BeginInvoke(
							() => Broadcaster.Invoke("parsed file", path));
					}
				}
			}
		}
		
		private Parse DoParse(Parser parser, Job job)		// threaded
		{
			int index, length;
			CsGlobalNamespace globals;
			Token[] tokens, comments;
			parser.TryParse(job.Text, out index, out length, out globals, out tokens, out comments);
			
			return new Parse(job.Edit, index, length, globals, comments, tokens);
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
				foreach (string path in m_parses.Keys)
				{
					if (!DoIsOpen(windows, path))
						deathRow.Add(path);
				}
				
				foreach (string path in deathRow)
				{
					m_parses.Remove(path);
				}
			}
		}
		
		private bool DoIsOpen(IWindows windows, string path)
		{
			foreach (Boss boss in windows.All())
			{
				var editor = boss.Get<ITextEditor>();
				if (path == editor.Path)
					return true;
			}
			
			return false;
		}
		#endregion
		
		#region Private Types
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
		private Parser m_parser = new Parser();
		private object m_mutex = new object();
			private Dictionary<string, Job> m_jobs = new Dictionary<string, Job>();
			private Dictionary<string, Parse> m_parses = new Dictionary<string, Parse>();
		#endregion
	}
}
