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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace App
{
	[ExportClass("BrowseRecentFilesController", "NSWindowController", Outlets = "table")]
	internal sealed class BrowseRecentFilesController : NSWindowController, IObserver
	{
		public BrowseRecentFilesController() : base(NSObject.AllocAndInitInstance("BrowseRecentFilesController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("BrowseRecentFiles"), this);
			
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			Broadcaster.Register("opening document window", this);
			Broadcaster.Register("saved new document window", this);
			Broadcaster.Register("opened directory", this);
			
			DoLoadPrefs();
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opening document window":		// this will change the items we show
				case "saved new document window":		// need this to handle new documents
				case "opened directory":					// this may change the colors used by the items
					if (window().isVisible() || window().isMiniaturized())
						DoReload();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void Show()
		{
			DoReload();
			window().makeKeyAndOrderFront(this);
		}
		
		public void sortByName(NSObject sender)
		{
			m_sortByDate = false;
			DoSavePrefs();
			
			DoSort();
			m_table.reloadData();
		}
		
		public void sortByDate(NSObject sender)
		{
			m_sortByDate = true;
			DoSavePrefs();
			
			DoSort();
			m_table.reloadData();
		}
		
		public void clear(NSObject sender)
		{
			NSDocumentController.sharedDocumentController().clearRecentDocuments(this);
			
			DoReload();
			m_table.reloadData();
		}
		
		public void openWithFinder(NSObject sender)
		{
			uint count = m_table.selectedRowIndexes().count();
			if (NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
			{
				foreach (uint row in m_table.selectedRowIndexes())
				{
					string path = m_files[(int) row].Path;
					NSWorkspace.sharedWorkspace().openFile(NSString.Create(path));
				}
			}
		}
		
		public void showInFinder(NSObject sender)
		{
			foreach (uint row in m_table.selectedRowIndexes())
			{
				string path = m_files[(int) row].Path;
				NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
					NSString.Create(path), NSString.Empty);
			}
		}
		
		public new void keyDown(NSEvent evt)
		{
			bool handled = false;
			
			ushort key = evt.keyCode();
			if (key == Constants.ReturnKey || key == Constants.EnterKey)
			{
				if (m_table.numberOfRows() > 0)
				{
					doubleClicked(this);
					handled = true;
				}
			}
			
			if (!handled)
				Unused.Value = SuperCall(NSWindowController.Class, "keyDown:", evt);
		}
		
		public void doubleClicked(NSObject sender)
		{
			uint count = m_table.selectedRowIndexes().count();
			if (NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
			{
				Boss boss = Gear.ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				foreach (uint row in m_table.selectedRowIndexes())
				{
					launcher.Launch(m_files[(int) row].Path, -1, -1, 1);
				}
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_files != null ? m_files.Length : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSObject col, int row)
		{
			return m_files[row].DisplayText;
		}
		
		public void menuNeedsUpdate(NSMenu menu)
		{
			int row = m_table.clickedRow();
			if (!m_table.isRowSelected(row))
			{
				var indexes = NSIndexSet.indexSetWithIndex((uint) row);
				m_table.selectRowIndexes_byExtendingSelection(indexes, false);
			}
		}
		
		public bool validateUserInterfaceItem(NSObject sender)
		{
			bool enabled = false;
			
			Selector sel = (Selector) sender.Call("action");
			if (sel.Name == "sortByName:")
			{
				enabled = true;
				if (sender.respondsToSelector("setState:"))
					sender.Call("setState:", m_sortByDate ? 0 : 1);
			}
			else if (sel.Name == "sortByDate:")
			{
				enabled = true;
				if (sender.respondsToSelector("setState:"))
					sender.Call("setState:", m_sortByDate ? 1 : 0);
			}
			else if (sel.Name == "openWithFinder:" || sel.Name == "showInFinder:")
			{
				enabled = m_table.numberOfSelectedRows() > 0;
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
		
		#region Private Types
		private struct RecentFile
		{
			public RecentFile(string path, IFindDirectoryEditor finder, int index) : this()
			{
				Path = path;
				FileName = System.IO.Path.GetFileName(path);
				DisplayName = FileName;
				Index = index;
				
				// File name colors are associated with directory editors so coloring will only
				// happen if the appropriate directory is being edited.
				NSColor color = NSColor.blackColor();
				Boss editor = finder.GetDirectoryEditor(path);
				if (editor != null)
					color = editor.Get<IFileColor>().GetColor(FileName);
				
				DisplayText = NSAttributedString.Create(FileName, Externs.NSForegroundColorAttributeName, color);
				DisplayText.retain();
			}
			
			public RecentFile(RecentFile file, string displayName) : this()
			{
				Path = file.Path;
				FileName = file.FileName;
				DisplayName = displayName;
				Index = file.Index;
				
				NSDictionary attrs = file.DisplayText.fontAttributesInRange(new NSRange(0, 1));
				DisplayText = NSAttributedString.Create(displayName, attrs);
				DisplayText.retain();
			}
			
			public string DisplayName {get; private set;}
			public int Index {get; private set;}
			
			public string FileName {get; private set;}
			public string Path {get; private set;}
			
			public NSAttributedString DisplayText {get; private set;}
		}
		#endregion
		
		#region Private Methods
		private void DoReload()
		{
			Boss editor = ObjectModel.Create("DirectoryEditorPlugin");
			var finder = editor.Get<IFindDirectoryEditor>();
			
			foreach (RecentFile file in m_files)
			{
				file.DisplayText.release();
			}
			
			// Get all the recent files.
			int index = 0;
			NSArray array = NSDocumentController.sharedDocumentController().recentDocumentURLs();
			m_files = (from a in array
				let p = a.To<NSURL>().path().ToString()
					where File.Exists(p) && !p.Contains("/-Tmp-/")
				select new RecentFile(p, finder, ++index)).ToArray();
			
			// Use a reversed path for the name for any entries with duplicate names.
			Array.Sort(m_files, (lhs, rhs) => lhs.DisplayName.CompareTo(rhs.DisplayName));
			for (int i = 0; i < m_files.Length - 1; ++i)
			{
				string name = m_files[i].DisplayName;
				if (m_files[i + 1].DisplayName == name)
				{
					for (int j = i; j < m_files.Length && m_files[j].DisplayName == name; ++j)
					{
						RecentFile f = m_files[j];
						m_files[j] = new RecentFile(f, f.Path.ReversePath());
						f.DisplayText.release();
					}
				}
			}
			
			// Sort them and refresh the view.
			DoSort();
			m_table.reloadData();
		}
		
		// Note that sorting by date is tricky: what we really want to do is use the time the file was last opened
		// but there is no easy way to do that. The File.GetLastAccessTime method and the stat function can
		// be used as a proxy for this but they are kind of iffy because Spotlight and (I think) TimeMachine will
		// affect the access time.
		// 
		// So what we do when we want to sort by date is use the order the files were returned by 
		// recentDocumentURLs. What exactly this returns is poorly documented but it seems to be the
		// files in the order in which they were last opened.
		private void DoSort()
		{
			Array.Sort(m_files, (lhs, rhs) =>
			{
				int result;
				if (m_sortByDate)
					result = lhs.Index.CompareTo(rhs.Index);
				else
					result = lhs.DisplayName.CompareTo(rhs.DisplayName);
				return result;
			});
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			defaults.setBool_forKey(m_sortByDate, NSString.Create("recentFilesBrowserSortByDate"));
		}
		
		private void DoLoadPrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			m_sortByDate = defaults.boolForKey(NSString.Create("recentFilesBrowserSortByDate"));
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private RecentFile[] m_files = new RecentFile[0];
		private bool m_sortByDate = true;
		#endregion
	}
}
