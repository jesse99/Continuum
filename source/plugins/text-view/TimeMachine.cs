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
using MObjc;
using Mono.Unix;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;

namespace TextView
{
	internal sealed class TimeMachine : ITextContextCommands, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			m_root = DoGetRoot();
			m_timeMachineDir = DoGetTimeMachineDir();
			
			if (m_timeMachineDir != null)
			{
				boss = ObjectModel.Create("Application");
				var handler = boss.Get<IMenuHandler>();
				
				handler.Register(this, 43, this.DoOpen, this.DoCanOpen);
				Broadcaster.Register("opening document window", this);
				Broadcaster.Register("document path changed", this);
				Broadcaster.Register("closed document window", this);
			}
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, string language, bool editable, List<TextContextItem> items)
		{
			if (selection == null && m_timeMachineDir != null)
			{
				ITextEditor editor = DoGetMainEditor();
				if (editor != null && editor.Path != null)
				{
					if (DoCanOpen(editor.Path))
					{
						items.Add(new TextContextItem(0.1f));
						items.Add(new TextContextItem("Find in Time Machine", s => {DoOpen(); return s;}, 0.11f));
					}
					else
					{
						string tmPath = editor.Path;
						var entries = new List<Backup>();
						bool isNew;
						
						lock (m_mutex)
						{
							var tmIndex = m_backups.Indexer<UniqueIndexer<string, Backup>>("tm path");
							if (tmIndex.ContainsItem(tmPath))
							{
								string realPath = tmIndex.GetItem(tmPath).RealPath;
								
								var realIndex = m_backups.Indexer<Indexer<string, Backup>>("real path");
								foreach (Backup backup in realIndex.GetItems(realPath))
								{
									entries.Add(backup);
								}
							}
							
							isNew = m_newFiles.Contains(editor.Path);	// if this is true we need to, or are in the middle of, finding time machine paths for this file
						}
						
						if (entries.Count > 0)
						{
							items.Add(new TextContextItem(0.9f));
							
							entries.Sort((lhs, rhs) => rhs.WriteTime.CompareTo(lhs.WriteTime));
							int j = entries.FindIndex(i => i.TimeMachinePath == tmPath);
							int min = Math.Max(j - 10, 0);
							int max = Math.Min(min + 21, entries.Count);
							
							float sortOrder = 0.9f;
							if (min > 0)
							{
								items.Add(new TextContextItem(Constants.Ellipsis, sortOrder));
								sortOrder += 0.0001f;
							}
							
							for (int i = min; i < max; ++i)
							{
								Backup entry = entries[i];
								
								TimeSpan age = DateTime.Now - entry.WriteTime;
								string title = "Open from " + TimeMachineWindowTitle.AgeToString(age);
								string tmP = entry.TimeMachinePath;		// need a temp or the delegate will use a value mutated by foreach
								
								var item = new TextContextItem(title, s => {DoOpen(tmP); return s;}, sortOrder);
								item.State = tmP == tmPath ? 1 : 0;
								items.Add(item);
								sortOrder += 0.0001f;
							}
							
							if (max < entries.Count)
								items.Add(new TextContextItem(Constants.Ellipsis, sortOrder));
						}
						else if (isNew)
						{
							items.Add(new TextContextItem(0.9f));
							items.Add(new TextContextItem("Finding old Files", null, 0.9f));
						}
					}
				}
			}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opening document window":
				case "document path changed":
					DoAddTitle((Boss) value);
					break;
					
				case "closed document window":
					DoPruneBackups();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoAddTitle(Boss boss)
		{
			if (boss.Has<ITextEditor>() && !boss.Has<IDocumentWindowTitle>())
			{
				var editor = boss.Get<ITextEditor>();
				if (editor.Path != null && editor.Path.StartsWith(m_timeMachineDir))
				{
					boss.ExtendWithInterface(typeof(TimeMachineWindowTitle), typeof(IDocumentWindowTitle));
				}
			}
		}
		
		private ITextEditor DoGetMainEditor()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			ITextEditor editor = null;
			if (boss != null)
				editor = boss.Get<ITextEditor>();
			
			return editor;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private string DoGetTimeMachinePath(string realPath, string version)
		{
			Contract.Requires(!string.IsNullOrEmpty(realPath), "realPath is null or empty");
			Contract.Requires(realPath[0] == '/', "realPath isn't rooted: " + realPath);
			
			// /Users/micky/foo.txt => /Volumes/Macintosh HD/Users/micky/foo.txt
			realPath = Path.Combine(m_root, realPath.Substring(1));
			
			// /Volumes/Macintosh HD/Users/micky/foo.txt => Macintosh HD/Users/micky/foo.txt
			realPath = realPath.Substring(realPath.IndexOf('/', 1) + 1);
			
			// /Volumes/LaCie/Backups.backupdb/Jesse Jones’s Mac Pro/${version}/
			string path = Path.Combine(m_timeMachineDir, version);
			
			// /Volumes/LaCie/Backups.backupdb/Jesse Jones’s Mac Pro/${version}/Macintosh HD/Users/micky/foo.txt
			path = Path.Combine(path, realPath);
			
			return path;
		}
		
		private bool DoCanOpen(string realPath)
		{
			bool can = false;
			
			if (m_root != null && m_timeMachineDir != null)
			{
				string path = DoGetTimeMachinePath(realPath, "Latest");
				can = path != null && File.Exists(path);
			}
			
			return can;
		}
		
		private bool DoCanOpen()
		{
			bool can = false;
			
			ITextEditor editor = DoGetMainEditor();
			if (editor != null && editor.Path != null)
			{
				can = DoCanOpen(editor.Path);
			}
			
			return can;
		}
		
		private void DoOpen()
		{
			ITextEditor editor = DoGetMainEditor();
			if (editor != null && editor.Path != null)
			{
				string tmPath = DoGetTimeMachinePath(editor.Path, "Latest");
				if (tmPath != null)
				{
					NSString s = NSString.Create(tmPath);
					s = s.stringByResolvingSymlinksInPath();
					DoOpen(s.description());
					
					lock (m_mutex)
					{
						m_newFiles.Add(editor.Path);
						Monitor.Pulse(m_mutex);
					}
				}
			}
		}
		
		private void DoOpen(string tmPath)
		{
			if (m_thread == null)
			{
				m_thread = new Thread(this.DoThread);
				m_thread.Name = "TimeMachine.DoThread";
				m_thread.IsBackground = true;		// allow the app to quit even if the thread is still running
				m_thread.Start();
			}
			
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(tmPath, -1, -1, 4);
		}
		
		private string DoGetRoot()
		{
			foreach (string v in Directory.GetDirectories("/Volumes"))
			{
				NSString str = NSString.Create(v);
				str = str.stringByResolvingSymlinksInPath();
				if (str.Equals("/"))
				{
					Log.WriteLine(TraceLevel.Verbose, "App", "'/' maps to '{0}'", v);
					return v;
				}
			}
			
			Log.WriteLine(TraceLevel.Warning, "App", "couldn't find the root directory under '/Volumes'");
			return null;
		}
		
		// This will return something like "/Volumes/LaCie/Backups.backupdb/Jesse Jones’s Mac Pro".
		private string DoGetTimeMachineDir()
		{
			// TODO: is there a better way to do this? NSSearchPathForDirectoriesInDomains
			// doesn't seem to do it...
			string root = null;
			foreach (string volume in Directory.GetDirectories("/Volumes"))
			{
				string backups = Path.Combine(volume, "Backups.backupdb");
				root = DoFindTimeMachineDir(backups);
				if (root != null)
					break;
			}
			
			if (root != null)
				Log.WriteLine(TraceLevel.Info, "App", "time machine dir: '{0}'", root);
			else
				Log.WriteLine(TraceLevel.Warning, "App", "couldn't find a volume with a Backups.backupdb directory");
			
			return root;
		}
		
		private string DoFindTimeMachineDir(string backups)
		{
			if (Directory.Exists(backups))
			{
				foreach (string dir in Directory.GetDirectories(backups))
				{
					if (Directory.Exists(Path.Combine(dir, "Latest")))
						return dir;
				}
				
				Log.WriteLine(TraceLevel.Warning, "App", "couldn't find a 'Latest' directory in any of the directories inside '{0}'", backups);
			}
			
			return null;
		}
		
		private void DoPruneBackups()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			Boss[] bosses = boss.Get<IWindows>().All();
			IEnumerable<string> paths = from b in bosses let e = b.Get<ITextEditor>() select e.Path;
			
			lock (m_mutex)
			{
				var deathRow = new List<Backup>();
				
				var realIndex = m_backups.Indexer<Indexer<string, Backup>>("real path");
				foreach (string realPath in realIndex.GetKeys().Distinct())
				{
					if (!paths.Contains(realPath))
					{
						if (!realIndex.GetItems(realPath).Any(b => paths.Contains(b.TimeMachinePath)))
						{
							deathRow.AddRange(realIndex.GetItems(realPath));
						}
					}
				}
				
				var tmIndex = m_backups.Indexer<UniqueIndexer<string, Backup>>("tm path");
				foreach (Backup b in deathRow)
				{
					tmIndex.RemoveItem(b.TimeMachinePath);
				}
				Log.WriteLine(TraceLevel.Verbose, "App", "removed {0} backups", deathRow.Count);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoThread()
		{
			try
			{
				while (true)
				{
					int index;
					lock (m_mutex)
					{
						while (m_newFiles.Count == 0)
						{
							Unused.Value = Monitor.Wait(m_mutex);
						}
						
						index = m_newFiles.Count - 1;
					}
					
					DoFindBackups(m_newFiles[index]);
					
					lock (m_mutex)
					{
						m_newFiles.RemoveAt(index);
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "TimeMachine thread died:");
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
			}
			
			m_thread = null;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoFindBackups(string realPath)
		{
			var backups = new Dictionary<DateTime, string>();	// write time => tmPath
			
			foreach (string path in Directory.GetDirectories(m_timeMachineDir))
			{
				string version = Path.GetFileName(path);
				if (char.IsDigit(version[0]))
				{
					string tmPath = DoGetTimeMachinePath(realPath, version);
					if (File.Exists(tmPath))
					{
						DateTime date = File.GetLastWriteTime(tmPath);
						backups[date] = tmPath;
					}
				}
			}
			
			lock (m_mutex)
			{
//				Console.WriteLine("adding backups for {0}", realPath);
				var tmIndex = m_backups.Indexer<UniqueIndexer<string, Backup>>("tm path");
				foreach (var b in backups)
				{
					if (!tmIndex.ContainsItem(b.Value))
						m_backups.Add(new Backup(realPath, b.Value, b.Key));
//					Console.WriteLine("    {0} {1}", b.TimeMachinePath, TimeMachineWindowTitle.AgeToString(DateTime.Now - b.WriteTime));
				}
				
				if (m_backups.Count > m_highwater)
				{
					m_highwater = m_backups.Count;
					Log.WriteLine(TraceLevel.Info, "App", "time machine highwater is now {0}", m_highwater);
				}
			}
		}
		#endregion
		
		#region Private Types
		private struct Backup
		{
			public Backup(string rp, string tp, DateTime wt) : this()
			{
				RealPath = rp;
				TimeMachinePath = tp;
				WriteTime = wt;
			}
			
			public string RealPath {get; private set;}
			
			public string TimeMachinePath {get; private set;}
			
			public DateTime WriteTime {get; private set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_root;					// the volume "/" maps to, e.g. "/Volumes/Macintosh HD"
		private string m_timeMachineDir;		// contains the Latest directory plus the time-stamped directories
		
		private Thread m_thread;
		private object m_mutex = new object();
			private MultiIndex<Backup> m_backups = new MultiIndex<Backup>(
				new OrderedNonUnique<string, Backup>("real path", b => b.RealPath),
				new OrderedUnique<string, Backup>("tm path", b => b.TimeMachinePath));
			private int m_highwater = 30;
			
			private List<string> m_newFiles = new List<string>();
		#endregion
	}
}
