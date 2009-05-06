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

using Gear.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared
{
	[Flags]
	[Serializable]
	public enum GlobFlags : uint
	{
		None = 0,
		FNM_NOESCAPE = 0x01,			/* Disable backslash escaping. */
		FNM_PATHNAME = 0x02,			/* Slash must be matched by slash. */
		FNM_PERIOD = 0x04,				/* Period must be matched by period. */
		FNM_LEADING_DIR = 0x08,	/* Ignore /<tail> after Imatch. */
		FNM_CASEFOLD = 0x10			/* Case insensitive search. */
	}
	
	public static class Glob
	{
		public static bool Match(string glob, string name)
		{
			Contract.Requires(glob != null, "glob is null");
			Contract.Requires(name != null, "name is null");
			
			GlobFlags flags = GlobFlags.FNM_PATHNAME | GlobFlags.FNM_PERIOD | GlobFlags.FNM_CASEFOLD;
			int result = fnmatch(glob, name, flags);
			
			return result == 0;
		}
		
		// Splits a list of globs separated by spaces with optional escaping of embeded spaces.
		public static string[] Split(string inGlobs)
		{
			Contract.Requires(inGlobs != null, "inGlobs is null");
			
			string globs = inGlobs.Replace("\\ ", Constants.Replacement);
			
			string[] result = globs.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
			if (globs.Length != inGlobs.Length)
			{
				for (int i = 0; i < result.Length; ++i)
				{
					result[i] = result[i].Replace(Constants.Replacement, " ");
				}
			}
			
			return result;
		}
		
		// Returns the globs as a space separated list with embedded spaces escaped.
		public static string Join(string[] globs)
		{
			Contract.Requires(globs != null, "globs is null");
			
			var result = new StringBuilder(3*globs.Length);
			
			for (int i = 0; i < globs.Length; ++i)
			{
				result.Append(globs[i].Replace(" ", "\\ "));
				
				if (i + 1 < globs.Length)
					result.Append(' ');
			}
			
			return result.ToString();
		}
		
		[DllImport("libc")]
		private static extern int fnmatch(
			string    pattern,
			string    name,
			GlobFlags flags);
	}
}
