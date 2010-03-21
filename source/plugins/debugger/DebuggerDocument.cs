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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Mono.Debugger;
using Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Debugger
{
	[ExportClass("DebuggerDocument", "NSDocument")]
	internal sealed class DebuggerDocument : NSDocument
	{
		private DebuggerDocument(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
			
			if (ms_windows == null)
				ms_windows = new DebuggerWindows();
		}
		
		// Used when opening mdb files.
		public new void makeWindowControllers()
		{
			try
			{
				m_breakInMain = true;
				makeWindowControllersNoUI(NSString.Empty);
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Couldn't start the debugger.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
				
				close();									// note that exceptions leaving here are ignored by cocoa
			}
		}
		
		// Used when opening mdb files or exe files (via AppleScript).
		public void makeWindowControllersNoUI(NSString args)
		{
			string path = fileURL().path().ToString();
			string dir = Path.GetDirectoryName(path);
			
			if (path.EndsWith(".mdb"))
				m_executable = Path.Combine(dir, Path.GetFileNameWithoutExtension(path));
			else
				m_executable = Path.Combine(dir, path);
			
			// The Soft debugger returns a VM in EndInvoke even when you pass
			// it a completely bogus path so we need to do this check. 
			if (!File.Exists(m_executable))
				throw new FileNotFoundException(m_executable + " was not found.");
			
			ProcessStartInfo info = new ProcessStartInfo();
			if (!NSObject.IsNullOrNil(args))
				info.Arguments = string.Format("--debug {0} {1:D}", m_executable, args);
			else
				info.Arguments = string.Format("--debug {0}", m_executable);
//			info.EnvironmentVariables.Add("key", "value");		// TODO: might want to support this
			info.FileName = "mono";
			info.RedirectStandardInput = false;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.UseShellExecute = false;
			info.WorkingDirectory = dir;
			
			m_debugger = new Debugger(info);
			
			NSWindow window = DoCreateCodeWindow();
			addWindowController(window.windowController());
		}
		
		public new void close()
		{
			if (m_debugger != null)
			{
				m_debugger.Dispose();
				m_debugger = null;
			}
			
			SuperCall(NSDocument.Class, "close");
		}
		
		public Debugger Debugger
		{
			get {return m_debugger;}
		}
		
		// Full path to the executable.
		public string Executable
		{
			get {return m_executable;}
		}
		
		public bool BreakInMain
		{
			get {return m_breakInMain;}
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			return true;	// don't think fileURL is available here so its hard to do anything useful...
		}
		
		#region Private Methods
		private NSWindow DoCreateCodeWindow()
		{
			Boss pluginBoss = ObjectModel.Create("TextEditorPlugin");
			var create = pluginBoss.Get<ICreate>();
			Boss boss = create.Create("CodeViewer");
			
			var viewer = boss.Get<ICodeViewer>();
			viewer.Init(this);
			
			var editor = boss.Get<ITextEditor>();
			editor.Editable = false;
			
			var window = boss.Get<IWindow>();
			return window.Window;
		}
		#endregion
		
		#region Fields
		private string m_executable;
		private Debugger m_debugger;
		private bool m_breakInMain;
		
		private static DebuggerWindows ms_windows;
		#endregion
	}
}
