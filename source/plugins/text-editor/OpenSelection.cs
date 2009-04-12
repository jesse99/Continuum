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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TextEditor
{
	// Here are some test cases which should work:
	// source/plugins/find/AssemblyInfo.cs															relative path
	// DatabaseTest.cs																						local file
	// <AppKit/NSResponder.h>																		non-local relative path
	// /Users/jessejones/Source/Continuum/source/plugins/find/AssemblyInfo.cs		absolute path
	// http://dev.mysql.com/tech-resources/articles/why-data-modeling.html			url
	// NSWindow.h																							file in preferred directory																
	// C#.cs																										file not in preferred directory
	internal sealed class OpenSelection : IOpenSelection
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public bool Open(string text)
		{
			bool opened = false;
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying to open '{0}'", text);
			
			if (!opened)
				opened = DoOpenURL(text, 0, text.Length);
				
			if (!opened)
				opened = DoOpenFile(text, 0, text.Length);
				
			if (!opened)
				Log.WriteLine("Open Selection", "open failed");
			
			return opened;
		}
		
		public bool Open(string text, ref int location, ref int length)
		{
			bool opened = false;
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying to open a selection");
			
			if (!opened)
			{
				int loc = location;
				int len = length;
				DoExtendSelection(this.DoHtmlTest, text, ref loc, ref len);
				opened = DoOpenURL(text, loc, len);
			}
			
			if (!opened)
			{
				int loc = location;
				int len = length;
				DoExtendSelection(this.DoFileTest, text, ref loc, ref len);
				opened = DoOpenFile(text, loc, len);
			}
			
			return opened;
		}
		
		#region Private Methods
		private bool DoOpenURL(string text, int loc, int len)
		{
			string name = text.Substring(loc, len);
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying url '{0}'", name);
			return 	DoOpenURL(name);
		}
		
		private bool DoOpenFile(string text, int loc, int len)
		{
			string name = text.Substring(loc, len);
			int line = -1, col = -1;
			DoGetLineAndCol(text, loc, len, ref line, ref col);
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying path '{0}'", name);
			
			bool found = DoOpenAbsolutePath(name, line, col);
			
			if (!found)
			{
				name = System.IO.Path.GetFileName(name);
				
				Boss boss = ObjectModel.Create("FileSystem");
				var fs = boss.Get<IFileSystem>();
				string[] candidates = fs.LocatePath("/" + name);
				
				if (candidates.Length > 0)
					found = DoOpenLocalPath(candidates, name, line, col) || DoOpenGlobalPath(candidates, name, line, col);
				else
					Log.WriteLine(TraceLevel.Verbose, "Open Selection", "open using locate failed (no candidates)");
			}
			
			return found;
		}
		
		private void DoGetLineAndCol(string text, int loc, int len, ref int line, ref int col)
		{
			int l = -1, c = -1;
			
			// gmcs - Application.cs(14,10)
			Scanner scanner = new Scanner(text, loc + len);
			if (scanner.Scan('(') && scanner.Scan(ref l) && scanner.Scan(',') && scanner.Scan(ref c) && scanner.Scan(')'))
			{
				line = l;
				col = c;
			}
			
			// make - Makefile:28
			scanner.Reset();
			if (scanner.Scan(':') && scanner.Scan(ref l))
			{
				line = l;
			}
			
			// gendarme - Application.cs(~10)
			scanner.Reset();
			if (scanner.Scan('(') && scanner.Scan('\u2248') && scanner.Scan(ref l) && scanner.Scan(')'))
			{
				line = l;
			}
			
			// gendarme - Application.cs(?10)		TODO: not sure why we get this sometimes from gendarme...
			scanner.Reset();
			if (scanner.Scan('(') && scanner.Scan('?') && scanner.Scan(ref l) && scanner.Scan(')'))
			{
				line = l;
			}
		}
		
		// If the name looks like an URL we'll launch it with a browser.
		private bool DoOpenURL(string name)
		{
			bool opened = false;
			
			try
			{
				NSURL url = NSURL.URLWithString(NSString.Create(name));
				if (!NSObject.IsNullOrNil(url))		// URLWithString returns nil on failures...
				{
					Log.WriteLine(TraceLevel.Verbose, "Open Selection", "it seems to be an url");
					if (url.scheme().length() > 0)
					{
						Log.WriteLine(TraceLevel.Verbose, "Open Selection", "and the scheme is {0:D}", url.scheme());
						Unused.Value = NSWorkspace.sharedWorkspace().openURL(url);	
						opened = true;
					}
				}
			}
			catch (Exception)
			{
			}
			
			return opened;
		}
		
		// If the name is an absolute path then we can open it with Continuum if it's
		// a file type we handle or launch it with an external app if not.
		private bool DoOpenAbsolutePath(string name, int line, int col)
		{
			bool opened = false;
			
			try
			{
				if (System.IO.Path.IsPathRooted(name) && (System.IO.File.Exists(name) || NSWorkspace.sharedWorkspace().isFilePackageAtPath(NSString.Create(name))))
				{
					Boss boss = ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(name, line, col, 1);
					opened = true;
				}
			}
			catch
			{
			}
			
			return opened;
		}
		
		// If the name is not an absolute path then we'll see if it corresponds to a 
		// path rooted at one of the directories we have open.
		private bool DoOpenLocalPath(string[] candidates, string name, int line, int col)
		{
			bool opened = false;
			
			try
			{
				if (!System.IO.Path.IsPathRooted(name))
				{
					// Find the paths for the directories the user has open.
					var localPaths = new List<string>();
					Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
					var windows = boss.Get<IWindows>();
					foreach (Boss b in windows.All())
					{
						var editor = b.Get<IDirectoryEditor>();
						localPaths.Add(editor.Path);
					}
					
					// Find the paths in candidates which are under a local directory.
					var paths = new List<string>();
					foreach (string candidate in candidates)
					{
						if (localPaths.Any(p => candidate.StartsWith(p)))
							paths.Add(candidate);
					}
					
					// Open all of the paths we found.
					if (paths.Count > 0 && paths.Count < 10)		// TODO: use a picker if there is more than one file
					{
						boss = ObjectModel.Create("Application");
						var launcher = boss.Get<ILaunch>();
					
						foreach (string p in paths)
						{
							launcher.Launch(p, line, col, 1);
							opened = true;
						}
						
						Log.WriteLine(TraceLevel.Verbose, "Open Selection", "opened {0} local files", paths.Count);
					}
					else if (paths.Count > 0)
						Log.WriteLine("Open Selection", "open local failed ({0} is too many paths)", paths.Count);
				}
			}
			catch
			{
			}
			
			return opened;
		}
		
		private string[] DoGetPreferredPaths()
		{
			var defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.arrayForKey(NSString.Create("preferred paths"));
			
			var result = new string[data.count() + 1];
			result[0] = defaults.objectForKey(NSString.Create("mono_root")).To<NSString>().description();
			
			int i = 0;
			foreach (NSString s in data)
			{
				result[1 + i++] = s.description();
			}
			
			return result;
		}
		
		// This is our last try. We'll use the locate command to see if we can find the file.
		private bool DoOpenGlobalPath(string[] candidates, string name, int line, int col)
		{
			bool opened = false;
			
			try
			{
				Boss boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				Log.WriteLine(TraceLevel.Verbose, "Open Selection", "global open found {0} candidates", candidates.Length);
				
				// If there's only one path then just open it.
				if (candidates.Length == 1)
				{
					launcher.Launch(candidates[0], line, col, 1);
					opened = true;
				}
				else
				{
					List<string> paths = new List<string>();
					
					// Otherwise we'll prefer files within the preferred directories.
					string[] preferred = DoGetPreferredPaths();
					foreach (string path in candidates)	
					{
						foreach (string sp in preferred)
						{
							if (path.StartsWith(sp) && System.IO.Path.GetFileName(path) == name)
							{
								paths.Add(path);
								break;
							}
						}
					}
					
					// If we couldn't find a path within a preferred directory
					// then use everything we did find.
					if (paths.Count == 0)
						paths.AddRange(candidates);
						
					if (paths.Count > 0 && paths.Count < 10)		// TODO: use a picker if there is more than one file
					{
						foreach (string p in paths)
						{
							launcher.Launch(p, line, col, 1);
							opened = true;
						}
						
						Log.WriteLine(TraceLevel.Verbose, "Open Selection", "global opened {0} files", paths.Count);
					}
					else if (paths.Count > 0)
						Log.WriteLine("Open Selection", "global open failed ({0} is too many candidates)", paths.Count);
				}
			}
			catch
			{
			}
			
			return opened;
		}
		
		private delegate bool Tester(string text, int index);
		void DoExtendSelection(Tester tester, string text, ref int location, ref int length)
		{
			while (tester(text, location - 1))
			{
				--location;
				++length;
			}
			
			while (tester(text, location + length))
				++length;
		}
		
		private bool DoHtmlTest(string text, int index) 
		{
			if (index >= 0 && index < text.Length)
			{
				if (char.IsLetterOrDigit(text[index]))
					return true;
					
				string valid = ":?#/+-.@%_~!$&'()*,;=";	// see <http://tools.ietf.org/html/rfc3986#appendix-A>			
				if (valid.IndexOf(text[index]) >= 0)			// note that we don't allow [] because they are used in wiki formatting
					return true;
			}
			
			return false;
		}
		
		private bool DoFileTest(string text, int index) 
		{
			if (index >= 0 && index < text.Length)
			{
				if (text[index] == '\n' ||
					text[index] == '\r' ||
					text[index] == '\t' ||
					text[index] == ' ' ||
					text[index] == '<' ||
					text[index] == '>' ||
					text[index] == ':' ||
					text[index] == '\'' ||
					text[index] == '"' ||
					text[index] == '(')
					return false;
				
				return true;
			}
			
			return false;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
