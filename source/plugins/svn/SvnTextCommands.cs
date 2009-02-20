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
//using MCocoa;
//using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Subversion
{
	internal sealed class SvnTextCommands : ITextContextCommands
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
				
		public void Get(Boss boss, string selection, List<TextContextItem> items)
		{
			if (selection == null)
			{
				string path = DoGetPath();
				if (path != null)
				{
					Svn svn = DoGetSvn();
					string[] commands = svn.GetCommands(new string[]{path});
					
					bool addedSep = false;
					foreach (string command in commands)
					{
						if (m_candidates.Contains(command))
						{
							if (!addedSep)
							{
								items.Add(new TextContextItem(0.8f));
								addedSep = true;
							}

							string c = command;
							items.Add(new TextContextItem(command, s => {svn.Execute(c, path); return s;}, 0.8f));
						}
					}
				}
			}
		}
				
		#region Private Methods
		private string DoGetPath()
		{
			string path = null;
			
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();

			if (boss != null)
			{
				var editor = boss.Get<ITextEditor>();
				path = editor.Path;
			}

			return path;
		}

		private Svn DoGetSvn()
		{			
			Boss boss = ObjectModel.Create("Sccs");
			foreach (object impl in boss.Implementations)
			{
				Svn svn = impl as Svn;
				if (svn != null)
					return svn;
			}
			
			Trace.Fail("couldn't find an Svn implementation");
			
			return null;
		}
		#endregion

		#region Fields
		private Boss m_boss; 
		private readonly string[] m_candidates = new string[]{"Svn add", "Svn diff", "Svn log", "Svn revert"};
		#endregion
	} 
}