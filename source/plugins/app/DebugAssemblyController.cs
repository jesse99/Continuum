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

using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;

namespace App
{
	[ExportClass("DebugAssemblyController", "NSWindowController", Outlets = "args assembly env workingDir tool")]
	internal sealed class DebugAssemblyController : NSWindowController
	{
		public DebugAssemblyController() : base(NSObject.AllocAndInitInstance("DebugAssemblyController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("debug-assembly"), this);
			
			m_assembly = new IBOutlet<NSPathControl>(this, "assembly").Value;
			m_workingDir = new IBOutlet<NSPathControl>(this, "workingDir").Value;
			m_args = new IBOutlet<NSTextField>(this, "args").Value;
			m_env = new IBOutlet<NSTextField>(this, "env").Value;
			m_tool = new IBOutlet<NSTextField>(this, "tool").Value;
			
			m_assembly.Call("setDoubleAction:", new Selector("changeAssembly:"));
			m_workingDir.Call("setDoubleAction:", new Selector("changeWorkingDir:"));
			
			DoLoadPrefs();
			ActiveObjects.Add(this);
		}
		
		public void Show()
		{
			window().makeKeyAndOrderFront(this);
		}
		
		public void changeAssembly(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Choose Assembly"));
			panel.setCanChooseDirectories(false);
			panel.setCanChooseFiles(true);
			panel.setAllowsMultipleSelection(false);
			
			int button = panel.runModal();
			if (button == Enums.NSOKButton && panel.URLs().count() == 1)
			{
				NSURL url = panel.URL();
				m_assembly.setURL(url);
				
				string dir = System.IO.Path.GetDirectoryName(url.path().ToString());
				m_workingDir.setURL(NSURL.fileURLWithPath_isDirectory(NSString.Create(dir), true));
			}
		}
		
		public void changeWorkingDir(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Choose Working Directory"));
			panel.setCanChooseDirectories(true);
			panel.setCanChooseFiles(false);
			panel.setAllowsMultipleSelection(false);
			
			int button = panel.runModal();
			if (button == Enums.NSOKButton && panel.URLs().count() == 1)
			{
				m_workingDir.setURL(panel.URL());
			}
		}
		
		public void okPressed(NSObject sender)
		{
			DoSavePrefs();
			if (DoLaunch())
			{
				NSApplication.sharedApplication().stopModal();
				window().orderOut(this);
			}
		}
		
		public void cancelPressed(NSObject sender)
		{
			NSApplication.sharedApplication().stopModal();
			window().orderOut(this);
		}
		
		#region Private Methods
		private void DoLoadPrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			m_assembly.setURL(defaults.URLForKey(NSString.Create("debug_assembly_path")));
			m_workingDir.setURL(defaults.URLForKey(NSString.Create("debug_assembly_working_dir")));
			m_args.setStringValue(defaults.stringForKey(NSString.Create("debug_assembly_args")));
			m_env.setStringValue(defaults.stringForKey(NSString.Create("debug_assembly_env")));
			m_tool.setStringValue(defaults.stringForKey(NSString.Create("debug_assembly_tool")));
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			defaults.setObject_forKey(m_assembly.URL().path(), NSString.Create("debug_assembly_path"));
			defaults.setObject_forKey(m_workingDir.URL().path(), NSString.Create("debug_assembly_working_dir"));
			defaults.setObject_forKey(m_args.stringValue(), NSString.Create("debug_assembly_args"));
			defaults.setObject_forKey(m_env.stringValue(), NSString.Create("debug_assembly_env"));
			defaults.setObject_forKey(m_tool.stringValue(), NSString.Create("debug_assembly_tool"));
			
			defaults.synchronize();
		}
		
		private bool DoLaunch()
		{
			NSDocument doc = null;
			
			try
			{
				var controller = NSDocumentController.sharedDocumentController();
				
				NSError err;
				doc = controller.makeDocumentWithContentsOfURL_ofType_error(
					m_assembly.URL(), NSString.Create("Debugger"), out err);
				if (!NSObject.IsNullOrNil(err))
					err.Raise();
					
				controller.addDocument(doc);
				doc.Call("makeWindowControllersNoUI", m_tool.stringValue(), m_args.stringValue(), m_env.stringValue(), m_workingDir.URL().path(), true);
			}
			catch (System.Reflection.TargetInvocationException e)
			{
				if (doc != null)
					doc.close();
				doc = null;
					
				NSString title = NSString.Create("Failed to launch the debugger.");
				NSString message = NSString.Create(e.InnerException.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			catch (Exception e)
			{
				if (doc != null)
					doc.close();
				doc = null;
					
				NSString title = NSString.Create("Failed to launch the debugger.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			
			return doc != null;
		}
		#endregion
		
		#region Fields
		private NSPathControl m_assembly;
		private NSPathControl m_workingDir;
		private NSTextField m_args;
		private NSTextField m_env;
		private NSTextField m_tool;
		#endregion
	}
}
