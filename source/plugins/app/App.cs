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
using Gear.Helpers;
using MCocoa;
using MObjc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Shared;

namespace App
{
	[ExportClass("App", "NSApplication", Outlets = "buildMenu editMenu fileMenu refactorMenu sccsMenu searchMenu textMenu windowMenu")]
	internal sealed class App : NSApplication
	{
		public App(IntPtr instance) : base(instance)
		{
		}
		
		public new void addWindowsItem_title_filename(NSWindow window, NSString title, bool isFileName)
		{
			Unused.Value = SuperCall(NSApplication.Class, "addWindowsItem:title:filename:", window, title, isFileName);
			
			// Make directory windows bold.
			NSObject controller = window.windowController();
			if (controller.class_().Name == "DirectoryController")
			{
				NSMenuItem item = DoGetMenuItem(window);
				if (item != null)
				{
					var dict = NSMutableDictionary.Create();
					dict.setObject_forKey(NSFont.menuBarFontOfSize(0.0f), Externs.NSFontAttributeName);
					dict.setObject_forKey(NSNumber.Create(-5.0f), Externs.NSStrokeWidthAttributeName);
					
					var str = NSAttributedString.Alloc().initWithString_attributes(title, dict);
					str.autorelease();
					item.setAttributedTitle(str);
				}
			}
			
			// I tried making the path component of reversed paths gray, but it didn't always work and,
			// when it did, it was only for the first window name. Cocoa does adjust the attributes for
			// the menu item when it does things like underline dirty documents and I think that this
			// is resetting the foreground color.
		}
		
		public NSMenu buildMenu()
		{
			return this["buildMenu"].To<NSMenu>();
		}
		
		public NSMenu editMenu()
		{
			return this["editMenu"].To<NSMenu>();
		}
		
		public NSMenu fileMenu()
		{
			return this["fileMenu"].To<NSMenu>();
		}
		
		public NSMenu refactorMenu()
		{
			return this["refactorMenu"].To<NSMenu>();
		}
		
		public NSMenu sccsMenu()
		{
			return this["sccsMenu"].To<NSMenu>();
		}
		
		public NSMenu searchMenu()
		{
			return this["searchMenu"].To<NSMenu>();
		}
		
		public NSMenu textMenu()
		{
			return this["textMenu"].To<NSMenu>();
		}
		
		public NSMenu windowMenu()
		{
			return this["windowMenu"].To<NSMenu>();
		}
		
		public new void updateWindowsItem(NSWindow window)
		{
			Unused.Value = SuperCall(NSApplication.Class, "updateWindowsItem:", window);
			
			NSWindowController controller = window.windowController();
			NSObject obj = controller;
			
			// Underline dirty documents.
			if (obj.class_().Name == "TextController")
			{
				NSMenuItem item = DoGetMenuItem(window);
				if (item != null)
				{
					var dict = NSMutableDictionary.Create();
					dict.setObject_forKey(NSFont.menuBarFontOfSize(0.0f), Externs.NSFontAttributeName);
					if (controller.document().isDocumentEdited())		// note that we have to set the title even if the document isn't dirty to clear the underline
						dict.setObject_forKey(NSNumber.Create(1), Externs.NSUnderlineStyleAttributeName);
					
					var str = NSAttributedString.Alloc().initWithString_attributes(item.title(), dict);
					str.autorelease();
					item.setAttributedTitle(str);
				}
			}
		}
		
		// Note that Dictionary.app will fall back to wikipedia for phrases so we
		// want to allow spaces.
		public bool canLookupInDictionary(NSString text)
		{
			if (NSObject.IsNullOrNil(text))
				return false;
				
			if (text.length() == 0)
				return false;
				
			if (!char.IsLetter(text.characterAtIndex(0)))
				return false;
				
			if (text.Any(c => c == '\t' || c == '\r' || c == '\n'))
				return false;
				
			return true;
		}
		
		#region Private Methods
		private NSMenuItem DoGetMenuItem(NSWindow window)
		{
			NSMenuItem item = null;
			
			NSMenu menu = windowsMenu();
			int index = menu.indexOfItemWithTarget_andAction(window, "makeKeyAndOrderFront:");
			if (index >= 0)
				item = menu.itemAtIndex(index);
			
			return item;
		}
		#endregion
	}
}
