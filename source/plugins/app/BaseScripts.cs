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
using MObjc;
using Mono.Unix;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace App
{
	internal abstract class BaseScripts : IStartup
	{
		protected BaseScripts(string dirName, int baseTag)
		{
			m_dirName = dirName;
			m_installedPath = Path.Combine(Paths.ScriptsPath, dirName);
			
			m_baseTag = baseTag;
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;

//			string path = NSBundle.mainBundle().resourcePath().description();	// can't do this in the ctor because it will register cocoa classes too soon
//			m_resourcesPath = Path.Combine(path, m_dirName);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public void OnStartup()
		{
			DoCreateDirectories();
			DoCopyMissingFiles();
			DoOverwriteFiles();
			
			DoFilesChanged(null, null);
			
			m_watcher = new DirectoryWatcher(m_installedPath, TimeSpan.FromMilliseconds(250));
			m_watcher.Changed += this.DoFilesChanged;
		}
		
		#region Protected Methods
		protected List<string> Items
		{
			get {return m_items;}
		}
		
		protected abstract void RemoveScriptsFromMenu();
		
		protected abstract Tuple2<NSMenu, int> GetScriptsLocation();
		
		protected abstract void Execute(int index);
		
		protected virtual void OnAddCustom(List<string> items)
		{
		}
		
		protected virtual bool IsEnabled()
		{
			return true;
		}
		
		protected void Rebuild()
		{
			DoRebuildMenu();
		}
		
		protected void SaveFile(string path)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			
			var windows = boss.Get<IWindows>();
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (path == editor.Path)
				{
					editor.Save();
					break;
				}
			}
		}
		
		protected virtual bool OnIsValidFile(string file)
		{
			bool valid = false;
			
			if (File.Exists(file))
			{
				try
				{
					var info = new UnixFileInfo(file);
					if ((info.FileAccessPermissions & FileAccessPermissions.UserExecute) == FileAccessPermissions.UserExecute)
					{
						valid = true;
					}
					else
					{
						string ext = Path.GetExtension(file);
						if (ext == ".py" || ext == ".sh" || ext == ".ref")
							Console.WriteLine("'{0}' is not executable.", file);
					}
				}
				catch (InvalidOperationException)
				{
					// It's possible for the code above to fail if the file disappears after we
					// check to see if it exists (and this is relatively common when running
					// both Foreshadow and Continuum) so we need to trap exceptions
					// from UnixFileInfo.
					valid = false;
				}
				catch (IOException)
				{
					valid = false;
				}
			}
			
			return valid;
		}
		
		#endregion
		
		#region Private Methods
		private void DoCreateDirectories()
		{
			if (!Directory.Exists(m_installedPath))
				Directory.CreateDirectory(m_installedPath);

			string path = Path.Combine(m_installedPath, "standard");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			path = Path.Combine(m_installedPath, "user");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			path = Path.Combine(m_installedPath, "unused");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}
		
		private void DoCopyMissingFiles()
		{
			string path = NSBundle.mainBundle().resourcePath().description();	// can't do this in the ctor because it will register cocoa classes too soon
			m_resourcesPath = Path.Combine(path, m_dirName);

			string standardPath = Path.Combine(m_installedPath, "standard");
			string[] resourceScripts = Directory.GetFiles(m_resourcesPath);
						
			var scripts = new List<string>();
			scripts.AddRange(Directory.GetFiles(standardPath));
			scripts.AddRange(Directory.GetFiles(Path.Combine(m_installedPath, "unused")));

			try
			{
				foreach (string src in resourceScripts)
				{
					string name = Path.GetFileName(src);
					if (name[0] != '.')
					{
						if (!scripts.Exists(s => Path.GetFileName(s) == name))
						{
							string dst = Path.Combine(standardPath, name);
							File.Copy(src, dst);
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "Couldn't add '{0}'", m_installedPath);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
		}
		
		private void DoOverwriteFiles()
		{
			string standardPath = Path.Combine(m_installedPath, "standard");
			string[] resourceScripts = Directory.GetFiles(m_resourcesPath);
			
			try
			{
				foreach (string src in resourceScripts)	
				{
					string name = Path.GetFileName(src);
					string dst = Path.Combine(standardPath, name);
					
					if (name[0] != '.')
					{
						if (File.Exists(dst))
						{
							if (File.GetLastWriteTime(src) > File.GetLastWriteTime(dst))
							{
								File.Delete(dst);
								File.Copy(src, dst);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "Couldn't update '{0}'", m_installedPath);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
		}
		
		private void DoRebuildMenu()
		{
			RemoveScriptsFromMenu();
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Deregister(this);
			
			DoUpdateMenu();
		}
		
		private void DoUpdateMenu()
		{
			Tuple2<NSMenu, int> loc = GetScriptsLocation();
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			
			m_items.Clear();
			m_items.AddRange(m_scripts);
			OnAddCustom(m_items);
			
			m_items.Sort((lhs, rhs) => Path.GetFileName(lhs).CompareTo(Path.GetFileName(rhs)));
			
			NSApplication app = NSApplication.sharedApplication();
			for (int i = m_items.Count - 1; i >= 0 ; --i)
			{
				string name = Path.GetFileNameWithoutExtension(m_items[i]);
				int tag = unchecked(m_baseTag + i);
				
				var item = NSMenuItem.Create(name, "appHandler:", app.delegate_());
				item.setTag(tag);
				loc.First.insertItem_atIndex(item, loc.Second);
				
				int k = i;
				handler.Register(this, tag, () => Execute(k), this.IsEnabled);
			}
		}
		
		private IEnumerable<string> DoGetFiles(string dir)
		{
			return from file in Directory.GetFiles(Path.Combine(m_installedPath, dir))
				where OnIsValidFile(file) select file;
		}
		
		// Note that this is also called if a file in the directory is modified.
		private void DoFilesChanged(object sender, DirectoryWatcherEventArgs e)
		{
			var scripts = new List<string>();
			scripts.AddRange(DoGetFiles("standard"));
			scripts.AddRange(DoGetFiles("user"));
			
			bool changed = scripts.Count != m_scripts.Count || scripts.Union(m_scripts).Count() != m_scripts.Count;
			if (changed)
			{
				m_scripts = scripts;
				DoRebuildMenu();
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_dirName;
		private int m_baseTag;
		private string m_installedPath;
		private string m_resourcesPath;
		private List<string> m_scripts = new List<string>();
		private List<string> m_items = new List<string>();
		private DirectoryWatcher m_watcher;
		#endregion
	} 
}