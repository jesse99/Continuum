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

using Mono.Unix;
using System;
using System.Diagnostics;
using System.IO;

namespace Shared
{
	public static class Paths
	{
		// This returns the path to the directory that contain's Continuum's
		// database and user script files.
		public static string SupportPath
		{
			get
			{
				if (ms_supportPath == null)
				{
					// TODO: use /Library/Application Support/Continuum instead
					string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					ms_supportPath = Path.Combine(path, "Continuum Files");
					if (!Directory.Exists(ms_supportPath))
						Directory.CreateDirectory(ms_supportPath);
				}
				
				return ms_supportPath;
			}
		}
		
		public static string GetAssemblyDatabase(string name)
		{
			string path = Path.Combine(SupportPath, "databases");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			
			path = Path.Combine(path, name + ".db");
			
			return path;
		}
		
		// Compares paths and handles things like extra slashes and references
		// to "." or "..".
		public static bool AreEqual(string lhs, string rhs)
		{
			Trace.Assert(lhs != null, "lhs is null");
			Trace.Assert(rhs != null, "rhs is null");
			
			lhs = UnixPath.GetCanonicalPath(lhs);
			rhs = UnixPath.GetCanonicalPath(rhs);
			
			return lhs == rhs;
		}
		
		private static string ms_supportPath;
	}
}
