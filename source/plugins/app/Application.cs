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
using MObjc.Helpers;
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
		
		public void Run(string[] args)
		{
			NSApplication app = NSApplication.Create("App", "MainMenu.nib", this.DoExtendDebugMenu);
			Log.WriteLine(TraceLevel.Verbose, "Startup", "created NSApplication");
			
			var pool = NSAutoreleasePool.Create();
			NSMutableDictionary dict = NSMutableDictionary.Create();
			dict.setObject_forKey(NSNumber.Create(20), NSString.Create("NSRecentDocumentsLimit"));	// TODO: add a pref for this
			foreach (IFactoryPrefs factory in m_boss.GetRepeated<IFactoryPrefs>())
			{
				factory.OnInitFactoryPref(dict);
			}
			NSUserDefaultsController.sharedUserDefaultsController().setInitialValues(dict);
			NSUserDefaults.standardUserDefaults().registerDefaults(dict);
			Log.WriteLine(TraceLevel.Verbose, "Startup", "initialized default prefs");
			
			// TODO: starting with mono 2.2 the path to the exe is part of the unmanaged command
			// line arguments, but not the managed command line arguments. So, when cocoa starts
			// up it opens continuum.exe as a document. The following line suppresses cocoa's argument
			// processing... 
			NSUserDefaults.standardUserDefaults().setObject_forKey(NSString.Create("NO"), NSString.Create("NSTreatUnknownArgumentsAsOpen"));
			
			foreach (string path in args)
			{
				app.BeginInvoke(() => DoOpen(path), TimeSpan.FromSeconds(0.250));
			}
			
			Log.WriteLine(TraceLevel.Verbose, "Startup", "starting event loop");
			Broadcaster.Invoke("starting event loop", null);
			pool.release();
			
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
			
			dict.setObject_forKey(NSString.Create("~"), NSString.Create("debug_assembly_path"));
			dict.setObject_forKey(NSString.Create("~"), NSString.Create("debug_assembly_working_dir"));
			dict.setObject_forKey(NSString.Empty, NSString.Create("debug_assembly_args"));
			dict.setObject_forKey(NSString.Empty, NSString.Create("debug_assembly_env"));
			dict.setObject_forKey(NSString.Create("mono"), NSString.Create("debug_assembly_tool"));
		}
		
		#region Private Methods
		private static void DoOpen(string path)
		{
			try
			{
				Log.WriteLine(TraceLevel.Info, "Startup", "opening '{0}'", path);
				NSURL url = NSURL.fileURLWithPath(NSString.Create(path));
				NSError err;
				NSDocumentController.sharedDocumentController().openDocumentWithContentsOfURL_display_error(
					url, true, out err);
					
				if (!NSObject.IsNullOrNil(err))
					err.Raise();
			}
			catch (Exception e)
			{
				// A path like '-psn_0_9378033' is now passed in as the first argument when running mono.
				// Not sure what it is, but we don't want to bother users about it...
				if (!path.StartsWith("-psn_"))
				{
					NSString title = NSString.Create("Couldn't open the file.");
					NSString message = NSString.Create(e.Message);
					Unused.Value = Functions.NSRunAlertPanel(title, message);
				}
			}
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
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
