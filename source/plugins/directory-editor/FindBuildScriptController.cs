// Copyright (C) 2011 Jesse Jones
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

namespace DirectoryEditor
{
	[ExportClass("FindBuildScriptController", "NSWindowController")]
	internal sealed class FindBuildScriptController : NSWindowController
	{
		public FindBuildScriptController() : base(NSObject.AllocAndInitInstance("FindBuildScriptController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("FindBuildScript"), this);	
			Unused.Value = window().setFrameAutosaveName(NSString.Create("FindBuildScript window"));
		}
		
		public bool Generate {get; private set;}
		
		public string Path {get; private set;}
		
		public void Run()
		{
			Unused.Value = NSApplication.sharedApplication().runModalForWindow(window());
		}
		
		public void generatePressed(NSObject sender)
		{
			Generate = true;
			
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
			window().orderOut(this);
		}
		
		public void searchPressed(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Choose Build Script"));
			panel.setCanChooseDirectories(false);
			panel.setCanChooseFiles(true);
			panel.setAllowsMultipleSelection(false);
			
			int button = panel.runModal();
			if (button == Enums.NSOKButton && panel.URLs().count() == 1)
			{
				NSURL url = panel.URL();
				Path = url.path().ToString();
				
				NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
				window().orderOut(this);
			}
		}
		
		public void ignorePressed(NSObject sender)
		{
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSCancelButton);
			window().orderOut(this);
		}
	}
}
