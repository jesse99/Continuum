// Copyright (C) 2007-2008 Jesse Jones
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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Find
{
	internal abstract class BaseFindInFiles : IFindProgress
	{
		protected BaseFindInFiles(string directory, string[] include, string[] exclude)
		{	
			m_directory = directory;
			m_include = include;
			m_exclude = exclude;
			
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			m_encoding = boss.Get<ITextEncoding>();
			
			boss = ObjectModel.Create("Application");
			m_transcript = boss.Get<ITranscript>();
		}
		
		public void Run()
		{
			Contract.Requires(m_thread == null, "can't restart");
			
			// Get a list of all the files we need to process.
			DoGetFiles(m_directory);
			m_fileCount = m_files.Count;	
			
			// Process any that we have open.
			DoProcessOpenFiles();
			
			// For any that are left spin off a thread to handle them.
			m_thread = new Thread(this.DoThread);
			m_thread.Start();
		}
		
		public string Title
		{
			get {return m_title;}	
			set {m_title = value;}
		}
		
		public int FileCount
		{
			get {return m_fileCount;}	
		}
		
		public int ProcessedCount
		{
			get {return m_processCount;}
		}
		
		public int ChangeCount
		{
			get {return m_changeCount;}
		}
		
		public string Processing
		{
			get {return m_processing;}
		}
		
		public bool Cancelled
		{
			get {return m_cancelled;}
			set {m_cancelled = value;}
		}
		
		#region Protected Methods
		[ThreadModel(ThreadModel.Concurrent)]
		protected abstract NSFileHandle OnOpenFile(string path);
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected abstract string OnProcessFile(string file, string text);
		#endregion
		
		#region Private Methods
		private void DoProcessOpenFiles()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			
			var windows = boss.Get<IWindows>();
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				int i = m_files.IndexOf(editor.Path);
				if (i >= 0)
				{
					var text = b.Get<IText>();
					
					string result = OnProcessFile(editor.Path, text.Text);
					if (result != text.Text)
					{
						int index = text.Selection.location;
						text.Replace(result, 0, text.Text.Length, "Replace All");
						
						text.Selection = new NSRange(index, 0);
						text.ShowSelection();
						
						++m_changeCount;
					}
					
					++m_processCount;
					m_files.RemoveAt(i);
				}
			}
		}
		
		private void DoReload()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			
			var windows = boss.Get<IWindows>();
			foreach (Boss b in windows.All())
			{
				var reload = b.Get<IReload>();
				reload.Reload();
			}
		}
		
		private void DoGetFiles(string directory)
		{
			try
			{
				string[] files = Directory.GetFiles(directory);
				foreach (string file in files)
				{
					if (DoIsValidFile(file))
						m_files.Add(file);
				}
				
				string[] dirs = Directory.GetDirectories(directory);
				foreach (string dir in dirs)
				{
					if (DoIsValidDir(dir))
						DoGetFiles(dir);
				}
			}
			catch (IOException)
			{
				// If the file system is changing via another process we may land here.
			}
		}
		
		private bool DoIsValidFile(string file)
		{
			string name = Path.GetFileName(file);
			
			bool include = m_include.Length == 0;
			if (!include)
			{
				foreach (string glob in m_include)
				{
					if (Glob.Match(glob, name))
					{
						include = true;
						break;
					}
				}
			}
			
			if (include)
			{
				foreach (string glob in m_exclude)
				{
					if (Glob.Match(glob, name))
					{
						include = false;
						break;
					}
				}
			}
			
			return include;
		}
		
		private bool DoIsValidDir(string dir)
		{
			string name = Path.GetFileName(dir.TrimEnd('/'));
			
			foreach (string glob in m_exclude)
			{
				if (Glob.Match(glob, name))
					return false;
			}
			
			return true;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoThread()
		{
			// Then process each file in turn.
			for (int i = 0; i < m_files.Count && !m_cancelled; ++i)
			{
				if (m_cancelled)
					break;
					
				++m_processCount;		// note that ints and references can be read and written atomically (but not longs)
				m_processing = m_files[i];
				
				DoProcessFile(m_processing);
			}
			
			m_files.Clear();
			
			// Finally we need to reload any windows the user may have opened
			// while we were in our thread.
			if (m_changeCount > 0)
				NSApplication.sharedApplication().BeginInvoke(this.DoReload);
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoProcessFile(string file)
		{
			NSAutoreleasePool pool = NSAutoreleasePool.Create();
			
			try
			{
				NSFileHandle handle = OnOpenFile(file);
				NSData data = handle.readDataToEndOfFile();
				
				uint coding;
				var result = m_encoding.Decode(data, out coding);
				
				string oldText = result.description();
				string newText = OnProcessFile(file, oldText);
				if (newText != oldText)
				{
					data = m_encoding.Encode(NSString.Create(newText), coding);
					
					handle.seekToFileOffset(0);
					handle.writeData(data);
					handle.truncateFileAtOffset(data.length());
					++m_changeCount;
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't do the search/replace for '{0}':", file);
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
				
				m_transcript.WriteLine(Output.Error, "Couldn't do the search/replace for '{0}'.", file);
				m_transcript.WriteLine(Output.Error, e.Message);
			}
			finally
			{
				pool.release();
			}
		}
		#endregion
		
		#region Fields
		private string m_directory;
		private string[] m_include;
		private string[] m_exclude;
		private Thread m_thread;
		private List<string> m_files = new List<string>(50);
		private ITextEncoding m_encoding;
		private ITranscript m_transcript;
		
		private volatile string m_title = string.Empty;
		private volatile int m_fileCount = 1000;		// default to something non-zero so the progress window doesn't think we are done on startup
		private volatile int m_processCount;
		private volatile int m_changeCount;
		private volatile string m_processing = string.Empty;
		private volatile bool m_cancelled;
		#endregion
	}
}
