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

using MCocoa;
using Gear;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DirectoryEditor
{
	[ExportClass("DummyUserInterfaceItem", "NSObject")]
	internal sealed class DummyUserInterfaceItem : NSObject
	{
		public DummyUserInterfaceItem(string selector) : base(NSObject.AllocNative("DummyUserInterfaceItem"))
		{
			m_selector = new Selector(selector);
			autorelease();
			
			ActiveObjects.Add(this);
		}
		
		public Selector action()
		{
			return m_selector;
		}
		
		public int tag()
		{
			return 0;
		}
		
		private Selector m_selector;
	}
	
	[ExportClass("DirectoryView", "NSOutlineView")]
	internal sealed class DirectoryView : NSOutlineView
	{
		public DirectoryView(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
			
			NSMenu menu = NSMenu.Alloc().initWithTitle(NSString.Empty);
			menu.autorelease();
			
			// Make the window the key window (handleSccs expects that it
			// is either the main or the key window).
			window().makeKeyAndOrderFront(this);
			
			// If the item clicked on is not selected then select it.
			NSPoint baseLoc = evt.locationInWindow();
			NSPoint viewLoc = convertPointFromBase(baseLoc);
			int row = rowAtPoint(viewLoc);
			if (row >= 0)
			{
				if (!isRowSelected(row))
				{
					var indexes = NSIndexSet.indexSetWithIndex((uint) row);	
					selectRowIndexes_byExtendingSelection(indexes, false);
				}
			}
			
			// Find the items which are selected.
			var paths = new List<string>();
			foreach (uint index in selectedRowIndexes())
			{
				DirectoryItem item = (DirectoryItem) (itemAtRow((int) index));
				paths.Add(item.Path);
			}
			
			// Build the menu.
			Dictionary<string, string[]> commands = Sccs.GetCommands(paths);
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
				}
			}
			
			return menu;
		}
	}
}
