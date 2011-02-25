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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace App
{
	internal sealed class Launcher : ILaunch
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Launch(string path, int line, int col, int tabWidth)
		{
			try
			{
				// If we don't have a glob for the path then try to open it using the Finder.
				bool opened = false;
				if (!DoCanOpen(path))
				{
					Boss boss = ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					opened = fs.Launch(path);
				}
				
				// If we have a glob or can't open it with the Finder then use Continuum.
				if (!opened)
				{
					NSError err;
					NSURL url = NSURL.fileURLWithPath(NSString.Create(path));
					Unused.Value = NSDocumentController.sharedDocumentController().openDocumentWithContentsOfURL_display_error(
						url, true, out err);
					
					if (err != null)
						err.Raise();
					
					if (line != -1)
						DoShowLine(path, line, col, tabWidth);
//						NSApplication.sharedApplication().BeginInvoke(() => DoShowLine(path, line, col, tabWidth));	// use BeginInvoke in case the text controller restores the scrollers
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't open '{0}'", path);
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString title = NSString.Create("Couldn't open '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		public void Launch(string path, NSRange selection)
		{
			try
			{
				bool opened = false;
				if (!DoCanOpen(path))
				{
					Boss boss = ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					opened = fs.Launch(path);
				}
				
				if (!opened)
				{
					NSError err;
					NSURL url = NSURL.fileURLWithPath(NSString.Create(path));
					Unused.Value = NSDocumentController.sharedDocumentController().openDocumentWithContentsOfURL_display_error(
						url, true, out err);
					
					if (err != null)
						err.Raise();
					
					DoSetSelection(path, selection);
//					NSApplication.sharedApplication().BeginInvoke(() => DoSetSelection(path, selection));		// use BeginInvoke in case the text controller restores the scrollers
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't open '{0}'", path);
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString title = NSString.Create("Couldn't open '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		#region Private Methods
		private bool DoCanOpen(string path)
		{
			Boss boss = ObjectModel.Create("Application");
			foreach (var can in boss.GetRepeated<ICanOpen>())
			{
				if (can.Can(path))
					return true;
			}
			
			return false;
		}
		
		private void DoSetSelection(string path, NSRange selection)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (editor.Path != null && Paths.AreEqual(editor.Path, path))
				{
					var text = b.Get<IText>();
					text.Selection = selection;
					text.ShowSelection();
				}
			}
		}
		
		private void DoShowLine(string path, int line, int col, int tabWidth)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (editor.Path != null && Paths.AreEqual(editor.Path, path))
				{
					editor.ShowLine(line, col, tabWidth);
				}
			}
		}
		#endregion
	
		#region Fields
		private Boss m_boss;
		#endregion
	}
}