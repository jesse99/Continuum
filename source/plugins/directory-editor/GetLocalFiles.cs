// Copyright (C) 2010 Jesse Jones
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DirectoryEditor
{
	internal sealed class GetLocalFiles : IGetFiles, IFileColor, IOpened, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			Broadcaster.Register("directory prefs changed", this);
		}
		
		public void Opened()
		{
			var window = m_boss.Get<IWindow>();
			m_controller = (DirectoryController) window.Window.windowController();
			Contract.Assert(m_controller != null);
			
			DoReset();
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "directory prefs changed":
					if (m_boss.Equals(value))
						DoReset();
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public NSColor GetColor(string fileName)
		{
			NSColor color;
			
			lock (m_mutex)
			{
				color = DirectoryItemStyler.GetFileColor(fileName, m_fileGlobs, m_fileColors);
			}
			
			return color;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public string Path
		{
			get {return m_path;}
		}
		
		// Note that we don't use this to construct the directory table view because we want to
		// lazily create the table items (directories can potentially have lots of items).
		[ThreadModel(ThreadModel.Concurrent)]
		public void GetFiles(List<string> files, List<NSColor> colors = null)
		{
			lock (m_mutex)
			{
				DoGetFiles(m_path, files, colors);
			}
		}
		
		#region Private Methods 
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoGetFiles(string dir, List<string> files, List<NSColor> colors)
		{
			try
			{
				foreach (string entry in Directory.GetFileSystemEntries(dir))
				{
					string name = System.IO.Path.GetFileName(entry);
					if (!DirectoryController.IsIgnored(name, m_ignored))
					{
						if (File.Exists(entry))
						{
							files.Add(entry);
							if (colors != null)
							{
								NSColor color = DirectoryItemStyler.GetFileColor(name, m_fileGlobs, m_fileColors);
								colors.Add(color);
							}
						}
						else if (Directory.Exists(entry))
						{
							DoGetFiles(entry, files, colors);
						}
					}
				}
			}
			catch (IOException e)
			{
				// Weird things can happen if the file system is mutated as we're trying to iterate over it.
				Log.WriteLine(TraceLevel.Warning, "Errors", "{0} getting local files for {1}", e.Message, dir);
			}
		}
		
		private void DoReset()
		{
			lock (m_mutex)
			{
				m_path = m_controller.Path;
				
				// We make copies of all of these collections because some of them
				// get mutated on updates.
				m_ignored = (string[]) m_controller.IgnoredItems.Clone();
				m_fileGlobs = (string[][]) m_controller.Styler.FileGlobs.Clone();
				
				if (m_fileColors != m_controller.Styler.FileColors)
				{
					foreach (NSColor color in m_fileColors)
					{
						color.release();
					}
					
					m_fileColors = (NSColor[]) m_controller.Styler.FileColors.Clone();
					
					foreach (NSColor color in m_fileColors)
					{
						color.retain();
					}
				}
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private DirectoryController m_controller;
		
		private object m_mutex = new object();
			private string m_path = string.Empty;
			private string[] m_ignored = new string[0];
			private string[][] m_fileGlobs = new string[][]{};
			private NSColor[] m_fileColors = new NSColor[0];
		#endregion
	}
}
