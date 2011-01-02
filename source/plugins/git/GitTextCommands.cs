// Copyright (C) 2010 Jesse Jones
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Git
{
	internal sealed class GitTextCommands : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, bool editable, List<TextContextItem> items)
		{
			if (selection == null && editable)
			{
				string path = DoGetPath();
				if (path != null)
				{
					Git git = DoGetGit();
					string[] commands = git.GetCommands(new string[]{path});
					
					items.Add(new TextContextItem(0.8f));
					foreach (string command in commands)
					{
						if (m_candidates.Contains(command))
						{
							string c = command;
							items.Add(new TextContextItem(command, s => {git.Execute(c, path); return s;}, 0.8f));
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
		
		private Git DoGetGit()
		{
			Boss boss = ObjectModel.Create("Sccs");
			foreach (object impl in boss.Implementations)
			{
				var git = impl as Git;
				if (git != null)
					return git;
			}
			
			Contract.Assert(false, "couldn't find a Git implementation");
			
			return null;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private readonly string[] m_candidates = new string[]{"Git blame", "Git checkout", "Git diff", "Git log", "Gitk"};
		#endregion
	}
}
