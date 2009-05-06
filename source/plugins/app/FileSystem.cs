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

using MCocoa;
using Gear;
using Gear.Helpers;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace App
{
	internal sealed class FileSystem : IFileSystem
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Launch(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			Unused.Value = NSWorkspace.sharedWorkspace().openFile(NSString.Create(path));
		}
		
		// Returns the size of a file or the files within a directory.
		// Note that directories and files that start with a '.' are
		// ignored.
		public long GetBytes(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			long bytes = 0;
			
			if (Directory.Exists(path))
				bytes += DoGetDirBytes(path);
			else
				bytes += DoGetFileBytes(path);
			
			return bytes;
		}
	
		public string[] LocatePath(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			string result = string.Empty;
			
			try
			{
				using (Process process = new Process())
				{
					process.StartInfo.FileName = "locate";
					process.StartInfo.Arguments = "-i '" + path + "'";
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					
					process.Start();
					
					result = process.StandardOutput.ReadToEnd();
					process.WaitForExit();
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "couldn't locate '{0}'", path);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
			
			var paths = new List<string>(result.Split(new char[]{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries));
			Unused.Value = paths.RemoveAll(p => p.Contains("/.svn/") || p.Contains("/.Trashes/"));
			
			return paths.ToArray();
		}
		
		public string GetTempFile(string prefix, string extension)
		{
			Contract.Requires(!string.IsNullOrEmpty(prefix), "prefix is null or empty");
			Contract.Requires(extension != null, "extension is null");
			
			prefix = prefix.Replace('/', '.');		// prefix is often a type name which causes problems if it is a nested type unless we do this
			
			string dir = Path.GetTempPath();
			string path = Path.Combine(dir, prefix) + extension;
			if (!File.Exists(path))
				return path;
				
			for (int i = 2; i < 1000; ++i)
			{
				path = Path.Combine(dir, prefix) + " #" + i + extension;
				if (!File.Exists(path))
					return path;
			}
			
			throw new InvalidOperationException("Couldn't find a temp file for " + prefix);
		}
		
		// TODO: remove this once Directory.GetFiles is fixed
		public string[] GetAllFiles(string path, string glob)
		{
			var files = new List<string>();
			DoGetAllFiles(files, path, glob);
			
			return files.ToArray();
		}
		
		#region Private Methods 
		private static void DoGetAllFiles(List<string> files, string path, string glob)
		{
			try
			{
				files.AddRange(Directory.GetFiles(path, glob));
				
				string[] dirs = Directory.GetDirectories(path);
				foreach (string dir in dirs)
				{
					DoGetAllFiles(files, dir, glob);
				}
			}
			catch (IOException)
			{
				// If the file system is changing via another process we may land here.
			}
		}
		
		private static long DoGetDirBytes(string path)
		{
			long bytes = 0;
			
			try
			{
				foreach (string p in Directory.GetFileSystemEntries(path))
				{
					if (Path.GetFileName(p)[0] != '.')
					{
						if (Directory.Exists(p))
							bytes += DoGetDirBytes(p);
						else
							bytes += DoGetFileBytes(p);
					}
				}
			}
			catch (IOException)
			{
				// If the file system is changing via another process we may land here.
			}
			
			return bytes;
		}
		
		private static long DoGetFileBytes(string path)
		{
			long bytes = 0;
			
			NSError error;
			NSDictionary attrs = NSFileManager.defaultManager().attributesOfItemAtPath_error(NSString.Create(path), out error);
			if (NSObject.IsNullOrNil(error))
			{
				NSObject value = attrs.objectForKey(Externs.NSFileSize);
				bytes = (long) value.Call("longLongValue");
			}
			
			return bytes;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
