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

using Gear.Helpers;
using Mono.Unix;
using System;
using System.Diagnostics;
using System.IO;

namespace Shared
{
	public static class Paths
	{
		public static string ScriptsPath
		{
			get
			{
				if (ms_scriptsPath == null)
				{
					ms_scriptsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					ms_scriptsPath = Path.Combine(ms_scriptsPath, "Library");
					ms_scriptsPath = Path.Combine(ms_scriptsPath, "Application Support");
					ms_scriptsPath = Path.Combine(ms_scriptsPath, "Continuum");
					
					if (!Directory.Exists(ms_scriptsPath))
						Directory.CreateDirectory(ms_scriptsPath);
				}
				
				return ms_scriptsPath;
			}
		}
		
		public static string DatabasesPath
		{
			get
			{
				if (ms_dbPath == null)
				{
					// Note that Time Machine will exclude the contents of ~/Library/Caches (see
					// /System/Library/CoreServices/backupd.bundle/Contents/Resources/StdExclusions.plist).
					ms_dbPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					ms_dbPath = Path.Combine(ms_dbPath, "Library");
					ms_dbPath = Path.Combine(ms_dbPath, "Caches");
					ms_dbPath = Path.Combine(ms_dbPath, "Continuum");
					
					if (!Directory.Exists(ms_dbPath))
						Directory.CreateDirectory(ms_dbPath);
				}
				
				return ms_dbPath;
			}
		}
		
		public static string GetAssemblyDatabase(string name)
		{
			return Path.Combine(DatabasesPath, name + ".db");
		}
		
		// Compares paths and handles things like extra slashes and references
		// to "." or "..".
		[Pure]
		public static bool AreEqual(string lhs, string rhs)
		{
			Contract.Requires(lhs != null, "lhs is null");
			Contract.Requires(rhs != null, "rhs is null");
			
			lhs = UnixPath.GetCanonicalPath(lhs);
			rhs = UnixPath.GetCanonicalPath(rhs);
			
			return lhs == rhs;
		}
		
		private static string ms_scriptsPath;
		private static string ms_dbPath;
	}
}
