// Copyright (C) 2011 Jesse Jones
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
using System;
using System.IO;

namespace DefaultBuilder
{
	internal static class Helpers
	{
		public static void WriteFile(string path, string contents)
		{
			if (!File.Exists(path))
			{
				File.WriteAllText(path, contents);
			}
			else
			{
				string current = File.ReadAllText(path);
				if (current != contents)
					File.WriteAllText(path, contents);
			}
		}
		
		public static string GetFiles(string dir, string[] globs)
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (string glob in globs)
			{
				foreach (string path in Directory.GetFiles(dir, glob, SearchOption.AllDirectories))
				{
					builder.Append(Helpers.GetRelativePath(dir, path));
					builder.Append(' ');
				}
			}
			
			return builder.ToString();
		}
		
		public static string GetRelativePath(string dir, string path)
		{
			Contract.Requires(!path.EndsWith("/"));
			
			if (path.StartsWith(dir + "/"))
				path = path.Substring(dir.Length + 1);
			
			if (path.Contains(" "))
				path = string.Format("'{0}'", path);
				
			return path;
		}
	}
}
