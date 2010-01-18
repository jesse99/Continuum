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

using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;

// Allow deprecated methods so that we can continue to run on leopard.
#pragma warning disable 618

namespace DirectoryEditor
{
	internal sealed class OpenDir : IOpen
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Open()
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Open Directory"));
			panel.setCanChooseDirectories(true);
			panel.setCanChooseFiles(false);
			panel.setAllowsMultipleSelection(true);
			
			int btn = panel.runModal();
			
			if (btn == Enums.NSOKButton)
			{
				foreach (NSString path in panel.filenames())
				{
					Open(path.ToString());
				}
			}
		}
		
		public void Open(string path)
		{
			var windows = m_boss.Get<IWindows>();
			foreach (Boss boss in windows.All())
			{
				var editor = boss.Get<IDirectoryEditor>();
				if (editor.Path == path)
				{
					var window = boss.Get<IWindow>();
					window.Window.makeKeyAndOrderFront(NSApplication.sharedApplication());
					return;
				}
			}
			
			Gear.Helpers.Unused.Value = new DirectoryController(path);
		}
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
