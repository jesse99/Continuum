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
using System.Threading;

namespace App
{
	[ExportClass("BrowseLocalFilesController", "NSWindowController", Outlets = "table search progress")]
	internal sealed class BrowseLocalFilesController : NSWindowController, IObserver
	{
		public BrowseLocalFilesController() : base(NSObject.AllocAndInitInstance("BrowseLocalFilesController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("BrowseLocalFiles"), this);
			
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			m_search =  new IBOutlet<NSSearchFieldCell>(this, "search").Value;
			m_spinner =  new IBOutlet<NSProgressIndicator>(this, "progress").Value;
			
			Broadcaster.Register("opened directory", this);
			Broadcaster.Register("closed directory", this);
			Broadcaster.Register("directory changed", this);
			
//			DoLoadPrefs();
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opened directory":
				case "closed directory":
				case "directory changed":
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
		
		public void onSearch(NSObject sender)
		{
			m_filter = m_search.stringValue().ToString().Trim();
			DoFilter();
		}
		
		public void openWithFinder(NSObject sender)
		{
			foreach (uint row in m_table.selectedRowIndexes())
			{
				string path = m_files[(int) row].FullPath;
				NSWorkspace.sharedWorkspace().openFile(NSString.Create(path));
			}
		}
		
		public void showInFinder(NSObject sender)
		{
			foreach (uint row in m_table.selectedRowIndexes())
			{
				string path = m_files[(int) row].FullPath;
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
			Boss boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			foreach (uint row in m_table.selectedRowIndexes())
			{
				launcher.Launch(m_files[(int) row].FullPath, -1, -1, 1);
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_files != null ? m_files.Count : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSObject col, int row)
		{
			LocalFile file = m_files[row];
			if (file.Color != null)
			{
				var attrs = NSDictionary.dictionaryWithObject_forKey(file.Color, Externs.NSForegroundColorAttributeName);
				return NSAttributedString.Create(file.DisplayName, attrs);
			}
			else
			{
				return NSString.Create(file.DisplayName);
			}
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
			if (sel.Name == "openWithFinder:" || sel.Name == "showInFinder:")
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
		private sealed class LocalFile
		{
			public LocalFile(string fullPath, string relativePath, IDirectoryEditor editor)
			{
				FullPath = fullPath;
				RelativePath = relativePath;
				FileName = System.IO.Path.GetFileName(relativePath);
				DisplayName = FileName;
				Editor = editor;
			}
			
			public string DisplayName {get; set;}
			
			public string FileName {get; private set;}
			public string RelativePath {get; private set;}
			public string FullPath {get; private set;}
			
			public IDirectoryEditor Editor {get; private set;}
			public NSColor Color {get; set;}
		}
		#endregion
		
		#region Private Methods
		private void DoReload()
		{
			Boss plugin = ObjectModel.Create("DirectoryEditorPlugin");
			var windows = plugin.Get<IWindows>();
			
			var roots = new List<KeyValuePair<string, IDirectoryEditor>>();
			foreach (Boss dir in windows.All())
			{
				var editor = dir.Get<IDirectoryEditor>();
				roots.Add(new KeyValuePair<string, IDirectoryEditor>(editor.Path, editor));	// we pass the path in because the editor is not thread safe
			}
			
			// For now we'll queue up a second thread even if one is already queued.
			// In the future it might be better to abort the old thread.
			++m_queued;
			m_spinner.startAnimation(this);
			ThreadPool.QueueUserWorkItem(o => DoGetCandidates(roots));
		}
		
		// We have to check each component to catch things like .svn directories.
		// TODO: might be better to make IDirectoryEditor.IsIgnored thead safe
		// so that we can avoid recursing into ignored directories.
		private bool DoIsIgnored(IDirectoryEditor editor, string path)
		{
			string[] components = path.Split(new char[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
			return components.Any(c => editor.IsIgnored(c) || c == "bin");
		}
		
		private void DoRefresh(List<LocalFile> candidates)
		{
			m_candidates = candidates.Where(c => !DoIsIgnored(c.Editor, c.RelativePath));
			foreach (LocalFile file in m_candidates)
			{
				file.Color = file.Editor.Boss.Get<IFileColor>().GetColor(file.FileName);
			}
			
			if (--m_queued == 0)
				m_spinner.stopAnimation(this);
			DoFilter();
		}
		
		private void DoFilter()
		{
			if (!string.IsNullOrEmpty(m_filter))
				m_files = (from c in m_candidates where c.FileName.StartsWith(m_filter, true, null) select c).ToList();
			else
				m_files = m_candidates.ToList();
			m_table.reloadData();
		}
		
		// Instead of using a thread to get the files we could ask the directory editor for them. This
		// isn't so good though because the directory editor lazily loads files to minimize the number
		// of table entries and to avoid blocking the main thread.
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoGetCandidates(IEnumerable<KeyValuePair<string, IDirectoryEditor>> roots)
		{
			var candidates = new List<LocalFile>();
			
			try
			{
				// Get all the files within the directories being edited.
				foreach (var entry in roots)
				{
					foreach (string file in Directory.GetFiles(entry.Key, "*", SearchOption.AllDirectories))
					{
						string path = file.Substring(Path.GetDirectoryName(entry.Key).Length);	// use the path up to, and including, the directory being edited
						candidates.Add(new LocalFile(file, path, entry.Value));
					}
				}
				
				// Use a reversed path for the name for any entries with duplicate names.
				candidates.Sort((lhs, rhs) => lhs.DisplayName.CompareTo(rhs.DisplayName));
				for (int i = 0; i < candidates.Count - 1; ++i)
				{
					string name = candidates[i].DisplayName;
					if (candidates[i + 1].DisplayName == name)
					{
						for (int j = i; j < candidates.Count && candidates[j].DisplayName == name; ++j)
						{
							candidates[j].DisplayName = candidates[j].RelativePath.ReversePath();
						}
					}
				}
			}
			catch (Exception e)
			{
				string err = string.Format("Error getting local files: {0}", e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => DoShowError(err));
			}
			
			NSApplication.sharedApplication().BeginInvoke(() => DoRefresh(candidates));
		}
		
		private void DoShowError(string err)
		{
			Boss boss = ObjectModel.Create("Application");
			
			var transcript = boss.Get<ITranscript>();
			transcript.Show();
			transcript.WriteLine(Output.Error, err);
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private NSSearchFieldCell m_search;
		private NSProgressIndicator m_spinner;
		private IEnumerable<LocalFile> m_candidates;	// all the files under the directories being edited
		private List<LocalFile> m_files;						// the files matching the current search pattern
		private int m_queued;
		private string m_filter;
		#endregion
	}
}
