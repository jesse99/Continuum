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

using Gear;
using MCocoa;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Styler
{
	internal sealed class RegexDeclarations : IDeclarations
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Declaration[] Get(string text, StyleRun[] runs, CsGlobalNamespace globals)
		{
			Trace.Assert(runs != null, "runs is null");
			
			var decs = new List<Declaration>();
			string indent = string.Empty;
			foreach (StyleRun run in runs)
			{
				if (run.Type == StyleType.Type)
				{
					decs.Add(new Declaration(
						text.Substring(run.Offset, run.Length),	
						new NSRange(run.Offset, run.Length),
						true, false));
					indent = "    ";
				}
				else if (run.Type == StyleType.Member)
				{
					decs.Add(new Declaration(
						indent + text.Substring(run.Offset, run.Length),
						new NSRange(run.Offset, run.Length),
						false, false));
				}
			}
			
			return decs.ToArray();
		}
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
