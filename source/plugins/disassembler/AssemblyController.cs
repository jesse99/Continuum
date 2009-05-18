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
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;

namespace Disassembler
{
	[ExportClass("AssemblyController", "NSWindowController")]
	internal sealed class AssemblyController : NSWindowController
	{
		public AssemblyController(AssemblyDocument doc) : base(NSObject.AllocNative("AssemblyController"))
		{
			m_doc = doc;
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("disassembler"), this);
//			m_table = new IBOutlet<NSOutlineView>(this, "table");

//			window().setTitle(NSString.Create(m_name));
//			Unused.Value = window().setFrameAutosaveName(NSString.Create(window().title().ToString() + " editor"));
//			window().makeKeyAndOrderFront(this);
			
//			m_table.Value.setDoubleAction("doubleClicked:");
//			m_table.Value.setTarget(this);

			ActiveObjects.Add(this);
		}
		
//		public void doubleClicked(NSOutlineView sender)
//		{
//			DoOpenSelection();
//		}
		
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
		#endregion
		
		#region Fields
		private AssemblyDocument m_doc;
//		private IBOutlet<NSOutlineView> m_table;
		#endregion
	}
}
