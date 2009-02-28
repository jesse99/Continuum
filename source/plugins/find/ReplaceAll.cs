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
using Shared;
using System;
using System.Text.RegularExpressions;

namespace Find
{
	internal sealed class ReplaceAll : BaseFindInFiles
	{
		public ReplaceAll(string directory, Regex re, string replacement, string[] include, string[] exclude)
			: base(directory, include, exclude)
		{	
			m_regex = re;
			m_replacement = replacement;
		}
		
		#region Protected Methods
		protected override NSFileHandle OnOpenFile(string path)	// threaded
		{
			return NSFileHandle.fileHandleForUpdatingAtPath(NSString.Create(path));
		}
		
		protected override string OnProcessFile(string file, string text)	// threaded
		{
			return m_regex.Replace(text, m_replacement);
		}
		#endregion
		
		#region Fields
		private Regex m_regex;
		private string m_replacement;
		#endregion
	}
}
