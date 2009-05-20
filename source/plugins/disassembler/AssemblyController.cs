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

using Gear;
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.IO;

namespace Disassembler
{
	[ExportClass("AssemblyController", "NSWindowController", Outlets = "table")]
	internal sealed class AssemblyController : NSWindowController
	{
		public AssemblyController(AssemblyDocument doc) : base(NSObject.AllocNative("AssemblyController"))
		{
			m_doc = doc;
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("disassembler"), this);
			window().setDelegate(this);

//			window().setTitle(NSString.Create(m_name));
//			Unused.Value = window().setFrameAutosaveName(NSString.Create(window().title().ToString() + " editor"));
//			window().makeKeyAndOrderFront(this);
			
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			ActiveObjects.Add(this);
			autorelease();							// get rid of the retain done by AllocNative
		}
		
		public void windowWillClose(NSObject notification)
		{
			m_table.setDelegate(null);
			
			window().release();
		}
		
		public void doubleClicked(NSOutlineView sender)
		{
			NSIndexSet selections = m_table.selectedRowIndexes();
			uint row = selections.firstIndex();
			while (row != Enums.NSNotFound)
			{
				AssemblyItem item = (AssemblyItem) (m_table.itemAtRow((int) row));
				DoOpen(item);
				
				row = selections.indexGreaterThanIndex(row);
			}
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, AssemblyItem item)
		{
			return item == null ? m_doc.Namespaces.Length : item.ChildCount;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, AssemblyItem item)
		{
			return item == null ? true : item.ChildCount > 0;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, AssemblyItem item)
		{
			return item == null ? m_doc.Namespaces[index] : item.GetChild(index);
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, AssemblyItem item)
		{
			return NSString.Create(item.Label);
		}
		
		#region Private Methods
		private void DoOpen(AssemblyItem item)
		{
			string text = item.GetText();
			if (text.Length > 0)
			{
				Boss boss = ObjectModel.Create("FileSystem");
				var fs = boss.Get<IFileSystem>();
				string file = fs.GetTempFile(item.Label.Replace(".", string.Empty), ".cil");
				
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", text);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			else
			{
				Functions.NSBeep();
			}
		}
		#endregion
		
		#region Fields
		private AssemblyDocument m_doc;
		private NSOutlineView m_table;
		#endregion
	}
}
