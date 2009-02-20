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
using Shared;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace App
{
	internal sealed class GmcsParser : IParseErrors
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public void Parse(string text, List<BuildError> errors)
		{			
			foreach (Match match in ms_re.Matches(text))
			{
				if (match.Success)
				{
					int line = int.Parse(match.Groups[2].ToString());
					int col = int.Parse(match.Groups[3].ToString());
					
					// Despite being one-based gmcs sometimes returns a zero col...
					col = Math.Max(col, 1);
					
					errors.Add(new BuildError(
						"gmcs",
						match.Groups[1].ToString(), 					// file
						match.Groups[5].ToString(), 					// message
						line, 														// line
						col,														// col
						match.Groups[4].ToString() == "error"));	// isError
				}
			}
		}
		
		#region Fields 
		private Boss m_boss; 
		//                                                                1                2       3            4                           5
		//                                                                TheFile.cs   (5     , 19  ) :     error              CS0234 : The type...
		private static Regex ms_re = new Regex(@"([\w\.\-\\\/]+)\((\d+) , (\d+)\) : \s+ (error|warning) \s+ \w+    : (.+)$", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
		#endregion
	} 
}