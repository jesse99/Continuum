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

using Gear.Helpers;
using MCocoa;
using MObjc;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Find
{
	[ExportClass("FindInFilesOptionsController", "NSWindowController", Outlets = "dirsTable canRemove")]
	internal sealed class FindInFilesOptionsController : NSWindowController
	{
		public FindInFilesOptionsController(FindInFilesController find) : base(NSObject.AllocAndInitInstance("FindInFilesOptionsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("find-in-files-options"), this);
			m_find = find;
			
			m_canRemove = new IBOutlet<NSNumber>(this, "canRemove");
			this.willChangeValueForKey(NSString.Create("canRemove"));
			m_canRemove.Value = NSNumber.Create(true);
			this.didChangeValueForKey(NSString.Create("canRemove"));
			
			m_dirsTable = new IBOutlet<NSTableView>(this, "dirsTable");
			m_dirsTable.Value.setDelegate(this);
			
			Unused.Value = window().setFrameAutosaveName(NSString.Create("find in files options panel"));
		}
		
		public void addDir(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.openPanel();
			panel.setCanChooseFiles(false);
			panel.setCanChooseDirectories(true);
			panel.setAllowsMultipleSelection(true);
			panel.setCanCreateDirectories(false);
			
			int result = panel.runModalForDirectory_file_types(null, null, null);
			if (result == Enums.NSOKButton && panel.filenames().count() > 0)
			{
				NSMutableArray dirs = NSMutableArray.Create();
				
				NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
				dirs.addObjectsFromArray(defaults.arrayForKey(NSString.Create("default find directories")));
				
				foreach (NSString path in panel.filenames())
				{
					if (!dirs.containsObject(path))
						dirs.addObject(path);
				}
				
				defaults.setObject_forKey(dirs, NSString.Create("default find directories"));
				m_find.AddDefaultDirs();
			}
		}
		
		public void removeDirs(NSObject sender)
		{
			NSIndexSet selections = m_dirsTable.Value.selectedRowIndexes();
			NSMutableArray dirs = NSMutableArray.Create();
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			dirs.addObjectsFromArray(defaults.arrayForKey(NSString.Create("default find directories")));
			
			dirs.removeObjectsAtIndexes(selections);
			defaults.setObject_forKey(dirs, NSString.Create("default find directories"));
		}
		
		public void restoreDefaults(NSObject sender)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			NSMutableArray dirs = NSMutableArray.Create();
			foreach (string path in Startup.DefaultDirs)
			{
				dirs.addObject(NSString.Create(path));
			}
			defaults.setObject_forKey(dirs, NSString.Create("default find directories"));
			
			defaults.setObject_forKey(NSString.Create(Startup.DefaultInclude), NSString.Create("default include glob"));
			defaults.setObject_forKey(NSString.Create(Startup.AlwaysExclude), NSString.Create("always exclude globs"));
		}
		
		// TODO: it would be nice to allow the user to reorder rows in the table
		// by dragging them around, but this seems to be a bit difficult, see
		// http://www.cocoadev.com/index.pl?NSTableView
		public void tableViewSelectionDidChange(NSNotification notification)
		{
			this.willChangeValueForKey(NSString.Create("canRemove"));
			
			NSIndexSet selections = m_dirsTable.Value.selectedRowIndexes();
			m_canRemove.Value = NSNumber.Create(selections.count() > 0);
			
			this.didChangeValueForKey(NSString.Create("canRemove"));
		}
		
		public bool validateUserInterfaceItem(NSObject item)
		{
			Selector sel = (Selector) item.Call("action");
			
			bool valid = false;
			if (sel.Name == "removeDirs:")
			{
				NSIndexSet selections = m_dirsTable.Value.selectedRowIndexes();
				valid = selections.count() > 0;
			}
			else if (respondsToSelector(sel))
			{
				valid = true;
			}
			else if (SuperCall(NSWindowController.Class, "respondsToSelector:", new Selector("validateUserInterfaceItem:")).To<bool>())
			{
				valid = SuperCall(NSWindowController.Class, "validateUserInterfaceItem:", item).To<bool>();
			}
			
			return valid;
		}
		
		#region Fields
		private FindInFilesController m_find;
		private IBOutlet<NSTableView> m_dirsTable;
		private IBOutlet<NSNumber> m_canRemove;
		#endregion
	}
}
