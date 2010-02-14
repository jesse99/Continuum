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
using MCocoa;
using MObjc;
using Mono.Unix;
using Mono.Unix.Native;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Allow deprecated methods so that we can continue to run on leopard.
#pragma warning disable 618

namespace App
{
	// http://developer.apple.com/documentation/Cocoa/Reference/ApplicationKit/Classes/NSApplication_Class/Reference/Reference.html#//apple_ref/doc/uid/20000012-BAJFJIIB
	[ExportClass("AppDelegate", "NSObject", Outlets = "SccsMenu")]
	internal sealed class AppDelegate : NSObject
	{
		private AppDelegate(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
			Profile.Start("App");
		}
		
		public void applicationDidFinishLaunching(NSObject notification)
		{
			m_boss = ObjectModel.Create("Application");
			Log.WriteLine(TraceLevel.Verbose, "Startup", "invoking IStartup");
			
			foreach (IStartup start in m_boss.GetRepeated<IStartup>())
			{
				start.OnStartup();
			}
			
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
			foreach (IShutdown downer in m_boss.GetRepeated<IShutdown>())
			{
				downer.OnShutdown();
			}
			
//			NSUserDefaults.standardUserDefaults().synchronize();

			Profile.Stop("App");
#if PROFILE
			Console.WriteLine(Profile.GetResults());
#endif
			
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
		
		public void openBinary(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Open as Binary"));
			panel.setTreatsFilePackagesAsDirectories(true);
			panel.setAllowsMultipleSelection(true);
			
			int btn = panel.runModal();
			
			if (btn == Enums.NSOKButton)
			{
				foreach (NSString path in panel.filenames())
				{
					DoOpenBinary(path);
				}
			}
		}
		
		public void openBinaries(NSObject sender)
		{
			string[] paths = DoGetSelectedPaths();
			foreach (string path in paths)
			{
				if (System.IO.File.Exists(path))
					DoOpenBinary(NSString.Create(path));
			}
		}
		
		private const string Script = @"#!/bin/sh
# Takes a list of files and opens them within {0}.
app={1}

if [ ""$1"" = ""-?"" ] || [ ""$1"" = ""-h"" ] || [ ""$1"" = ""--help"" ] ; then
    echo ""Usage: {2} [files]""
    exit 0
fi

if [ -d ""$app"" ] ; then
    open -a ""$app"" ""$@""
else
    echo ""Couldn't find $app: try re-installing the tool.""
fi
";
		
		public void installTool(NSObject sender)
		{
			try
			{
				// Get the path and the name of the app bundle.
				string appPath = DoGetAppPath();
				string appName = Path.GetFileNameWithoutExtension(appPath);
				
				// Generate the script and write it out to /tmp.
				string tmpPath = Path.Combine("/tmp", appName.ToLower());
				var info = new UnixFileInfo(tmpPath);
				using (UnixStream stream = info.Open(
					OpenFlags.O_CREAT | OpenFlags.O_TRUNC | OpenFlags.O_WRONLY,
					FilePermissions.ACCESSPERMS))
				{
					string script = string.Format(Script, appName, appPath, appName.ToLower());
					byte[] buffer = System.Text.Encoding.UTF8.GetBytes(script);
					stream.Write(buffer, 0, buffer.Length);
				}
				
				// Use Authorization Services to copy the script to /usr/bin.
				string realPath = Path.Combine("/usr/bin", appName.ToLower());
				DoInstall(tmpPath, realPath);
				
				// Tell the user what we did.
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.Show();
				transcript.WriteLine(Output.Normal, "installed {0}", realPath);
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Couldn't install the tool.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
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
			Console.WriteLine("Bosses:");
			Boss[] bosses = Boss.GetBosses();
			foreach (Boss boss in bosses)
			{
				if (!boss.Definition.IsAbstract())
					Console.WriteLine("    {0}", boss);
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
					
//						string details = o.ToString("D", null);
//						Console.WriteLine("   {0}", details.Length < 480 ? details : details.Substring(0, 480));
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
			string path = Path.Combine(Paths.ScriptsPath, "scripts/standard/");
			
			NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
				NSString.Create(path), NSString.Empty);
		}
		
		public void openRefactors(NSObject sender)
		{
			string path = Path.Combine(Paths.ScriptsPath, "refactors/standard/");
		
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
			else if (sel.Name == "useSelectionForFind:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanUseSelectionForFind();
			}
			else if (sel.Name == "useSelectionForReplace:")
			{
				var find = m_boss.Get<IFind>();
				enabled = find.CanUseSelectionForReplace();
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
			
			return enabled;
		}
		
		#region Private Methods
		private void DoOpenBinary(NSString path)
		{
			try
			{
				NSURL url = NSURL.fileURLWithPath(path);
				NSDocumentController controller = NSDocumentController.sharedDocumentController();
				
				NSDocument doc = controller.documentForURL(url);
				if (NSObject.IsNullOrNil(doc))
				{
					NSError err;
					doc = controller.makeDocumentWithContentsOfURL_ofType_error(
						url, NSString.Create("binary"), out err);
					if (!NSObject.IsNullOrNil(err))
						err.Raise();
						
					controller.addDocument(doc);
					doc.makeWindowControllers();
				}
				
				doc.showWindows();
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't open {0:D}", path);
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
				
				NSString title = NSString.Create("Couldn't open '{0:D}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private string DoGetAppPath()
		{
			string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			Contract.Assert(Path.IsPathRooted(asmPath), asmPath + " is not absolute");
			
			string appPath = asmPath;
			while (!string.IsNullOrEmpty(appPath) && Path.GetExtension(appPath) != ".app")
			{
				appPath = Path.GetDirectoryName(appPath);
			}
			
			if (string.IsNullOrEmpty(appPath))
				throw new Exception("Couldn't get the .app directory from " + asmPath);
			
			return appPath;
		}
		
		public void DoInstall(string fromPath, string toPath)
		{
			string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string path = Path.GetDirectoryName(asmPath);
			path = Path.GetDirectoryName(path);
			path = Path.GetDirectoryName(path);
			
			using (Process process = new Process())
			{
				process.StartInfo.FileName = "install-tool";
				process.StartInfo.Arguments = string.Format("{0} {1}", fromPath, toPath);
				process.StartInfo.RedirectStandardOutput = false;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.WorkingDirectory = path;
				
				process.Start();
				process.WaitForExit();
				if (process.ExitCode != 0)
					throw new Exception("Failed with error " + process.ExitCode);
			}
		}
		
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
