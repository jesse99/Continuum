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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Find
{
	[ExportClass("FindResultsController", "NSWindowController", Outlets = "view")]
	internal sealed class FindResultsController : NSWindowController, IObserver
	{
		public FindResultsController(FindAll find) : base(NSObject.AllocAndInitInstance("FindResultsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("find-results"), this);
			
			m_find = find;
			m_table = new IBOutlet<NSOutlineView>(this, "view");
			
			m_table.Value.setDoubleAction("doubleClicked:");
			m_table.Value.setTarget(this);
			m_table.Value.setDelegate(this);
			
			window().setDelegate(this);
			Unused.Value = window().setFrameAutosaveName(NSString.Create("find results window"));
			window().makeKeyAndOrderFront(null);
			
			Broadcaster.Register("text changed", this);
			NSApplication.sharedApplication().BeginInvoke(this.DoUpdate, TimeSpan.FromMilliseconds(50));
			
			ms_controllers.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "text changed":
					DoDocChanged((TextEdit) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void windowWillClose(NSNotification n)
		{
			m_closed = true;
			m_find.Cancelled = true;
			
			Broadcaster.Unregister(this);
			Unused.Value = ms_controllers.Remove(this);
			
			NSApplication.sharedApplication().BeginInvoke(this.DoRelease);
		}
		
		public void copy(NSObject sender)
		{
			m_table.Value.Copy();
		}
		
		// Note that we get nasty seg faults if we do this inside windowWillClose, but only
		// after mucking with the disclosure arrows and double clicking on a file.
		private void DoRelease()
		{
			release();
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
				Unused.Value = SuperCall(NSWindowController.Class, "keyDown:", evt);
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, NSObject item)
		{
			if (item == null)
				return m_finds.Length;
				
			FindsForFile result = item as FindsForFile;
			if (result != null)
				return result.Length;
			
			return 0;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, NSObject item)
		{
			if (item == null)
				return true;
				
			FindsForFile result = item as FindsForFile;
			return result != null && result.Length > 0;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, NSObject item)
		{
			if (item == null)
				return m_finds[index];
				
			FindsForFile result = item as FindsForFile;
			Contract.Assert(item != null, "item is null");
			Contract.Assert(result != null, "item is a " + item.GetType());
			
			return result[index];
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, NSObject item)
		{
			if (item == null)
				return NSString.Empty;
				
			FindsForFile result = item as FindsForFile;
			if (result != null)
				return result.StyledPath;
				
			FindInstance instance = item as FindInstance;
			if (instance != null)
				return instance.StyledContext;
				
			return item;
		}
			
		#region Private Methods
		private void DoDocChanged(TextEdit edit)
		{	
			var editor = edit.Boss.Get<ITextEditor>();
			string path = editor.Path;
			
			// Note that simply opening the file should not invalidate the finds.
			if (path != null && edit.UserEdit)
			{
				bool changed = false;
				
				foreach (FindsForFile finds in m_finds)
				{
					for (int i = 0; i < finds.Length; ++i)
					{
						if (Paths.AreEqual(finds[i].Path, path))
						{
							if (finds[i].Update(edit.EditedRange, edit.ChangeInLength))
								changed = true;
						}
					}
				}
				
				if (changed)
					window().display();
			}
		}
		
		private void DoOpenSelection()
		{
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			
			NSIndexSet selections = m_table.Value.selectedRowIndexes();
			uint row = selections.firstIndex();
			while (row != Enums.NSNotFound)
			{
				NSObject item = m_table.Value.itemAtRow((int) row);

				FindsForFile finds = item as FindsForFile;
				if (finds != null)
					launcher.Launch(finds.Path.description(), 1, 1, 1);
				
				FindInstance instance = item as FindInstance;
				if (instance != null)
					launcher.Launch(instance.Path, instance.Range);
				
				row = selections.indexGreaterThanIndex(row);
			}
		}
		
		private void DoUpdate()
		{
			if (!m_closed)
			{
				FindsForFile[] results = m_find.Results();
				if (results.Length != m_finds.Length)
				{
					m_finds = results;
					m_table.Value.reloadData();
					m_table.Value.expandItem_expandChildren(null, true);	// TODO: this isn't quite right, what we really want to do is ensure any new items start out opened
				}
				
				if (m_find.ProcessedCount >= m_find.FileCount)
				{
					int numMatches = m_finds.Aggregate(0, (total, next) => total + next.Length);
					if (numMatches == 1)
						window().setTitle(NSString.Create("{0} has one match.", m_find.Title));
					else
						window().setTitle(NSString.Create("{0} has {1} matches.", m_find.Title, numMatches));
				}
				else
				{
					window().setTitle(NSString.Create("{0} with {1} files left.", m_find.Title, m_find.FileCount - m_find.ProcessedCount));
					
					NSApplication.sharedApplication().BeginInvoke(this.DoUpdate, TimeSpan.FromSeconds(1));
				}
			}
		}
		#endregion
		
		#region Fields
		private FindAll m_find;
		private IBOutlet<NSOutlineView> m_table;
		private FindsForFile[] m_finds = new FindsForFile[0];
		private bool m_closed;
		
		private static List<FindResultsController> ms_controllers = new List<FindResultsController>();
		#endregion
	}
}
