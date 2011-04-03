// Copyright (C) 2007-2011 Jesse Jones
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
using System.Linq;
using System.Text.RegularExpressions;

namespace TextEditor
{
	// Here are some test cases which should work:
	// source/plugins/find/Find.cs																relative path (this should work as well for files not in the locate db)
	// DatabaseTest.cs																			local file
	// <AppKit/NSResponder.h>																non-local relative path
	// /Users/jessejones/Source/Continuum/source/plugins/find/AssemblyInfo.cs		absolute path
	// http://dev.mysql.com/tech-resources/articles/why-data-modeling.html			url
	// http://developer.apple.com/library/mac/#documentation/MacOSX/Conceptual/OSX_Technology_Overview/About/About.html relative url
	// NSWindow.h																				file in preferred directory																
	// c#.cs																						file not in preferred directory
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
		
		public void Open()
		{
			string text = new GetString{Title = "Open Selection", ValidRegex = m_validator}.Run();
			if (text != null)
				text = text.Trim();
			
			if (!string.IsNullOrEmpty(text))
				if (!Open(text))
					Functions.NSBeep();
		}
		
		public bool Open(string text)
		{
			bool opened = false;
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying to open '{0}'", text);
			
			if (!opened)
				opened = DoOpenFile(text, 0, text.Length);
				
			if (!opened)
				opened = DoOpenURL(text, 0, text.Length);
				
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
				DoExtendSelection(this.DoFileTest, text, ref loc, ref len);
				opened = DoOpenFile(text, loc, len);
			}
			
			if (!opened)
			{
				int loc = location;
				int len = length;
				DoExtendSelection(this.DoHtmlTest, text, ref loc, ref len);
				opened = DoOpenURL(text, loc, len);
			}
			
			return opened;
		}
		
		public bool IsValid(string text)
		{
			return m_validator.IsMatch(text) && !text.Contains('\n');
		}
		
		#region Private Methods
		private bool DoOpenURL(string text, int loc, int len)
		{
			string name = text.Substring(loc, len);
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying url '{0}'", name);
			return DoOpenURL(name);
		}
		
		private bool DoOpenFile(string text, int loc, int len)
		{
			string path = text.Substring(loc, len);
			int line = -1, col = -1;
			DoGetLineAndCol(text, loc, len, ref line, ref col);
			Log.WriteLine(TraceLevel.Verbose, "Open Selection", "trying path '{0}'", path);
			
			bool found = false;
			
			// We don't want to try paths like "//code.google.com/p/mobjc/w/list" because
			// we'll find "/Developer/SDKs/MacOSX10.5.sdk/usr/include/c++/4.0.0/list".
			if (!path.StartsWith("//"))
			{
				if (!found)
					found = DoOpenAbsolutePath(path, line, col);
				
				if (!found)
					found = DoOpenRelativePath(path, line, col);
				
				if (!found)
				{
					string name = System.IO.Path.GetFileName(path);
					
					Boss boss = ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					var candidates = new List<string>(fs.LocatePath("/" + name));
					
					// This isn't as tight a test as we would like, but I don't think we can
					// do much better because of links. For example, <AppKit/NSResponder.h>
					// maps to a path like:
					//  "/System/Library/Frameworks/AppKit.framework/Versions/C/Headers/NSResponder.h".
					candidates.RemoveAll(c => System.IO.Path.GetFileName(path) != name);
					
					if (candidates.Count > 0)
						found = DoOpenLocalPath(candidates.ToArray(), name, line, col) || DoOpenGlobalPath(candidates.ToArray(), name, line, col);
					else
						Log.WriteLine(TraceLevel.Verbose, "Open Selection", "open using locate failed (no candidates)");
				}
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
		
		// If the name looks like an URL we'll launch it with a browser. Note that
		// this can return true even if an URL isn't actually opened.
		private bool DoOpenURL(string name)
		{
			bool opened = false;
			
			try
			{
				NSURL url = DoGetURL(name);
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
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Verbose, "Open Selection", "DoOpenURL failed with: {0}", e.Message);
			}
			
			return opened;
		}
		
		// NSURL.URLWithString doesn't handle relative URLs so if there are hash-marks we
		// have to build the relative URL ourself.
		private NSURL DoGetURL(string path)
		{
			string[] parts = path.Split('#');
			NSURL url = NSURL.URLWithString(NSString.Create(parts[0]));
			
			for (int i = 1; i < parts.Length; ++i)
			{
				url = NSURL.URLWithString_relativeToURL(NSString.Create(parts[i]), url);
			}
			
			return url;
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
		
		// TODO: The operation of open selection is a little weird: when searching for
		// local files we use the full file name, but when using the locate command
		// we allow partial file name matches.
		private bool DoOpenRelativePath(string name, int line, int col)
		{
			bool opened = false;
			
			try
			{
				if (!System.IO.Path.IsPathRooted(name))
				{
					// See if a file exists at local path + name.
					Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
					var windows = boss.Get<IWindows>();
					
					var paths = new List<string>();
					foreach (Boss b in windows.All())
					{
						var editor = b.Get<IDirectoryEditor>();
						string candidate = System.IO.Path.Combine(editor.Path, name);
						if (System.IO.File.Exists(candidate))
							paths.Add(candidate);
					}
					
					// Open all of the paths we found.
					opened = DoOpenPath(paths, line, col);
				}
			}
			catch
			{
			}
			
			return opened;
		}
		
		// If the name is not an absolute path then we'll see if it corresponds to a 
		// path rooted at one of the directories we have open. (This won't be used
		// for a relative path rooted at one of the directories we have open, but it
		// will be used if we have a relative path or file name rooted at a directory
		// under one of our open directories).
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
						{
							if (System.IO.File.Exists(candidate))
							{
								paths.Add(candidate);
							}
						}
					}
					
					// Open all of the paths we found.
					opened = DoOpenPath(paths, line, col);
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
							if (path.StartsWith(sp) && System.IO.Path.GetFileName(path).ToLower() == name.ToLower())
							{
								if (System.IO.File.Exists(path))
								{
									Log.WriteLine(TraceLevel.Verbose, "Open Selection", "found preferred '{0}'", path);
									paths.Add(path);
									break;
								}
							}
						}
					}
					
					// If we couldn't find a path within a preferred directory
					// then use everything we did find.
					if (paths.Count == 0)
						paths.AddRange(candidates);
						
					opened = DoOpenPath(paths, line, col);
				}
			}
			catch
			{
			}
			
			return opened;
		}
		
		private bool DoOpenPath(List<string> candidates, int line, int col)
		{
			Contract.Requires(candidates.Count > 0);
			
			var paths = new List<string>(candidates);
			if (paths.Count > 2)
			{
				paths.Sort();
				
				var getter = new GetItem<string>{Title = "Choose Files", Items = paths.ToArray(), AllowsMultiple = true};
				paths = new List<string>(getter.Run(i => i));
				
				// If the user canceled then we don't want to proceed so we'll say we opened it.
				if (paths.Count == 0)
					return true;
			}
			
			bool opened = false;
			if (paths.Count > 0)
			{
				Boss boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				
				var bad = new List<string>();
				foreach (string p in paths)
				{
					try
					{
						launcher.Launch(p, line, col, 1);
						opened = true;
					}
					catch
					{
						bad.Add(p);
					}
				}
				
				if (bad.Count > 0)
				{
					NSString title = NSString.Create("Couldn't open.");
					NSString message = NSString.Create(string.Join("\n", bad.ToArray()));
					Unused.Value = Functions.NSRunAlertPanel(title, message);
				}
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
		private Regex m_validator = new Regex(@"\S+");
		#endregion
	}
}
