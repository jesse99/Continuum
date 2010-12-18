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
			
			DoReload();
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			Broadcaster.Register("opening document window", this);
			Broadcaster.Register("opening directory", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opening document window":	// this will change the items we show
				case "opened directory":				// this may change the colors used by the items
					DoReload();
					m_table.reloadData();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void Show()
		{
			window().makeKeyAndOrderFront(this);
		}
		
		public void doubleClicked(NSObject sender)
		{
			Boss boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			foreach (uint row in m_table.selectedRowIndexes())
			{
				launcher.Launch(m_files[(int) row].Path, -1, -1, 1);
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_files != null ? m_files.Length : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSObject col, int row)
		{
			RecentFile file = m_files[row];
			if (file.Color != null)
			{
				NSColor color = file.Color.GetColor(Path.GetFileName(file.Path));
				var attrs = NSDictionary.dictionaryWithObject_forKey(color, Externs.NSForegroundColorAttributeName);
				return NSAttributedString.Create(file.Name, attrs);
			}
			else
			{
				return NSString.Create(file.Name);
			}
		}
		
		#region Private Types
		private sealed class RecentFile
		{
			public RecentFile(string path, IFindDirectoryEditor finder)
			{
				Name = System.IO.Path.GetFileName(path);
				Path = path;
				
				// File name colors are associated with directory editors so coloring will only
				// happen if the appropiate directory is being edited.
				Boss editor = finder.GetDirectoryEditor(path);
				if (editor != null)
					Color = editor.Get<IFileColor>();
			}
			
			public string Name {get; set;}
			
			public string Path {get; set;}
			
			public IFileColor Color {get; set;}
		}
		#endregion
		
		#region Private Methods
		private void DoReload()
		{
			Boss editor = ObjectModel.Create("DirectoryEditorPlugin");
			var finder = editor.Get<IFindDirectoryEditor>();
			
			// Get all the recent files.
			NSArray array = NSDocumentController.sharedDocumentController().recentDocumentURLs();
			m_files = (from a in array let p = a.To<NSURL>().path().ToString() where File.Exists(p) select new RecentFile(p, finder)).ToArray();
			
			// Sort them by name and then by path.
			Array.Sort(m_files, (lhs, rhs) =>
			{
				int result = lhs.Name.CompareTo(rhs.Name);
				if (result == 0)
					result = lhs.Path.CompareTo(rhs.Path);
				return result;
			});
			
			// Use a reversed path for the name for any entries with duplicate names.
			for (int i = 0; i < m_files.Length - 1; ++i)
			{
				string name = m_files[i].Name;
				if (m_files[i + 1].Name == name)
				{
					for (int j = i; j < m_files.Length && m_files[j].Name == name; ++j)
					{
						m_files[j].Name = m_files[j].Path.ReversePath();
					}
				}
			}
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private RecentFile[] m_files;
		#endregion
	}
}
