// Copyright (C) 2009 Jesse Jones
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
using System.Text.RegularExpressions;

namespace Shared
{
	[ExportClass("GetItemController", "NSWindowController", Outlets = "table okButton")]
	internal sealed class GetItemController : NSWindowController
	{
		public GetItemController() : base(NSObject.AllocAndInitInstance("GetItemController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("get-item"), this);	
			Unused.Value = window().setFrameAutosaveName(NSString.Create("get-item window"));
			
			m_okButton = new IBOutlet<NSButton>(this, "okButton").Value;
			m_table = new IBOutlet<NSTableView>(this, "table").Value;
			
			m_table.setDoubleAction("doubleClicked:");
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnDealloc()
		{
			if (m_items != null)
				m_items.release();
				
			base.OnDealloc();
		}
		
		public string Title
		{
			get {return window().title().description();}
			set {window().setTitle(NSString.Create(value));}
		}
		
		public bool AllowsMultiple
		{
			get {return m_table.allowsMultipleSelection();}
			set {m_table.setAllowsMultipleSelection(value);}
		}
		
		public string[] Items
		{
			set
			{
				if (m_items != null)
					m_items.release();
				
				m_items = NSArray.Create(value).Retain();
			}
		}
		
		public string[] Run()
		{
			string[] result = null;
			
			m_table.reloadData();
			DoUpdateButtons();
			
			int button = NSApplication.sharedApplication().runModalForWindow(window());
			if (button == Enums.NSOKButton)
			{
				NSIndexSet indexes = m_table.selectedRowIndexes();
				result = new string[indexes.count()];
				
				int i = 0;
				uint index = indexes.firstIndex();
				while (index < Enums.NSNotFound)
				{
					result[i++] = m_items.objectAtIndex((uint) index).description();
					index = indexes.indexGreaterThanIndex(index);
				}
			}
			
			return result;
		}
		
		public void doubleClicked(NSTableView sender)
		{
			int row = m_table.selectedRow();
			if (row >= 0)
				pressedOK(this);
		}
		
		public void tableViewSelectionDidChange(NSNotification notification)
		{
			DoUpdateButtons();
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_items != null ? (int) m_items.count() : 0;
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSObject col, int row)
		{
			return m_items.objectAtIndex((uint) row);
		}
		
		public void pressedOK(NSObject sender)
		{
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
			window().orderOut(this);
		}
		
		public void pressedCancel(NSObject sender)
		{
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSCancelButton);
			window().orderOut(this);
		}
		
		#region Private Methods
		private void DoUpdateButtons()
		{
			int row = m_table.selectedRow();
			m_okButton.setEnabled(row >= 0);
		}
		#endregion
		
		#region Fields
		private NSTableView m_table;
		private NSButton m_okButton;
		private NSArray m_items;
		#endregion
	}
}
