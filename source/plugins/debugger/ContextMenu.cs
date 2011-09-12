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
using Shared;
using System;
using System.Collections.Generic;

namespace Debugger
{
	internal sealed class ContextMenu : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, string language, bool editable, List<TextContextItem> items)
		{
			Boss boss = DoGetWindowBoss();
			if (boss != null)
			{
				var viewer = boss.Get<ICodeViewer>();
				if (viewer.CanDisplaySource())
				{
					if (viewer.CanDisplayIL())
					{
						if (viewer.IsShowingSource())
							items.Add(new TextContextItem("Show IL", s => {viewer.ShowIL(); return s;}, 0.01f));
						else
							items.Add(new TextContextItem("Show Source", s => {viewer.ShowSource(); return s;}, 0.01f));
					}
					
					items.Add(new TextContextItem("Open Source", s => {viewer.OpenSource(); return s;}, 0.011f));
				}
			}
		}
		
		#region Private Methods
		private Boss DoGetWindowBoss()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			if (boss != null && !boss.Has<ICodeViewer>())
				boss = null;
			
			return boss;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
