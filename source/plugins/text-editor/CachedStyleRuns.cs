// Copyright (C) 2008-2009 Jesse Jones
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
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class CachedStyleRuns : ICachedStyleRuns
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(out int editCount, out StyleRun[] runs)
		{
			lock (m_mutex)
			{
				editCount = m_editCount;
				runs = m_runs;
			}
		}
		
		public void Reset(int edit, StyleRun[] runs)
		{
			Contract.Requires(runs != null, "runs is null");
			
			// .NET guarantees that these fields are atomically set but we need
			// to ensure that the entire group is set atomically.
			lock (m_mutex)
			{
				m_editCount = edit;
				m_runs = runs;
			}
		}
		
		#region Fields
		private Boss m_boss;
		private object m_mutex = new object();
			private int m_editCount = int.MinValue;
			private StyleRun[] m_runs = new StyleRun[0];
		#endregion
	}
}
