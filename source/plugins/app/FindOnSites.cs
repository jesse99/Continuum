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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace App
{
	internal sealed class FindOnSites : ITextContextCommands
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;

			boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			
			handler.Register(this, 41, this.DoFindOnApple, this.DoNeedsFindOnApple);
			handler.Register(this, 42, this.DoFindOnMSDN, this.DoNeedsFindOnMSDN);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public void Get(Boss boss, string selection, List<TextContextItem> items)
		{
			if (selection != null && selection.Length < 100 && !selection.Any(c => char.IsWhiteSpace(c)))
			{
				items.Add(new TextContextItem(0.1f));

				if (DoNeedsFindOnApple(selection))
					items.Add(new TextContextItem("Find on Apple", this.DoFindOnApple, 0.1f));
	
				if (DoNeedsFindOnMSDN(selection))
					items.Add(new TextContextItem("Find on MSDN", this.DoFindOnMSDN, 0.1f));
			}
		}
				
		#region Private Methods
		private string DoGetWindowSelection()
		{
			string selection = null;
			
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();

			if (boss != null)
			{
				var text = boss.Get<IText>();
				NSRange sel = text.Selection;
				selection = text.Text.Substring(sel.location, sel.length);
			}

			return selection;
		}
		
		private bool DoNeedsFindOnApple()
		{
			string selection = DoGetWindowSelection();
			return selection != null && DoNeedsFindOnApple(selection);
		}
		
		private bool DoNeedsFindOnMSDN()
		{
			string selection = DoGetWindowSelection();
			return selection != null && DoNeedsFindOnMSDN(selection);
		}
		
		private void DoFindOnApple()
		{
			string selection = DoGetWindowSelection();
			if (selection != null)
				DoFindOnApple(selection);
		}
		
		private void DoFindOnMSDN()
		{
			string selection = DoGetWindowSelection();
			if (selection != null)
				DoFindOnMSDN(selection);
		}
		
		private string DoFindOnApple(string selection)
		{
			// We get better results if Objective-C method names are split into words.
			string name = selection;
			if (name.Length > 2 && char.IsLower(name[0]) && name[1] != '_')
			{
				name = name.Replace("_", "%20");
				name = name.Replace(":", "%20");
			}
			
			NSURL url = NSURL.URLWithString(NSString.Create("http://www.google.com/search?q=" + name + "%20site:developer.apple.com/documentation/Cocoa"));			// TODO: probably should use a pref for the sites
			Unused.Value = NSWorkspace.sharedWorkspace().openURL(url);
			
			return selection;
		}
		
		// // TODO: if we can figure out that the selection is a class or method then we should add that to our url
		private string DoFindOnMSDN(string selection)
		{
			NSURL url = NSURL.URLWithString(NSString.Create("http://www.google.com/search?q=" + selection + "%20site:msdn.microsoft.com/en-us/library"));
			Unused.Value = NSWorkspace.sharedWorkspace().openURL(url);
			
			return selection;
		}
		
		private bool DoNeedsFindOnApple(string selection)
		{
			bool valid = false;
			if (selection.Length > 3)
			{
				if (selection[0] == 'N' && selection[1] == 'S' && char.IsUpper(selection[2]))
					valid = true;

				else if (selection[0] == 'C' && selection[1] == 'F' && char.IsUpper(selection[2]))
					valid = true;

				else if (selection[0] == 'F' && selection[1] == 'S' && char.IsUpper(selection[2]))
					valid = true;

				else if (selection[0] == 'L' && selection[1] == 'S' && char.IsUpper(selection[2]))
					valid = true;

				else if (char.IsLower(selection[0]))
					valid = true;
			}
			
			if (valid)
				valid = selection.Any(c => !char.IsWhiteSpace(c));
			
			return valid;
		}
		
		private bool DoNeedsFindOnMSDN(string selection)
		{
			bool valid = selection.Length > 2 && !DoNeedsFindOnApple(selection);

			if (valid)
				valid = selection.Any(c => !char.IsWhiteSpace(c));
			
			return valid;
		}
		#endregion

		#region Fields
		private Boss m_boss; 
		#endregion
	} 
}