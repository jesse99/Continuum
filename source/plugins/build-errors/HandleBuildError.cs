// Copyright (C) 2008 Jesse Jones
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
using Shared;
using System;
using System.Diagnostics;
using System.IO;

namespace BuildErrors
{
	internal sealed class HandleBuildError : IHandleBuildError, IStartup, IFactoryPrefs, IBuildErrors
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			dict.setObject_forKey(NSString.Create("Georgia"), NSString.Create("errors font name"));
			dict.setObject_forKey(NSNumber.Create(14.0f), NSString.Create("errors font size"));
			
			var attrs = NSDictionary.dictionaryWithObject_forKey(NSColor.redColor(), Externs.NSForegroundColorAttributeName);
			NSData data = NSArchiver.archivedDataWithRootObject(attrs);
			dict.setObject_forKey(data, NSString.Create("errors font attributes"));
			
			NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(255/255.0f, 229/255.0f, 229/255.0f, 1.0f);
			data = NSArchiver.archivedDataWithRootObject(color);
			dict.setObject_forKey(data, NSString.Create("errors color"));
		}
		
		public void OnStartup()
		{
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			
			handler.Register(this, 53, () => {--m_current; DoHandle();}, this.DoCanGotoPreviousError);
			handler.Register(this, 54, () => {++m_current; DoHandle();}, this.DoCanGotoNextError);
			
			m_controller = new ErrorsController();
			Unused.Value = m_controller.Retain();
		}
		
		public void Reset()
		{
			m_dirPath = null;
			m_errors = null;
			m_controller.Clear();
			
			Broadcaster.Unregister(this);
		}
		
		public void Close()
		{
			m_controller.window().orderOut(m_controller);
			m_boss.CallRepeated<IDisplayBuildError>(i => i.Clear());
		}
		
		public void Set(string dirPath, BuildError[] errors)
		{
			Trace.Assert(!string.IsNullOrEmpty(dirPath), "dirPath is null or empty");
			Trace.Assert(errors != null, "errors is null");
			Trace.Assert(errors.Length > 0, "errors is empty");
			
			m_dirPath = dirPath;
			m_errors = errors;
			m_current = 0;
			DoHandle();
			
			Broadcaster.Register("text lines changed", this, this.DoUpdateLines);
		}
		
		public void ShowCurrent()
		{
			if (m_errors != null && m_current < m_errors.Length)
				DoHandle();
		}
		
		public int Count
		{
			get {return m_errors != null ? m_errors.Length : 0;}
		}
		
		public BuildError Get(int index)
		{
			return m_errors[index];
		}
		
		public void Show(int index)
		{
			m_current = index;
			DoHandle();
		}
		
		#region Private Methods
		private bool DoCanGotoPreviousError()
		{
			return m_current > 0 && m_errors != null && m_errors.Length > 0;
		}
		
		private bool DoCanGotoNextError()
		{
			return m_errors != null && m_current + 1 < m_errors.Length;
		}
		
		private void DoHandle()
		{
			BuildError error = m_errors[m_current];
			
			string path = System.IO.Path.Combine(m_dirPath, error.File);
			if (!File.Exists(path))
				path = DoFindFile(m_dirPath, error.File);
			
			int tabWidth = error.Tool == "gmcs" ? 8 : 1;	
			if (path != null)
			{
				Boss boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(path, error.Line, error.Column, tabWidth);
				
				m_controller.Set(error.Message, m_current, m_errors.Length);
			}
			
			if (path != null && error.Line > 0)
				m_boss.CallRepeated<IDisplayBuildError>(i => i.Display(path, error.Line, error.Column, tabWidth));
			else
				m_boss.CallRepeated<IDisplayBuildError>(i => i.Clear());
		}
		
		private void DoUpdateLines(string name, object value)
		{
			var data = (Tuple3<string, int, int>) value;
			
			foreach (BuildError error in m_errors)
			{
				string path = System.IO.Path.Combine(m_dirPath, error.File);
				if (Paths.AreEqual(path, data.First))
				{
					if (data.Third <= 0)
						if (data.Second <= error.Line && error.Line <= data.Second - data.Third)
							error.Column = -1;
					
					if (data.Second < error.Line)
						error.Line += data.Third;	
				}
			}
		}
		
		// Normally we only have to do a search with recursive make files 
		// where path will be a relative path somewhere within root.
		private string DoFindFile(string root, string path)
		{
			try
			{
				string[] dirs = Directory.GetDirectories(root);
				foreach (string dir in dirs)		
				{
					if (Path.GetFileName(dir)[0] != '.') 
					{
						string candidate = Path.Combine(dir, path);
						if (File.Exists(candidate))
						{
							return candidate;
						}
						else
						{	
							candidate = DoFindFile(dir, path);
							if (candidate != null)
								return candidate;
						}
					}
				}
			}
			catch (IOException)
			{
				// If the file system is changing via another process we may land here.
			}
			
			return null;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_dirPath;
		private BuildError[] m_errors;
		private int m_current;
		private ErrorsController m_controller;
		#endregion
	} 
}
