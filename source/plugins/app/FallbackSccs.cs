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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace App
{
	[ExportClass("CopyHandler", "NSObject")]
	internal sealed class CopyHandler : NSObject
	{
		public CopyHandler() : base(NSObject.AllocNative("CopyHandler"))
		{
			ActiveObjects.Add(this);
		}
		
		public bool fileManager_shouldCopyItemAtPath_toPath(NSFileManager manager, NSString fromPath, NSString toPath)
		{
			NSRange range = fromPath.rangeOfString(ms_svn);
			
			return range.location == Enums.NSNotFound;
		}
		
		private static readonly NSString ms_svn = NSString.Create("/.svn").Retain();
	}	
	
	internal sealed class FallbackSccs : ISccs
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
			
			// Note that if this is changed GetCommands must be updated as well.
			m_commands.Add(Name + " rename", this.DoRename);
			m_commands.Add(Name + " duplicate", this.DoDuplicate);
			m_commands.Add(Name + " create directory", this.DoCreateDir);
			m_commands.Add(Name + " create file", this.DoCreateFile);
			m_commands.Add(Name + " move to trash", this.DoTrash);
			m_commands.Add(Name + " open with Finder", this.DoFinderOpen);
			m_commands.Add(Name + " touch", this.DoTouch);
			m_commands.Add(Name + " show in Finder", this.DoShow);
			m_commands.Add(Name + " info", this.DoInfo);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string Name
		{
			get {return "File";}
		}
		
		public bool Rename(string oldPath, string newPath)
		{
			File.Move(oldPath, newPath);		// note that this also works for directories
			
			return true;
		}
		
		public bool Duplicate(string path)
		{
			DoDuplicate(path);
			
			return true;
		}
		
		public string[] GetCommands()
		{
			return m_commands.Keys.ToArray();
		}
		
		public string[] GetCommands(IEnumerable<string> paths)
		{
			Contract.Requires(paths.Any(), "paths is empty");
			
			var commands = new List<string>(m_commands.Count);
			
			int count = paths.Count();
			if (count == 1)
				commands.Add(Name + " rename");
			
			if (paths.All(p => File.Exists(p)))
			{
				commands.Add(Name + " touch");
			}
			
			if (count == 1)
			{
				commands.Add(Name + " create directory");
				commands.Add(Name + " create file");
			}
			
			commands.Add(Name + " duplicate");
			commands.Add(Name + " move to trash");
			commands.Add(Name + " open with Finder");
			commands.Add(Name + " show in Finder");
			commands.Add(Name + " info");
			
			return commands.ToArray();
		}
		
		public void Execute(string command, string path)
		{
			Contract.Requires(m_commands.ContainsKey(command), command + " is not a File command");
			
			m_commands[command](path);
		}
		
		#region Private Methods
		// Note that NSWorkspaceDuplicateOperation provides a simpler way to do
		// this but it doesn't work as well. For example, "foo copy 2" becomes 
		// "foo copy 2 copy".
		private string DoGetCopyName(string oldName, int count)
		{
			string newName = oldName;
			
			if (count == 1)
			{
				if (!oldName.EndsWith(" copy") && !oldName.Contains(" copy "))
					newName = oldName + " copy";
			}
			else
			{
				if (m_copyRE == null)
					m_copyRE = new Regex(@" copy \d+$");
				
				if (m_copyRE.IsMatch(oldName))
					newName = m_copyRE.Replace(oldName, " copy " + count);	// "foo copy 4"
				
				else if (oldName.EndsWith(" copy"))		
					newName = oldName + " " + count;										// "foo copy"		
				
				else
					newName = oldName + " copy " + count;								// "foo"
			}
			
			return newName;
		}
		
		private string DoGetCopyPath(string dir, string oldName)
		{
			for (int i = 1; i < 100; ++i)
			{
				string name = Path.GetFileNameWithoutExtension(oldName);
				string newName = DoGetCopyName(name, i);
				
				string extension = Path.GetExtension(oldName);
				newName += extension;
				
				string newPath = Path.Combine(dir, newName);
				if (!File.Exists(newPath))
					return newPath;
			}
			
			throw new InvalidOperationException("Couldn't find a new name to use.");
		}
		
		private void DoDuplicate(string path)		// TODO: should move this (and maybe some others here) into IFileSystem
		{
			// Get the new path.
			string oldDir = Path.GetDirectoryName(path);
			string oldName = Path.GetFileName(path);
			string newPath = DoGetCopyPath(oldDir, oldName);
			
			// Copy the directory, but don't copy .svn directories.
			var handler = new CopyHandler();
			handler.autorelease();
			
			NSError error;
			NSFileManager.defaultManager().setDelegate(handler);		// this is not thread safe, but NSFileManager is documented as not being thread safe anyway
			NSFileManager.defaultManager().copyItemAtPath_toPath_error(
				NSString.Create(path), NSString.Create(newPath), out error);
			NSFileManager.defaultManager().setDelegate(null);
			
			if (!NSObject.IsNullOrNil(error))
				error.Raise();
				
			// Unfortunately while copyItemAtPath_toPath_error won't copy the contents
			// of the .svn directory it will make an .svn directory in the target so we have 
			// to manually delete it.
			string svn = Path.Combine(newPath, ".svn");
			if (Directory.Exists(svn))
				Directory.Delete(svn);
		}
		
		private void DoCreateDir(string path)
		{
			var get = new GetString{Title = "Directory Name", Label = "Name:"};
			string name = get.Run();
			if (name != null)
			{
				string oldDir = Path.GetDirectoryName(path);
				Unused.Value = Directory.CreateDirectory(Path.Combine(oldDir, name));
			}
		}
		
		private void DoCreateFile(string path)
		{
			var get = new GetString{Title = "File Name", Label = "Name:"};
			string name = get.Run();
			if (name != null)
			{
				string oldDir = Path.GetDirectoryName(path);
				using (FileStream stream = File.Create(Path.Combine(oldDir, name)))
				{
					stream.Dispose();
				}
			}
		}
		
		private void DoInfo(string path)
		{
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			
			transcript.Show();
			transcript.WriteLine(Output.Command, "File info");
			transcript.WriteLine(Output.Normal, "Path: {0}", path);
			
			NSError error;
			NSDictionary attrs = NSFileManager.defaultManager().attributesOfItemAtPath_error(
				NSString.Create(path), out error);
			if (NSObject.IsNullOrNil(error))
			{
				foreach (var entry in attrs)
				{
					transcript.WriteLine(Output.Normal, "{0:D}: {1:D}", entry.Key, entry.Value);
				}
			}
			else
			{
				transcript.WriteLine(Output.Error, " {0:D}", error.localizedDescription());
			}
			
			transcript.WriteLine(Output.Normal, string.Empty);
		}
		
		private void DoRename(string path)
		{
			string oldName = Path.GetFileName(path);
			
			var get = new GetString{Title = "New Name", Label = "Name:", Text = oldName};
			string newName = get.Run();
			if (newName != null && newName != oldName)
			{
				string oldDir = Path.GetDirectoryName(path);
				Rename(path, Path.Combine(oldDir, newName));
			}
		}
		
		private void DoFinderOpen(string path)
		{
			NSWorkspace.sharedWorkspace().openFile(NSString.Create(path));
		}
		
		private void DoShow(string path)
		{
			NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
				NSString.Create(path), NSString.Empty);
		}
		
		private void DoTouch(string path)
		{
			File.SetLastWriteTime(path, DateTime.Now);
		}
		
		private void DoTrash(string path)
		{
			NSString source = NSString.Create(Path.GetDirectoryName(path));
			NSArray files = NSArray.Create(Path.GetFileName(path));
			
			NSString home = Functions.NSHomeDirectory();
			NSString dest = home.stringByAppendingPathComponent(NSString.Create(".Trash"));
			int tag;
			Unused.Value = NSWorkspace.sharedWorkspace().performFileOperation_source_destination_files_tag(
				Externs.NSWorkspaceRecycleOperation, source, dest, files, out tag);
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private Regex m_copyRE;
		private Dictionary<string, Action<string>> m_commands = new Dictionary<string, Action<string>>();
		#endregion
	}
}
