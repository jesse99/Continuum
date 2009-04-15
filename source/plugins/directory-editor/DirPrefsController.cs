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

using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DirectoryEditor	
{
	[ExportClass("DirPrefsController", "NSWindowController", Outlets = "addSpace sheet ignoredItems ignoredTargets pathColor files1Color files2Color files3Color files4Color files5Color files6Color files1Globs files2Globs files3Globs files4Globs files5Globs files6Globs")]
	internal sealed class DirPrefsController : NSWindowController
	{
		private DirPrefsController(IntPtr instance) : base(instance)
		{
			m_sheet = new IBOutlet<NSWindow>(this, "sheet");
			m_ignoredTargets = new IBOutlet<NSTextField>(this, "ignoredTargets");
			m_ignoredItems = new IBOutlet<NSTextField>(this, "ignoredItems");
			m_addSpace = new IBOutlet<NSButton>(this, "addSpace");
			
			ActiveObjects.Add(this);
		}
		
		public void Open(DirectoryController dir)
		{
			m_dir = dir;
			
			// set ignored targets
			string s = Glob.Join(m_dir.IgnoredTargets);
			m_ignoredTargets.Value.setStringValue(NSString.Create(s));
			
			// set ignored items
			s = Glob.Join(m_dir.IgnoredItems);
			m_ignoredItems.Value.setStringValue(NSString.Create(s));
			
			// set add space
			m_addSpace.Value.setState(m_dir.AddSpace ? 1 : 0);
			
			// set path color
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string path = dir.Path;
			
			var data = defaults.objectForKey(NSString.Create(path + "-path color")).To<NSData>();
			var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
			Unused.Value = this["pathColor"].Call("setColor:", color);
			
			// set file colors/globs
			for (int i = 1; i <= DirectoryItemStyler.FilesCount; ++i)
			{
				data = defaults.objectForKey(NSString.Create(path + "-files" + i + " color")).To<NSData>();
				color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
				this["files" + i + "Color"].Call("setColor:", color);
				
				var globs = defaults.stringForKey(NSString.Create(path + "-files" + i + " globs"));
				this["files" + i + "Globs"].Call("setStringValue:", globs);
			}
			
			// display the sheet
			NSApplication.sharedApplication().beginSheet_modalForWindow_modalDelegate_didEndSelector_contextInfo(
				m_sheet.Value, m_dir.window(), this, null, IntPtr.Zero);
		}
		
		public void okPressed(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().endSheet(m_sheet.Value);
			m_sheet.Value.orderOut(this);
			
			// save ignored targets
			string s = m_ignoredTargets.Value.stringValue().description();
			m_dir.IgnoredTargets = Glob.Split(s);
			
			// save ignored items
			s = m_ignoredItems.Value.stringValue().description();
			m_dir.IgnoredItems = Glob.Split(s);
			
			// save add space
			m_dir.AddSpace = m_addSpace.Value.state() == 1;
			
			// save path color
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string path = m_dir.Path;
			
			var color = this["pathColor"].Call("color").To<NSObject>();
			var data = NSArchiver.archivedDataWithRootObject(color);
			defaults.setObject_forKey(data, NSString.Create(path + "-path color"));
			
			// save file colors/globs
			for (int i = 1; i <= DirectoryItemStyler.FilesCount; ++i)
			{
				color = this["files" + i + "Color"].Call("color").To<NSObject>();
				data = NSArchiver.archivedDataWithRootObject(color);
				defaults.setObject_forKey(data, NSString.Create(path + "-files" + i + " color"));
				
				var globs = this["files" + i + "Globs"].Call("stringValue").To<NSObject>();
				defaults.setObject_forKey(globs, NSString.Create(path + "-files" + i + " globs"));
			}
			
			// force the table to reload
			m_dir.Reload();
			
			Broadcaster.Invoke("directory prefs changed", m_dir.Boss);
			m_dir = null;
		}
		
		public void cancelPressed(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().endSheet(m_sheet.Value);
			m_sheet.Value.orderOut(this);
			m_dir = null;
		}
		
		#region Fields
		private IBOutlet<NSWindow> m_sheet;
		private IBOutlet<NSTextField> m_ignoredTargets;
		private IBOutlet<NSTextField> m_ignoredItems;
		private IBOutlet<NSButton> m_addSpace;
		private DirectoryController m_dir;
		#endregion
	}
}
