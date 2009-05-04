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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;
using System.IO;

namespace App
{
	internal sealed class Application : IApplication, IFactoryPrefs
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Run()
		{
			NSApplication app = NSApplication.Create("App", "MainMenu.nib", this.DoExtendDebugMenu);
			Log.WriteLine(TraceLevel.Verbose, "Startup", "created NSApplication");
			
			var pool = NSAutoreleasePool.Create();
			NSMutableDictionary dict = NSMutableDictionary.Create();
			dict.setObject_forKey(NSNumber.Create(20), NSString.Create("NSRecentDocumentsLimit"));	// TODO: add a pref for this
			m_boss.CallRepeated<IFactoryPrefs>(i => i.OnInitFactoryPref(dict));
			NSUserDefaultsController.sharedUserDefaultsController().setInitialValues(dict);
			NSUserDefaults.standardUserDefaults().registerDefaults(dict);	
			Log.WriteLine(TraceLevel.Verbose, "Startup", "initialized default prefs");
			
			// TODO: starting with mono 2.2 the path to the exe is part of the unmanaged command
			// line arguments, but not the managed command line arguments. So, when cocoa starts
			// up it opens continuum.exe as a document. The following line suppresses cocoa's argument
			// processing... 
			NSUserDefaults.standardUserDefaults().setObject_forKey(NSString.Create("NO"), NSString.Create("NSTreatUnknownArgumentsAsOpen"));
			pool.release();
			
			Log.WriteLine(TraceLevel.Verbose, "Startup", "starting event loop");
			app.run();
		}
		
		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string[] candidates = fs.LocatePath("/mono-uninstalled.pc.in");
			if (candidates.Length > 0)
			{
				string root = Path.GetDirectoryName(candidates[0]);				
				dict.setObject_forKey(NSString.Create(root), NSString.Create("mono_root"));
			}
			else
				dict.setObject_forKey(NSString.Create("/some/thing/mono-2.2"), NSString.Create("mono_root"));
		}
		
		private void DoExtendDebugMenu(NSMenu menu)
		{
#if DEBUG
			NSApplication app = NSApplication.sharedApplication();
			menu.addItem(NSMenuItem.Create("Dump Bosses", "dumpBosses:", app.delegate_()));
			menu.addItem(NSMenuItem.separatorItem());
			menu.addItem(NSMenuItem.Create("Dump Object Details", "dumpObjectDetails:", app.delegate_()));
			menu.addItem(NSMenuItem.Create("Dump Active Objects", "dumpActiveObjects:", app.delegate_()));
#endif
		}
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
