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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace TextEditor
{
	internal sealed class RestoreOpenFiles : IStartup, IShutdown
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnStartup()
		{
			var defaults = NSUserDefaults.standardUserDefaults();
			NSArray array = defaults.arrayForKey(NSString.Create("open-windows"));
			if (!NSObject.IsNullOrNil(array))
			{
				Boss boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				foreach (NSString path in array)
				{
					if (System.IO.File.Exists(path.ToString()))
						launcher.Launch(path.ToString(), -1, -1, 4);
				}
			}
		}
		
		public void OnShutdown()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			var paths = new List<string>();
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (editor.Path != null)
					paths.Add(editor.Path);
			}
			
			var array = NSArray.Create(paths.ToArray());
			var defaults = NSUserDefaults.standardUserDefaults();
			defaults.setObject_forKey(array, NSString.Create("open-windows"));
		}
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
