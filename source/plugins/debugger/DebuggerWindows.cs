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
using MObjc.Helpers;
using Shared;
using System;
using System.Collections.Generic;

namespace Debugger
{
	[ExportClass("DebuggerWindows", "NSObject", Outlets = "threadsWindow threadsController stackWindow stackController controlsWindow variablesWindow controlsController variablesController")]
	internal sealed class DebuggerWindows : NSObject, IObserver
	{
		public DebuggerWindows() : base(NSObject.AllocAndInitInstance("DebuggerWindows"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("debugger"), this);
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Register2(this, 66, this.DoToggleExceptions, this.DoEnableExceptions);
			
			foreach (WindowInfo info in m_windows)
			{
				info.Window = new IBOutlet<NSWindow>(this, info.Name + "Window").Value;
				info.Window.setFrameAutosaveName(NSString.Create("debugger {0} window", info.Name));	// We don't actually close these windows so we need to manage auto-saving ourself.
				info.Window.setExcludedFromWindowsMenu(true);
				
				Unused.Value = new IBOutlet<NSWindowController>(this, info.Name + "Controller").Value;	// Not sure why but we need to do this but if we don't the controllers are not constructed.
				
				WindowInfo temp = info;
				handler.Register(this, info.MenuId, () => temp.Window.makeKeyAndOrderFront(this), () => Debugger.IsRunning);
			}
			
			Broadcaster.Register("debugger started", this);
			Broadcaster.Register("debugger stopped", this);
			Broadcaster.Register("exiting event loop", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger started":
					DoLoadPrefs();
					
					foreach (WindowInfo info in m_windows)
					{
						if (!info.HiddenOnStart)
							info.Window.makeKeyAndOrderFront(this);
					}
					break;
				
				case "debugger stopped":
					DoSavePrefs();
					
					foreach (WindowInfo info in m_windows)
					{
						info.Window.saveFrameUsingName(NSString.Create("debugger {0} window", info.Name));
						info.Window.orderOut(this);
					}
					
					break;
				
				case "exiting event loop":
					DoSavePrefs();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public static bool BreakOnExceptions
		{
			get {return !ms_ignoreExceptions;}
		}
		
		#region Private Methods
		private void DoToggleExceptions()
		{
			ms_ignoreExceptions = !ms_ignoreExceptions;
			Broadcaster.Invoke("toggled exceptions", !ms_ignoreExceptions);
		}
		
		private MenuState DoEnableExceptions()
		{
			if (ms_ignoreExceptions)
				return MenuState.Enabled;
			else
				return MenuState.Enabled | MenuState.Checked;
		}
		
		private void DoLoadPrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			ms_ignoreExceptions = defaults.boolForKey(NSString.Create("ignore exceptions"));
			
			foreach (WindowInfo info in m_windows)
			{
				// We use HiddenOnStart instead of the more natural VisibleOnStart
				// because boolForKey returns false if the key does not exist.
				info.HiddenOnStart = defaults.boolForKey(NSString.Create("{0} window hidden", info.Name));
			}
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			defaults.setBool_forKey(ms_ignoreExceptions, NSString.Create("ignore exceptions"));
			
			foreach (WindowInfo info in m_windows)
			{
				defaults.setBool_forKey(!info.Window.isVisible(), NSString.Create("{0} window hidden", info.Name));
			}
		}
		#endregion
		
		#region Private Types
		private sealed class WindowInfo
		{
			public WindowInfo(string name, int menuId)
			{
				Name = name;
				MenuId = menuId;
			}
			
			public string Name {get; private set;}
			
			public int MenuId {get; private set;}
			
			public bool HiddenOnStart {get; set;}
			
			public NSWindow Window {get; set;}
		}
		#endregion
		
		#region Fields
		private HashSet<WindowInfo> m_windows = new HashSet<WindowInfo>
		{
			new WindowInfo("controls", 661),
			new WindowInfo("variables", 662),
			new WindowInfo("stack", 663),
			new WindowInfo("threads", 664),
		};
		private static bool ms_ignoreExceptions;
		#endregion
	}
}
