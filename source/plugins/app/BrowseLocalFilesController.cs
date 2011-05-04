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
	[ExportClass("BrowseLocalFilesController", "NSWindowController", Outlets = "table searchField search progress")]
	internal sealed class BrowseLocalFilesController : NSWindowController, IObserver
	{
		public BrowseLocalFilesController() : base(NSObject.AllocAndInitInstance("BrowseLocalFilesController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("BrowseLocalFiles"), this);
			
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			m_searchField =  new IBOutlet<NSSearchField>(this, "searchField").Value;
			m_search =  new IBOutlet<NSSearchFieldCell>(this, "search").Value;
			m_spinner =  new IBOutlet<NSProgressIndicator>(this, "progress").Value;
			
			Broadcaster.Register("opened directory", this);
			Broadcaster.Register("closed directory", this);
			Broadcaster.Register("directory changed", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opened directory":
				case "closed directory":
				case "directory changed":
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
			window().makeFirstResponder(m_searchField);
		}
		
		public void onSearch(NSObject sender)
		{
			m_filter = m_search.stringValue().ToString().Trim();
			DoFilter();
		}
		
		public void openWithFinder(NSObject sender)
		{
			uint count = m_table.selectedRowIndexes().count();
			if (NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
			{
				foreach (uint row in m_table.selectedRowIndexes())
				{
					string path = m_files[(int) row].FullPath;
					NSWorkspace.sharedWorkspace().openFile(NSString.Create(path));
				}
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
			uint count = m_table.selectedRowIndexes().count();
			if (NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
			{
				Boss boss = Gear.ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				foreach (uint row in m_table.selectedRowIndexes())
				{
					launcher.Launch(m_files[(int) row].FullPath, -1, -1, 1);
				}
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_files != null ? m_files.Count : 0;
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
		[ThreadModel(ThreadModel.Concurrent)]
		private struct LocalFile
		{
			public LocalFile(ThreadedFile file) : this()
			{
				FullPath = file.FullPath;
				RelativePath = file.RelativePath;
				FileName = file.FileName;
				
				DisplayText = NSAttributedString.Create(FileName, Externs.NSForegroundColorAttributeName, file.Color);
				DisplayText.retain();
			}
			
			public LocalFile(LocalFile file, string displayName) : this()
			{
				FullPath = file.FullPath;
				RelativePath = file.RelativePath;
				FileName = file.FileName;
				
				NSDictionary attrs = file.DisplayText.fontAttributesInRange(new NSRange(0, 1));
				DisplayText = NSAttributedString.Create(displayName, attrs);
				DisplayText.retain();
			}
			
			public string FileName {get; private set;}
			public string RelativePath {get; private set;}
			public string FullPath {get; private set;}
			public NSAttributedString DisplayText {get; private set;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private struct ThreadedFile
		{
			public ThreadedFile(string fullPath, string relativePath, NSColor color) : this()
			{
				FullPath = fullPath;
				RelativePath = relativePath;
				FileName = System.IO.Path.GetFileName(relativePath);
				Color = color;
			}
			
			public string FileName {get; private set;}
			public string RelativePath {get; private set;}
			public string FullPath {get; private set;}
			public NSColor Color {get; private set;}
		}
		#endregion
		
		#region Private Methods
		private void DoReload()
		{
			Boss plugin = ObjectModel.Create("DirectoryEditorPlugin");
			var windows = plugin.Get<IWindows>();
			
			var roots = new List<IGetFiles>();
			foreach (Boss dir in windows.All())
			{
				var getter = dir.Get<IGetFiles>();
				roots.Add(getter);
			}
			
			// For now we'll queue up a second thread even if one is already queued.
			// In the future it might be better to abort the old thread.
			++m_queued;
			m_spinner.startAnimation(this);
			ThreadPool.QueueUserWorkItem(o => DoGetCandidates(roots));
		}
		
		private void DoRefresh(List<LocalFile> candidates)
		{
			foreach (LocalFile file in m_candidates)
			{
				file.DisplayText.release();
			}
			m_candidates = candidates;
			
			if (--m_queued == 0)
				m_spinner.stopAnimation(this);
			DoFilter();
		}
		
		private void DoFilter()
		{
			m_files.Clear();
			
			if (!string.IsNullOrEmpty(m_filter))
			{
				foreach (LocalFile file in m_candidates)
				{
					List<NSRange> matches = DoGetMatches(file.FileName);
					if (matches != null)
						m_files.Add(file);
				}
			}
			else
			{
				m_files.AddRange(m_candidates);
			}
			
			m_table.reloadData();
		}
		
		private List<NSRange> DoGetMatches(string fileName)
		{
			Contract.Requires(m_filter != null && m_filter.Length > 0);
			
			// We have a match if the search string is within the file name,
			int index = fileName.IndexOf(m_filter, StringComparison.OrdinalIgnoreCase);
			if (index >= 0)
				return new List<NSRange>{new NSRange(index, m_filter.Length)};
			
			// or each character in the search string matches consecutive upper
			// case letters in the file name (e.g. "tv" will match "NSTextView").
			var matches = new List<NSRange>();
			index = fileName.IndexOf(char.ToUpper(m_filter[0]));
			if (index >= 0)
			{
				int i = 0;
				int j = index - 1;
				while (j < fileName.Length)
				{
					j = fileName.IndexOf(char.ToUpper(m_filter[i]), j + 1);
					if (j >= 0)
					{
						matches.Add(new NSRange(j, 1));
						if (++i == m_filter.Length)
							return matches;
					}
					else
						break;
				}
			}
			
			return null;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoGetCandidates(IEnumerable<IGetFiles> roots)
		{
			var candidates = new List<LocalFile>();
			var pool = NSAutoreleasePool.Create();
			
			try
			{
				// Get all the files within the directories being edited.
				var threaded = new List<ThreadedFile>();
				
				var files = new List<string>();
				var colors = new List<NSColor>();
				foreach (IGetFiles getter in roots)
				{
					files.Clear();
					colors.Clear();
					getter.GetFiles(files, colors);
					
					for (int i = 0; i < files.Count; ++i)
					{
						string relativePath = files[i].Substring(Path.GetDirectoryName(getter.Path).Length);	// use the path up to, and including, the directory being edited
						threaded.Add(new ThreadedFile(files[i], relativePath, colors[i]));
					}
				}
				
				// Use a reversed path for the name for any entries with duplicate names.
				candidates = (from t in threaded select new LocalFile(t)).ToList();
				candidates.Sort((lhs, rhs) => lhs.FileName.CompareTo(rhs.FileName));
				for (int i = 0; i < candidates.Count - 1; ++i)
				{
					string name = candidates[i].FileName;
					if (candidates[i + 1].FileName == name)
					{
						for (int j = i; j < candidates.Count && candidates[j].FileName == name; ++j)
						{
							LocalFile f = candidates[j];
							candidates[j] = new LocalFile(f, f.RelativePath.ReversePath());
							f.DisplayText.release();
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
			pool.release();
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
		private NSSearchField m_searchField;
		private NSSearchFieldCell m_search;
		private NSProgressIndicator m_spinner;
		private List<LocalFile> m_candidates = new List<LocalFile>();	// all the files under the directories being edited
		private List<LocalFile> m_files = new List<LocalFile>();			// the files matching the current search pattern
		private int m_queued;
		private string m_filter;
		#endregion
	}
}
