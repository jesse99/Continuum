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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DirectoryEditor	
{
	// TODO: need to get the path menu when command-click in title bar
	[ExportClass("DirectoryController", "NSWindowController", Outlets = "table targets build cancel prefsController")]
	internal sealed class DirectoryController : NSWindowController
	{
		public DirectoryController(string path) : base(NSObject.AllocNative("DirectoryController"))
		{	
			m_boss = ObjectModel.Create("DirectoryEditor");
			m_path = path;
			m_dirStyler = new DirectoryItemStyler(path);
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("directory-editor"), this);	
			m_table = new IBOutlet<NSOutlineView>(this, "table");
			m_targets = new IBOutlet<NSPopUpButton>(this, "targets");
			m_prefs = new IBOutlet<DirPrefsController>(this, "prefsController");
			
			m_name = System.IO.Path.GetFileName(path);
			window().setTitle(NSString.Create(m_name));
			Unused.Value = window().setFrameAutosaveName(NSString.Create(window().title().ToString() + " editor"));
			window().makeKeyAndOrderFront(this);
			
			m_table.Value.setDoubleAction("doubleClicked:");
			m_table.Value.setTarget(this);
			
			var wind = m_boss.Get<IWindow>();
			wind.Window = window();
			
			m_builder = new GenericBuilder(path);	
			
			m_targets.Value.removeAllItems();
			if (m_builder.CanBuild)
			{
				var handler = m_boss.Get<IMenuHandler>();
				handler.Register(this, 50, this.DoBuild, this.DoBuildEnabled);
				handler.Register(this, 51, this.DoBuildVariables, this.DoHaveBuilder);
				handler.Register(this, 52, this.DoBuildFlags, this.DoHaveBuilder);
				handler.Register(this, 599, () => m_builder.Cancel(), this.DoCancelEnabled);
				handler.Register(this, 1000, this.DoShowPrefs);
				
				Broadcaster.Register("global ignores changed", this, this.DoUpdateTargets);
				
				DoLoadPrefs(path);
				DoUpdateTargets(string.Empty, null);
			}
			
			m_root = new DirectoryItem(m_path, m_dirStyler, m_ignoredItems);
			Reload();
			
			m_watcher = DoCreateWatcher(path);
			if (m_watcher != null)
				m_watcher.Changed += this.DoDirChanged;
			
//			m_watcher = new FileSystemWatcher(path);
//			m_watcher.IncludeSubdirectories = true;
//			m_watcher.Created += this.DoDirChanged;
//			m_watcher.Deleted += this.DoDirChanged;
//			m_watcher.Renamed += this.DoDirChanged;
			
			m_boss.CallRepeated<IOpened>(i => i.Opened());
			Broadcaster.Invoke("opened directory", m_boss);
			
			ActiveObjects.Add(this);
		}
		
		public void windowWillClose(NSObject notification)
		{	
			var handler = m_boss.Get<IMenuHandler>();
			handler.Deregister(this);
			
			Broadcaster.Unregister(this);
			
			if (m_watcher != null)
				m_watcher.Dispose();
				
			if (m_root != null)
			{
				var root = m_root;
				m_root = null;
				
				m_table.Value.setDelegate(null);
				m_table.Value.reloadData();	

				root.Close();
				root.release();
			}

			window().autorelease();
			autorelease();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Reload()
		{
			m_dirStyler.Reload();

			var root = new DirectoryItem(m_path, m_dirStyler, m_ignoredItems);
			if (m_root != null)
			{
				m_root.Close();
				m_root.release();
			}
			m_root = root;
			
			m_table.Value.reloadData();		// TODO: collapses all the items...
		}
		
		public string Path
		{
			get {return m_root.Path;}
		}
		
		public string[] IgnoredTargets
		{
			get {return m_ignoredTargets;}
			set
			{
				if (!value.EqualValues(m_ignoredTargets))
				{
					m_ignoredTargets = value;
					Reload();
					DoSavePrefs();
				}
			}
		}
		
		public string[] IgnoredItems
		{
			get {return m_ignoredItems;}
			set
			{
				if (!value.EqualValues(m_ignoredItems))
				{
					m_ignoredItems = value;
					Reload();							// TODO: collapses all the items...
					DoSavePrefs();
				}
			}
		}
		
		public DateTime BuildStartTime
		{
			get {return m_startTime;}
		}
		
		public void outlineView_setObjectValue_forTableColumn_byItem(NSTableView table, NSObject value, NSTableColumn col, DirectoryItem item)
		{
			string newName = value.description();
			DoRename(item, newName);
		}
		
		public void targetChanged(NSPopUpButton sender)
		{
			m_builder.Target = sender.titleOfSelectedItem().ToString();
			DoSavePrefs();
		}
		
		public void renameItem(NSObject sender)
		{
			NSIndexSet selections = m_table.Value.selectedRowIndexes();
			if (selections.count() == 1)
			{
				uint row = selections.firstIndex();
				DirectoryItem item = (DirectoryItem) (m_table.Value.itemAtRow((int) row));
				string oldName = System.IO.Path.GetFileName(item.Path);
				
				var get = new GetString{Title = "New Name", Label = "Name:", Text = oldName};
				string newName = get.Run();
				if (newName != null)
					DoRename(item, newName);
			}
		}
		
		public void handleSccs(NSObject sender)
		{
			string command = sender.Call("title").To<NSObject>().description();

			foreach (uint row in m_table.Value.selectedRowIndexes())
			{
				DirectoryItem item = (DirectoryItem) (m_table.Value.itemAtRow((int) row));
				Sccs.Execute(command, item.Path);
			}
		}
		
		// Duplicate is such a common operation that we provide a shortcut for it.
		public void duplicate(NSObject sender)
		{
			foreach (uint row in m_table.Value.selectedRowIndexes())
			{
				DirectoryItem item = (DirectoryItem) (m_table.Value.itemAtRow((int) row));
				Sccs.Duplicate(item.Path);
			}
		}
		
		public void dirHandler(NSObject sender)
		{		
			int tag = (int) sender.Call("tag");
			
			var handler = m_boss.Get<IMenuHandler>();
			handler.Handle(tag);
		}
		
		public bool validateUserInterfaceItem(NSObject sender)
		{
			bool enabled = false;
			
			Selector sel = (Selector) sender.Call("action");
			
			if (sel.Name == "dirHandler:")
			{
				int tag = (int) sender.Call("tag");
				
				var handler = m_boss.Get<IMenuHandler>();
				enabled = handler.IsEnabled(tag);
				
				if (enabled && tag == 50 && sender.isMemberOfClass(NSMenuItem.Class))
				{
					Unused.Value = sender.Call("setTitle:", NSString.Create("Build " + m_name));
				}
				else if (enabled && tag == 1000 && sender.isMemberOfClass(NSMenuItem.Class))
				{
					Unused.Value = sender.Call("setTitle:", NSString.Create(m_name + " Preferences"));
				}
			}
			else if (sel.Name == "targetChanged:")
			{
				enabled = m_builder != null && m_builder.CanBuild;
			}
			else if (sel.Name == "renameItem:")
			{
				NSIndexSet selections = m_table.Value.selectedRowIndexes();
				enabled = selections.count() == 1;
			}
			else if (sel.Name == "duplicate:")
			{
				NSIndexSet selections = m_table.Value.selectedRowIndexes();
				enabled = selections.count() > 0 && 	m_table.Value.editedRow() < 0;	// cocoa crashes if we do a duplicate while editing...
			}
			else if (sel.Name == "handleSccs:")
			{
				var paths = new List<string>();
				foreach (uint row in m_table.Value.selectedRowIndexes())
				{
					DirectoryItem item = (DirectoryItem) (m_table.Value.itemAtRow((int) row));
					paths.Add(item.Path);
				}
				
				if (paths.Count > 0)
				{
					string command = sender.Call("title").To<NSObject>().description();
					Dictionary<string, string[]> commands = Sccs.GetCommands(paths);
					enabled = commands.Values.Any(a => Array.IndexOf(a, command) >= 0);
				}
			}
			else if (respondsToSelector(sel))
			{
				enabled = true;
			}
			else if (SuperCall("respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				enabled = SuperCall("validateUserInterfaceItem:", sender).To<bool>();
			}
			
			return enabled;
		}
		
		public void doubleClicked(NSOutlineView sender)
		{
			DoOpenSelection();
		}
		
		public new void keyDown(NSEvent evt)	
		{
			if (evt.keyCode() == 36)
				DoOpenSelection();
			else
				Unused.Value = SuperCall("keyDown:", evt);
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, DirectoryItem item)
		{
			if (m_root == null)
				return 0;
			
			return item == null ? m_root.Count : item.Count;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, DirectoryItem item)
		{
			return item == null ? true : item.Count != -1;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, DirectoryItem item)
		{
			if (m_root == null)
				return null;
			
			return item == null ? m_root[index] : item[index];
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, DirectoryItem item)
		{
			if (m_root == null)
				return NSString.Empty;
			
			if (col.identifier().ToString() == "1")
				return item == null ? m_root.Name : item.Name;
			else
				return item == null ? m_root.Bytes : item.Bytes;
		}
		
		#region Private Methods
		private void DoRename(DirectoryItem item, string newName)
		{
			string oldPath = item.Path;
			string oldName = System.IO.Path.GetFileName(oldPath);
			
			if (oldName != newName)
			{
				string oldDir = System.IO.Path.GetDirectoryName(oldPath);
				Sccs.Rename(oldPath, System.IO.Path.Combine(oldDir, newName));
			}
		}
				
		private DirectoryWatcher DoCreateWatcher(string path)
		{
			DirectoryWatcher watcher = null;
			
			try
			{
				watcher = new DirectoryWatcher(path, TimeSpan.FromSeconds(1));
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				
				transcript.WriteLine(Output.Error, "Can't update the window in response to changes to '{0}'.", path);
				transcript.WriteLine(Output.Error, e.ToString());	
			}
			
			return watcher;
		}
		
		private void DoDirChanged(object sender, DirectoryWatcherEventArgs e)
		{
			foreach (string path in e.Paths)
			{
				NSString spath = NSString.Create(path).stringByStandardizingPath();
				DirectoryItem item = m_root.Find(spath);
				if (item != null)				// TODO: note that the reloads will close any items beneath the item being closed
				{
					item.Reset();
					if (item == m_root)
					{
						item.Reset();
						m_table.Value.reloadItem_reloadChildren(null, true);
					}
					else
					{
						item.Reset();
						m_table.Value.reloadItem_reloadChildren(item, true);
					}
				}
			}
		}
		
		private void DoBuild()
		{
			m_startTime = DateTime.Now;
			m_builder.Build();
		}
		
		private bool DoBuildEnabled()	
		{
			return m_builder != null && (m_builder.State == State.Opened || m_builder.State == State.Built || m_builder.State == State.Canceled);
		}

		private void DoBuildFlags()
		{
			m_builder.SetBuildFlags();
		}
		
		private void DoBuildVariables()
		{
			m_builder.SetBuildVariables();
		}
		
		private bool DoHaveBuilder()
		{
			return m_builder != null;
		}
		
		private bool DoCancelEnabled()
		{
			return m_builder != null && m_builder.State == State.Building;
		}
		
		private void DoOpenSelection()
		{
			NSIndexSet selections = m_table.Value.selectedRowIndexes();
			uint row = selections.firstIndex();
			while (row != Enums.NSNotFound)
			{
				DirectoryItem item = (DirectoryItem) (m_table.Value.itemAtRow((int) row));
				DoOpen(item.Path);
				
				row = selections.indexGreaterThanIndex(row);
			}
		}

		private void DoOpen(string path)
		{
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(path, -1, -1, 1);
		}
		
		private void DoUpdateTargets(string name, object inValue)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string value = defaults.stringForKey(NSString.Create("globalIgnores")).To<NSString>().ToString();
			string[] ignored = value.Split(new char[]{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);

			List<string> titles = new List<string>();
			foreach (string title in m_builder.Targets)
			{
				if (Array.IndexOf(ignored, title) < 0 && Array.IndexOf(m_ignoredTargets, title) < 0)
					titles.Add(title);
			}
					
			if (titles.Count != m_targets.Value.itemTitles().count())
			{
				m_targets.Value.removeAllItems();
				m_targets.Value.addItemsWithTitles(NSArray.Create(titles.ToArray()));

				if (Array.IndexOf(ignored, m_builder.Target) < 0 && Array.IndexOf(m_ignoredTargets, m_builder.Target) < 0)
					m_targets.Value.selectItemWithTitle(NSString.Create(m_builder.Target));
				else
					m_builder.Target = m_targets.Value.titleOfSelectedItem().description();
			}
		}	

		private void DoShowPrefs()
		{		
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("dir-prefs"), this);
			Trace.Assert(!NSObject.IsNullOrNil(m_prefs.Value), "nib didn't set prefsController");
	
			m_prefs.Value.Open(this);
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			// ignored targets
			string key = Path + "-ignored targets";
			string value = string.Join(" ", m_ignoredTargets);
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// ignored targets
			key = Path + "-ignored items";
			value = string.Join(" ", m_ignoredItems);
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// default target
			key = Path + "-defaultTarget";
			value = m_builder.Target;
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
		}
		
		private void DoLoadPrefs(string path)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			// ignored targets
			string key = path + "-ignored targets";
			NSString value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_ignoredTargets = value.description().Split(' ');
			
			// ignored items
			key = path + "-ignored items";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_ignoredItems = value.description().Split(' ');
			else
				m_ignoredItems = new string[]{".*", "*.o", "*.pyc", "*.sln", "*.tmp"};
			
			// default target
			key = path + "-defaultTarget";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value) && Array.IndexOf(m_builder.Targets, value.description()) >= 0)
				m_builder.Target = value.description();		// if this is ignored DoUpdateTargets will fix things up
		}
		#endregion
		
		#region Fields
		private string m_path;
		private DirectoryItem m_root;
		private IBOutlet<NSOutlineView> m_table;
		private IBOutlet<NSPopUpButton> m_targets;
		private IBOutlet<DirPrefsController> m_prefs;
		private GenericBuilder m_builder;
		private DateTime m_startTime = DateTime.MinValue;
		private string m_name;
		private string[] m_ignoredTargets = new string[0];
		private string[] m_ignoredItems = new string[0];
		private Boss m_boss;
		private DirectoryItemStyler m_dirStyler;
		private DirectoryWatcher m_watcher;
//		private FileSystemWatcher m_watcher;
		#endregion
	}
}	