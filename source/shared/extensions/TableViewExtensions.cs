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

using MCocoa;
using MObjc;
using System;

namespace Shared
{
	public static class TableViewExtensions
	{
		public static void Copy(this NSTableView table)
		{
			var tab = NSAttributedString.Create("\t");
			var newline = NSAttributedString.Create("\n");
			NSArray cols = table.tableColumns();
			
			var data = table.dataSource();
			NSMutableAttributedString text = NSMutableAttributedString.Create();
			for (int row = 0; row < table.numberOfRows(); ++row)
			{
				foreach (NSTableColumn col in cols)
				{
					var s = data.Call("tableView:objectValueForTableColumn:row:", table, col, row).To<NSObject>();
					if (s is NSAttributedString)
						text.appendAttributedString((NSAttributedString) s);
					else
						text.appendAttributedString(NSAttributedString.Create(s.ToString()));
					text.appendAttributedString(tab);
				}
				text.appendAttributedString(newline);
			}
			
			NSPasteboard pasteboard = NSPasteboard.generalPasteboard();
			pasteboard.clearContents();
			pasteboard.writeObjects(NSArray.Create(text));
		}
		
		public static void Copy(this NSOutlineView table)
		{
			var tab = NSAttributedString.Create("\t");
			var newline = NSAttributedString.Create("\n");
			NSArray cols = table.tableColumns();
			
			var data = table.dataSource();
			NSMutableAttributedString text = NSMutableAttributedString.Create();
			for (int row = 0; row < table.numberOfRows(); ++row)
			{
				NSObject item = table.itemAtRow(row);
				int level = table.levelForItem(item);
				for (int i = 0; i < level; ++i)
					text.appendAttributedString(tab);
				
				foreach (NSTableColumn col in cols)
				{
					var s = data.Call("outlineView:objectValueForTableColumn:byItem:", table, col, item).To<NSObject>();
					if (s is NSAttributedString)
						text.appendAttributedString((NSAttributedString) s);
					else
						text.appendAttributedString(NSAttributedString.Create(s.ToString()));
					text.appendAttributedString(tab);
				}
				text.appendAttributedString(newline);
			}
			
			NSPasteboard pasteboard = NSPasteboard.generalPasteboard();
			pasteboard.clearContents();
			pasteboard.writeObjects(NSArray.Create(text));
		}
	}
}
