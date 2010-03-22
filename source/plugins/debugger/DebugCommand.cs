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
using Shared;
using System;
using System.Diagnostics;

namespace Debugger
{
	[ExportClass("DebugCommand", "NSScriptCommand")]
	internal sealed class DebugCommand : NSScriptCommand
	{
		public DebugCommand(IntPtr instance) : base(instance)
		{
		}
		
		public new NSObject performDefaultImplementation()
		{
			NSString path = directParameter().To<NSString>();
			NSObject args = evaluatedArguments().objectForKey(NSString.Create("Args"));
			
			try
			{
				if (Debugger.IsRunning)
					throw new Exception("The debugger is already running.");
					
				NSURL url = NSURL.fileURLWithPath(path);
				NSDocumentController controller = NSDocumentController.sharedDocumentController();
				
				NSDocument doc = controller.documentForURL(url);
				if (NSObject.IsNullOrNil(doc))
				{
					NSError err;
					doc = controller.makeDocumentWithContentsOfURL_ofType_error(
						url, NSString.Create("Debugger"), out err);
					if (!NSObject.IsNullOrNil(err))
						err.Raise();
						
					controller.addDocument(doc);
					if (!NSObject.IsNullOrNil(args))
						doc.Call("makeWindowControllersNoUI:", args);
					else
						doc.makeWindowControllers();
				}
				
				doc.showWindows();
			}
			catch (System.Reflection.TargetInvocationException t)
			{
				DoError(t.InnerException, path, args);
			}
			catch (Exception e)
			{
				DoError(e, path, args);
			}
			
			return null;
		}
		
		private void DoError(Exception e, NSString path, NSObject args)
		{
			if (!NSObject.IsNullOrNil(args))
				Log.WriteLine(TraceLevel.Error, "App", "debug \"{0:D}\" \"{1:D}\" failed", path, args);
			else
				Log.WriteLine(TraceLevel.Error, "App", "debug \"{0:D}\" failed", path);
			Log.WriteLine(TraceLevel.Error, "App", "{0}", e.Message);
			
			setScriptErrorNumber(1);
			setScriptErrorString(NSString.Create(e.Message));
		}
	}
}
