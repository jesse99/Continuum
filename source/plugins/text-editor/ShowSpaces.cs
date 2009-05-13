// Copyright (C) 2008-2009 Jesse Jones
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
	internal sealed class ShowSpaces : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(Boss boss, string selection, List<TextContextItem> items)
		{
			if (selection == null)
			{
				TextController controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
				if (controller != null && controller.StylesWhitespace)
				{
					Boss b = ObjectModel.Create("Stylers");
					var white = b.Get<IWhitespace>();
					
					items.Add(new TextContextItem(0.85f));
					
					items.Add(new TextContextItem(white.ShowSpaces ? "Hide Spaces" : "Show Spaces", this.DoToggleSpaces, 0.851f));
					items.Add(new TextContextItem(white.ShowTabs ? "Hide Tabs" : "Show Tabs", this.DoToggleTabs, 0.852f));
					items.Add(new TextContextItem(controller.WrapsWords ? "Don't Wrap Words" : "Wrap Words", this.DoToggleWordWrap, 0.853f));
				}
			}
		}
		
		#region Private Methods
		private string DoToggleSpaces(string selection)
		{
			TextController controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
			controller.showSpaces(null);
			
			return selection;
		}
		
		private string DoToggleTabs(string selection)
		{
			TextController controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
			controller.showTabs(null);
			
			return selection;
		}
		
		private string DoToggleWordWrap(string selection)
		{
			TextController controller = NSApplication.sharedApplication().mainWindow().windowController() as TextController;
			controller.toggleWordWrap(null);
			
			return selection;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
