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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Runtime.InteropServices;

namespace App
{
	[ExportClass("DocumentController", "NSDocumentController")]
	internal sealed class DocumentController : NSDocumentController
	{
		private DocumentController(IntPtr instance) : base(instance)
		{
		}
		
		public new void init()
		{
			SuperCall(NSDocumentController.Class, "init");
			setAutosavingDelay(10.0);
		}
		
		public NSObject openDocumentWithContentsOfURL_display_error(NSURL absoluteURL, bool displayDocument, IntPtr outError)
		{
			NSObject result = null;
			
			string path = absoluteURL.path().ToString();
			if (System.IO.Directory.Exists(path))
			{
				try
				{
					Boss boss = ObjectModel.Create("DirectoryEditorPlugin");	
					var open = boss.Get<IOpen>();
					result = open.Open(path).To<NSObject>();
				}
				catch (Exception e)
				{
					NSMutableDictionary userInfo = NSMutableDictionary.Create();
					userInfo.setObject_forKey(NSString.Create("Couldn't open '{0}", path), Externs.NSLocalizedDescriptionKey);
					userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
					
					NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 2, userInfo);
					Marshal.WriteIntPtr(outError, error);
				}
			}
			else
			{
				result = SuperCall(NSDocumentController.Class, "openDocumentWithContentsOfURL:display:error:",
					absoluteURL, displayDocument, outError).To<NSDocument>();
			}
			
			return result;
		}
		
		// This is called when the user select open from the file menu.
		public new int runModalOpenPanel_forTypes(NSOpenPanel openPanel, NSArray types)
		{
			openPanel.setTreatsFilePackagesAsDirectories(true);
			
			int result = SuperCall(NSDocumentController.Class, "runModalOpenPanel:forTypes:", openPanel, types).To<int>();
			
			if (result == Enums.NSOKButton)
			{
				uint count = openPanel.URLs().count();
				if (!NSApplication.sharedApplication().delegate_().Call("shouldOpenFiles:", count).To<bool>())
					result = Enums.NSCancelButton;
			}
			
			return result;
		}
	}
}
