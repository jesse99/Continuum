// Copyright (C) 2008-2010 Jesse Jones
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
	internal sealed class DirectoryController : NSWindowController, IObserver
	{
		public DirectoryController(string path) : base(NSObject.AllocAndInitInstance("DirectoryController"))
		{
			m_boss = ObjectModel.Create("DirectoryEditor");
			m_path = path;
			m_dirStyler = new DirectoryItemStyler(path);
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("directory-editor"), this);
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			m_targets = new IBOutlet<NSPopUpButton>(this, "targets").Value;
			m_prefs = new IBOutlet<DirPrefsController>(this, "prefsController");
			
			m_name = System.IO.Path.GetFileName(path);
			window().setTitle(NSString.Create(m_name));
			Unused.Value = window().setFrameAutosaveName(NSString.Create(window().title().ToString() + " editor"));
			window().makeKeyAndOrderFront(this);
			
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			var wind = m_boss.Get<IWindow>();
			wind.Window = window();
			
			m_builder = new GenericBuilder(path);
			
			m_targets.removeAllItems();
			if (m_builder.CanBuild)
			{
				var handler = m_boss.Get<IMenuHandler>();
				handler.Register(this, 50, this.DoBuild, this.DoBuildEnabled);
				handler.Register(this, 51, this.DoBuildVariables, this.DoHaveBuilder);
				handler.Register(this, 52, this.DoBuildFlags, this.DoHaveBuilder);
				handler.Register(this, 599, () => m_builder.Cancel(), this.DoCancelEnabled);
				handler.Register(this, 1000, this.DoShowPrefs);
				
				DoLoadPrefs(path);
				Broadcaster.Register("global ignores changed", this);
				Broadcaster.Register("finished building", this);
				DoUpdateTargets(string.Empty, null);
			}
			else
				DoLoadPrefs(path);
			
			m_root = new FolderItem(m_path, m_dirStyler, this);
			m_table.reloadData();
			
			m_watcher = DoCreateWatcher(path);
			if (m_watcher != null)
				m_watcher.Changed += this.DoDirChanged;
			
//			m_watcher = new FileSystemWatcher(path);
//			m_watcher.IncludeSubdirectories = true;
//			m_watcher.Created += this.DoDirChanged;
//			m_watcher.Deleted += this.DoDirChanged;
//			m_watcher.Renamed += this.DoDirChanged;

			foreach (IOpened open in m_boss.GetRepeated<IOpened>())
			{
				open.Opened();
			}
			Broadcaster.Invoke("opened directory", m_boss);
			
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "global ignores changed":
					DoUpdateTargets(name, value);
					break;
				
				// Automatic validation on toolbar items has problems in 10.6 (e.g. if
				// you start a build the build buttons won't change state once the build
				// finishes unless you do something like click on another window).
				case "finished building":
					this["build"].Call("validate");
					this["cancel"].Call("validate");
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		protected override void OnDealloc()
		{
			Broadcaster.Invoke("closed directory", m_boss);
//			m_boss.Free();
			m_boss = null;
			
			base.OnDealloc();
		}
		
		public void windowWillClose(NSObject notification)
		{	
			var handler = m_boss.Get<IMenuHandler>();
			handler.Deregister(this);
			
			Broadcaster.Invoke("closing directory", m_boss);
			Broadcaster.Unregister(this);
			
			if (m_watcher != null)
			{
				m_watcher.Changed -= this.DoDirChanged;
				m_watcher.Dispose();
			}
				
			if (m_root != null)
			{
				var root = m_root;
				m_root = null;
				
				m_table.setDelegate(null);
				m_table.setTarget(null);
				root.release();
			}
			
			m_builder = null;
			
			window().autorelease();
			autorelease();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public NSOutlineView Table
		{
			get {return m_table;}
		}
		
		public void Reload()
		{
			if (m_root != null)
			{
				m_dirStyler.Reload();
				m_root.Reload(null);
				
				m_table.reloadData();
			}
		}
		
		public NSColor GetFileColor(string fileName)
		{
			return m_dirStyler.GetFileColor(fileName);
		}
		
		public string Path
		{
			get
			{
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				return m_root.Path;
			}
		}
		
		public string[] IgnoredTargets
		{
			get {return m_ignoredTargets;}
			set
			{
				if (!value.EqualValues(m_ignoredTargets))
				{
					m_ignoredTargets = value;
					DoUpdateTargets(string.Empty, null);
					DoSavePrefs();
				}
			}
		}
		
		public bool IsIgnored(string name)
		{
			return IsIgnored(name, IgnoredItems);
		}
		
		internal DirectoryItemStyler Styler {get {return m_dirStyler;}}
		
		[ThreadModel(ThreadModel.Concurrent)]
		internal static bool IsIgnored(string name, IEnumerable<string> ignored)
		{
			if (ignored != null)
			{
				foreach (string glob in ignored)
				{
					if (Glob.Match(glob, name))
						return true;
				}
			}
			
			return false;
		}
		
		public string[] IgnoredItems
		{
			get {return m_ignoredItems;}
			set
			{
				if (!value.EqualValues(m_ignoredItems))
				{
					m_ignoredItems = value;
					Reload();
					DoSavePrefs();
				}
			}
		}
		
		public bool AddSpace
		{
			get {return m_addSpace;}
			set
			{
				if (value != m_addSpace)
				{
					m_addSpace = value;
					DoSavePrefs();
				}
			}
		}
		
		public bool AddBraceLine
		{
			get {return m_addBraceLine;}
			set
			{
				if (value != m_addBraceLine)
				{
					m_addBraceLine = value;
					DoSavePrefs();
				}
			}
		}
		
		public bool UseTabs
		{
			get {return m_useTabs;}
			set
			{
				if (value != m_useTabs)
				{
					m_useTabs = value;
					DoSavePrefs();
				}
			}
		}
		
		public int NumSpaces
		{
			get {return m_numSpaces;}
			set
			{
				if (value != m_numSpaces)
				{
					m_numSpaces = value;
					DoSavePrefs();
				}
			}
		}
		
		public DateTime BuildStartTime
		{
			get {return m_startTime;}
		}
		
		public void outlineView_setObjectValue_forTableColumn_byItem(NSTableView table, NSObject value, NSTableColumn col, TableItem item)
		{
			string newName = value.description();
			DoRename(item, newName);
		}
		
		public void targetChanged(NSPopUpButton sender)
		{
			m_builder.Target = sender.titleOfSelectedItem().ToString();
			DoSavePrefs();
		}
		
		public void windowWillMiniaturize(NSNotification notification)
		{
			foreach (Boss boss in DoGetTextWindows())
			{
				var window = boss.Get<IWindow>();
				if (!window.Window.isMiniaturized())
					window.Window.miniaturize(this);
			}
		}
		
		public void windowDidDeminiaturize(NSNotification notification)
		{
			foreach (Boss boss in DoGetTextWindows())
			{
				var window = boss.Get<IWindow>();
				if (window.Window.isMiniaturized())
					window.Window.deminiaturize(this);
			}
		}
		
		public void renameItem(NSObject sender)
		{
			NSIndexSet selections = m_table.selectedRowIndexes();
			if (selections.count() == 1)
			{
				uint row = selections.firstIndex();
				TableItem item = (TableItem) (m_table.itemAtRow((int) row));
				string oldName = System.IO.Path.GetFileName(item.Path);
				
				var get = new GetString{Title = "New Name", Label = "Name:", Text = oldName};
				string newName = get.Run();
				if (newName != null)
					DoRename(item, newName);
			}
		}
		
		// Duplicate is such a common operation that we provide a shortcut for it.
		public void duplicate(NSObject sender)
		{
			foreach (TableItem item in DoGetSelectedItems())
			{
				Sccs.Duplicate(item.Path);
			}
			
			m_table.deselectAll(this);
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
				MenuState state = handler.GetState(tag);
				enabled = (state & MenuState.Enabled) == MenuState.Enabled;
				if (sender.respondsToSelector("setState:"))
					sender.Call("setState:", (state & MenuState.Checked) == MenuState.Checked ? 1 : 0);
				
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
				NSIndexSet selections = m_table.selectedRowIndexes();
				enabled = selections.count() == 1;
			}
			else if (sel.Name == "duplicate:")
			{
				NSIndexSet selections = m_table.selectedRowIndexes();
				enabled = selections.count() > 0 && m_table.editedRow() < 0;	// cocoa crashes if we do a duplicate while editing...
			}
			else if (respondsToSelector(sel))
			{
				enabled = true;
			}
			else if (SuperCall(NSWindowController.Class, "respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				enabled = SuperCall(NSWindowController.Class, "validateUserInterfaceItem:", sender).To<bool>();
			}
			
			return enabled;
		}
		
		public void doubleClicked(NSOutlineView sender)
		{
			TableItem[] selectedItems = DoGetSelectedItems().ToArray();
			if (selectedItems.Length == 1 && selectedItems[0].IsExpandable)
				if (m_table.isItemExpanded(selectedItems[0]))
					m_table.collapseItem(selectedItems[0]);
				else
					m_table.expandItem(selectedItems[0]);
			else
				DoOpenSelection();
		}
		
		public new void keyDown(NSEvent evt)
		{
			if (evt.keyCode() == 36)
				doubleClicked(m_table);
			else
				Unused.Value = SuperCall(NSWindowController.Class, "keyDown:", evt);
		}
		
		public void copy(NSObject sender)
		{
			m_table.Copy();
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, TableItem item)
		{
			if (m_root == null)
				return 0;
			
			return item == null ? m_root.Count : item.Count;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, TableItem item)
		{
			return item == null ? true : item.IsExpandable;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, TableItem item)
		{
			if (m_root == null)
				return null;
			
			return item == null ? m_root[index] : item[index];
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, TableItem item)
		{
			if (m_root == null)
				return NSString.Empty;
			
			if (col.identifier().ToString() == "1")
				return item == null ? m_root.Name : item.Name;
			else
				return item == null ? m_root.Bytes : item.Bytes;
		}
		
		#region Private Methods
		private void DoRename(TableItem item, string newName)
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
			// Remember whatever we have selected. Note that we can't simply
			// use the IEnumerable result because it will be lazily computed which
			// won't work in this context.
			TableItem[] oldSelections = DoGetSelectedItems().ToArray();
			
			// Update which ever items are open.
			bool changed = false;
			var added = new List<TableItem>();
			foreach (string path in e.Paths)
			{
				NSString spath = NSString.Create(path).stringByStandardizingPath();
				TableItem item = m_root.Find(spath);
				if (item != null)
				{
					if (item.Reload(added))
					{
						m_table.reloadItem_reloadChildren(item == m_root ? null : item, true);
						changed = true;
					}
				}
			}
			
			// If an item has changed then we'll need to fixup the table selections.
			if (changed)
			{
				m_table.deselectAll(this);
				
				// Note that the refcounts of the items in oldSelections may be zero at
				// this point (so we can't use things like Name).
				NSMutableIndexSet newSelections = NSMutableIndexSet.Create();
				if (oldSelections.Any())
				{
					// Selections are based on row index instead of the item so if we
					// don't do this then the selection will switch to a different item.
					foreach (TableItem item in oldSelections)
					{
						int row = m_table.rowForItem(item);
						if (row >= 0)											// item may no longer exist
						{
							newSelections.addIndex((uint) row);
						}
					}
				}
				else
				{
					// If there were no old selections then we'll select whatever was
					// added (this is a nice thing to do for duplicate and hopefully
					// will not prove annoying elsewhere).
					foreach (TableItem item in added)
					{
						int row = m_table.rowForItem(item);
						if (row >= 0)
						{
							newSelections.addIndex((uint) row);
						}
					}
				}
				
				// Select the new items or fixup the selections for old selections.
				if (newSelections.count() > 0)
				{
					m_table.selectRowIndexes_byExtendingSelection(newSelections, false);
					
					if (newSelections.count() == 1)
						m_table.scrollRowToVisible((int) newSelections.First());
				}
				
				Broadcaster.Invoke("directory changed", m_boss);
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
			TableItem[] items = DoGetSelectedItems().ToArray();
			
			uint count = (uint) items.Length;
			if (NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
			{
				foreach (TableItem item in items)
				{
					DoOpen(item.Path);
				}
			}
		}
		
		private void DoOpen(string path)
		{
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(path, -1, -1, 1);
		}
		
		private IEnumerable<Boss> DoGetTextWindows()
		{
			string root = Path;
			if (!root.EndsWith("/"))
				root += "/";
				
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			foreach (Boss candidate in windows.All())
			{
				var editor = candidate.Get<ITextEditor>();
				if (editor.Path.StartsWith(root))
					yield return candidate;
			}
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
					titles.AddIfMissing(title);
			}
			
			if (titles.Count != m_targets.itemTitles().count())
			{
				m_targets.removeAllItems();
				m_targets.addItemsWithTitles(NSArray.Create(titles.ToArray()));
				
				if (Array.IndexOf(ignored, m_builder.Target) < 0 && Array.IndexOf(m_ignoredTargets, m_builder.Target) < 0)
					m_targets.selectItemWithTitle(NSString.Create(m_builder.Target));
				else
					m_builder.Target = m_targets.titleOfSelectedItem().description();
			}
		}
		
		public IEnumerable<TableItem> DoGetSelectedItems()
		{
			foreach (uint row in m_table.selectedRowIndexes())
			{
				TableItem item = (TableItem) (m_table.itemAtRow((int) row));
				yield return item;
			}
		}
		
		private void DoShowPrefs()
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("dir-prefs"), this);
			Contract.Assert(!NSObject.IsNullOrNil(m_prefs.Value), "nib didn't set prefsController");
			
			m_prefs.Value.Open(this);
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			// ignored targets
			string key = Path + "-ignored targets";
			string value = Glob.Join(m_ignoredTargets);
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// add space
			key = Path + "-add space";
			value = m_addSpace ? "1" : "0";
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// add brace line
			key = Path + "-add curly brace line";
			value = m_addBraceLine ? "1" : "0";
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// use tabs
			key = Path + "-use tabs";
			value = m_useTabs ? "1" : "0";
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// number of spaces
			key = Path + "-number of spaces";
			value = m_numSpaces.ToString();
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// ignored targets
			key = Path + "-ignored items";
			value = Glob.Join(m_ignoredItems);
			defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			
			// default target
			if (m_builder.Target != null)
			{
				key = Path + "-defaultTarget";
				value = m_builder.Target;
				defaults.setObject_forKey(NSString.Create(value), NSString.Create(key));
			}
		}
		
		private void DoLoadPrefs(string path)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			// ignored targets
			string key = path + "-ignored targets";
			NSString value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_ignoredTargets = Glob.Split(value.description());		// these aren't globs but they are split the same way
			
			// ignored items
			key = path + "-ignored items";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_ignoredItems = Glob.Split(value.description());
			else
				m_ignoredItems = new string[]{".*", "*.o", "*.pyc", "MIT.X11", "CVS", "waf", "*.sln", "*.csproj"};
			
			// add space
			key = path + "-add space";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_addSpace = value.description() == "1";
			else
				m_addSpace = false;
			
			// add brace line
			key = path + "-add curly brace line";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_addBraceLine = value.description() == "1";
			else
				m_addBraceLine = true;
			
			// use tabs
			key = path + "-use tabs";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_useTabs = value.description() == "1";
			else
				m_useTabs = true;
			
			// number of spaces
			key = path + "-number of spaces";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value))
				m_numSpaces = int.Parse(value.description());
			else
				m_numSpaces = 4;
			
			// default target
			key = path + "-defaultTarget";
			value = defaults.stringForKey(NSString.Create(key)).To<NSString>();
			if (!NSObject.IsNullOrNil(value) && Array.IndexOf(m_builder.Targets, value.description()) >= 0)
				m_builder.Target = value.description();		// if this is ignored DoUpdateTargets will fix things up
		}
		#endregion
		
		#region Fields
		private string m_path;
		private FolderItem m_root;
		private NSOutlineView m_table;
		private NSPopUpButton m_targets;
		private IBOutlet<DirPrefsController> m_prefs;
		private GenericBuilder m_builder;
		private DateTime m_startTime = DateTime.MinValue;
		private string m_name;
		private string[] m_ignoredTargets = new string[0];
		private string[] m_ignoredItems = new string[0];
		private bool m_addSpace;
		private bool m_addBraceLine = true;
		private int m_numSpaces = 4;
		private bool m_useTabs = true;
		private Boss m_boss;
		private DirectoryItemStyler m_dirStyler;
		private DirectoryWatcher m_watcher;
		#endregion
	}
}
