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

namespace Debugger
{
	[ExportClass("VariableView", "NSOutlineView")]
	internal sealed class VariableView : NSOutlineView
	{
		public VariableView(IntPtr instance) : base(instance)
		{
		}
		
		public new NSMenu menuForEvent(NSEvent evt)
		{
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
			
			NSMenu menu = SuperCall(NSOutlineView.Class, "menuForEvent:", evt).To<NSMenu>();
			
			return menu;
		}
	}
}
