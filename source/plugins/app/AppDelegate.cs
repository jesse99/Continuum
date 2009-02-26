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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace App
{
	// http://developer.apple.com/documentation/Cocoa/Reference/ApplicationKit/Classes/NSApplication_Class/Reference/Reference.html#//apple_ref/doc/uid/20000012-BAJFJIIB
	[ExportClass("AppDelegate", "NSObject", Outlets = "SccsMenu")]
	internal sealed class AppDelegate : NSObject
	{
		private AppDelegate(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public void applicationDidFinishLaunching(NSObject notification)
		{
			m_boss = ObjectModel.Create("Application");
			Log.WriteLine(TraceLevel.Verbose, "Startup", "invoking IStartup");
			m_boss.CallRepeated<IStartup>(i => i.OnStartup());
			
			// Initialize the Sccs menu.
			NSMenu menu = this["SccsMenu"].To<NSMenu>();
			menu.setAutoenablesItems(false);
			menu.setDelegate(this);
			
			Dictionary<string, string[]> commands = Sccs.GetCommands();
			foreach (var entry in commands)
			{
				if (entry.Value.Length > 0)
				{
					if (menu.numberOfItems() > 0)
						menu.addItem(NSMenuItem.separatorItem());
					
					var cmds = new List<string>(entry.Value);
					cmds.Sort();
					foreach (string command in cmds)
					{
						Unused.Value = menu.addItemWithTitle_action_keyEquivalent(
							NSString.Create(command), "handleSccs:", NSString.Empty);
					}
					
					var item = NSMenuItem.Alloc().initWithTitle_action_keyEquivalent(
						NSString.Create(entry.Key), null, NSString.Empty);
					item.autorelease();
				}
			}
			Log.WriteLine(TraceLevel.Verbose, "Startup", "initialized Sccs menu");
		}
		
		public void applicationDidBecomeActive(NSObject notification)
		{
			Log.WriteLine(TraceLevel.Verbose, "Startup", "applicationDidBecomeActive");
//			Boss boss = ObjectModel.Create("TextEditorPlugin");
//			DoReload(boss);
			
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			DoReload(boss);
		}
		
		public bool applicationShouldOpenUntitledFile(NSObject app)
		{
			return false;
		}
		
		public void applicationWillTerminate(NSObject notification)
		{
			m_boss.CallRepeated<IShutdown>(i => i.OnShutdown());
			
//			NSUserDefaults.standardUserDefaults().synchronize();
			
			Log.WriteLine("App", "exiting normally");
		}
		
		// We handle the enabling manually for the Sccs menu so that we can call
		// Sccs.GetCommands once for the menu instead of for each item which 
		// makes a noticeable speed difference.
		public void menuNeedsUpdate(NSMenu menu)
		{
			string[] paths = DoGetSelectedPaths();
			Dictionary<string, string[]> commands = Sccs.GetCommands(paths);
				
			for (int i = 0; i < menu.numberOfItems(); ++i)
			{
				NSMenuItem item = menu.itemAtIndex(i);
				
				string command = item.title().description();
				bool enable = commands.Values.Any(a => Array.IndexOf(a, command) >= 0);
				
				item.setEnabled(enable);
			}
		}
		
		public void handleSccs(NSObject sender)
		{
			string command = sender.Call("title").To<NSObject>().description();
			
			string[] paths = DoGetSelectedPaths();
			foreach (string path in paths)
			{
				Sccs.Execute(command, path);
			}
		}

#if DEBUG
		public void dumpBosses(NSObject sender)
		{
			Boss[] bosses = Boss.GetBosses();
			foreach (Boss boss in bosses)
			{
				Console.WriteLine("{0}", boss);
			}
		}
		
		// Note that there are races in this code (e.g. another thread may
		// release the last reference to an object before we can retain it).
		// Also we don't always know if an object is deallocated.
		public void dumpObjectDetails(NSObject sender)
		{
//			Call("collectGarbage:", this);
			for (int i = 0; i < 4; ++i)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				System.Threading.Thread.Sleep(250);
			}
			
			foreach (NSObject o in NSObject.Snapshot())
			{
				if (!o.IsDeallocated())
				{
//					o.retain();							// not all instances can be retained...
					string summary = o.ToString("G", null);
					if (summary.Contains("instance NSCFDictionary"))
					{
						Console.WriteLine("{0}", summary);
					
						string details = o.ToString("D", null);
						Console.WriteLine("   {0}", details.Length < 480 ? details : details.Substring(0, 480));
					}
//					o.release();
				}
				else
					Console.WriteLine("{0}", o);		// this will work if o is deallocated...
			}
			
			Console.WriteLine(" ");
		}
		
		public void dumpActiveObjects(NSObject sender)
		{
//			Call("collectGarbage:", this);
			for (int i = 0; i < 4; ++i)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				System.Threading.Thread.Sleep(250);
			}
			
			var dict = new Dictionary<Type, int>();
			foreach (object o in ActiveObjects.Snapshot())
			{
				Type type = o.GetType();
				if (dict.ContainsKey(type))
					dict[type] +=1 ;
				else
					dict[type] = 1;
			}
			
			foreach (var entry in dict)
			{
				Console.WriteLine("{0} {1}", entry.Value, entry.Key);
			}
			Console.WriteLine(" ");
		}
#endif	
		
		public void appHandler(NSObject sender)
		{
			int tag = (int) sender.Call("tag");
			
			var handler = m_boss.Get<IMenuHandler>();
			handler.Handle(tag);
		}
		
		public void openDir(NSObject sender)
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var open = boss.Get<IOpen>();
			open.Open();
		}
		
		public void openScripts(NSObject sender)
		{
			string path = System.IO.Path.Combine(Paths.SupportPath, "scripts/standard/");
			
			NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
				NSString.Create(path), NSString.Empty);
		}
		
		public void openRefactors(NSObject sender)
		{
			string path = System.IO.Path.Combine(Paths.SupportPath, "refactors/standard/");
		
			NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
				NSString.Create(path), NSString.Empty);
		}
		
		public void saveAll(NSObject sender)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				editor.Save();
			}
		}
		
		public void setPreferences(NSObject sender)
		{
			if (m_prefs == null)
				m_prefs = new PreferencesController();
			
			m_prefs.window().makeKeyAndOrderFront(this);
		}
		
		public void find(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.Find();
		}
		
		public void findInFiles(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.FindInFiles();
		}
		
		public void findAgain(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.FindNext();
		}
		
		public void findPrevious(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.FindPrevious();
		}
		
		public void useSelectionForFind(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.UseSelectionForFind();
		}
		
		public void useSelectionForReplace(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.UseSelectionForReplace();
		}
		
		public void replace(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.Replace();
		}
		
		public void replaceAll(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.ReplaceAll();
		}
		
		public void replaceAndFindAgain(NSObject sender)
		{
			var find = m_boss.Get<IFind>();
			find.ReplaceAndFind();
		}
		
		public bool validateUserInterfaceItem(NSObject sender)
		{
			bool enabled = false;
			
			Selector sel = (Selector) sender.Call("action");
			
			if (sel.Name == "appHandler:")
			{
				int tag = (int) sender.Call("tag");
				
				var handler = m_boss.Get<IMenuHandler>();
				enabled = handler.IsEnabled(tag);
			}
			else if (sel.Name == "find:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanFind();
			}
			else if (sel.Name == "useSelectionForFind:" || sel.Name == "useSelectionForReplace:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanUseSelection();
			}
			else if (sel.Name == "findAgain:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanFindNext();
			}
			else if (sel.Name == "findPrevious:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanFindPrevious();
			}
			else if (sel.Name == "replace:" || sel.Name == "replaceAll:" || sel.Name == "replaceAndFindAgain:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanReplace();
			}
			else if (respondsToSelector(sel))
			{
				enabled = true;
			}
			else if (SuperCall("respondsToSelector:", new Selector("validateUserInterfaceItem")).To<bool>())
			{
				enabled = SuperCall("validateUserInterfaceItem:", sender).To<bool>();
			}
			
			return enabled;
		}
		
		#region Private Methods
		private string[] DoGetSelectedPaths()
		{
			string[] paths = null;
			
			// First see if the main window is a text window.
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			if (boss != null)
			{
				var editor = boss.Get<ITextEditor>();
				paths = new string[]{editor.Path};
			}
			
			// Then see if it is a directory window.
			if (paths == null)
			{
				boss = ObjectModel.Create("DirectoryEditorPlugin");
				windows = boss.Get<IWindows>();
				boss = windows.Main();
				
				if (boss != null)
				{
					var editor = boss.Get<IDirectoryEditor>();
					paths = editor.SelectedPaths();
				}
			}
			
			// If the main window is not a text or directory window then we can't
			// get a path for the sccs commands.
			if (paths == null)
				paths = new string[0];
			
			return paths;
		}
		
		private void DoReload(Boss boss)
		{
			var windows = boss.Get<IWindows>();
			foreach (Boss b in windows.All())
			{
				if (b.Has<IReload>())
				{
					var reload = b.Get<IReload>();
					reload.Reload();
				}
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private PreferencesController m_prefs;
		#endregion
	}
}
