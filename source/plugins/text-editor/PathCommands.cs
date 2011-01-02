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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace TextEditor
{
	internal sealed class PathCommands : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, bool editable, List<TextContextItem> items)
		{
			if (selection == null)
			{
				var controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
				if (controller != null && controller.Path != null)
				{
					items.Add(new TextContextItem(0.89f));
					items.Add(new TextContextItem("Copy Path", this.DoCopy, 0.891f));
					items.Add(new TextContextItem("Show in Finder", this.DoShow, 0.892f));
				}
			}
		}
		
		#region Private Methods
		private string DoCopy(string selection)
		{
			var controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
			
			var text = NSString.Create(controller.Path);
			
			NSPasteboard pasteboard = NSPasteboard.generalPasteboard();
			pasteboard.clearContents();
			pasteboard.writeObjects(NSArray.Create(text));
			
			return selection;
		}
		
		private string DoShow(string selection)
		{
			var controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
			
			NSWorkspace.sharedWorkspace().selectFile_inFileViewerRootedAtPath(
				NSString.Create(controller.Path), NSString.Empty);
			
			return selection;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
