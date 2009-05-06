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

using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace MakeBuilder
{
	[ExportClass("FlagsController", "NSWindowController")]
	internal sealed class FlagsController : NSWindowController
	{
		public FlagsController(Dictionary<string, int> flags) : base(NSObject.AllocNative("FlagsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("make-flags"), this);	
			
			Unused.Value = window().setFrameAutosaveName(NSString.Create("make-flags window"));
			window().makeKeyAndOrderFront(this);
			
			m_docFlags = flags;
			m_flags = new Dictionary<string, int>(flags);
			
			NSView[] views = window().contentView().subviews();
			for (int i = 0; i < views.Length; ++i)
			{
				if (views[i].respondsToSelector("title"))
				{
					string title = views[i].Call("title").To<NSString>().ToString();
					if (m_flags.ContainsKey(title))
						views[i].Call("setState:", m_flags[title]);
				}
			}
			
			ActiveObjects.Add(this);
		}
		
		public void toggle(NSButton sender)
		{
			m_flags[sender.title().ToString()] = sender.state();
		}
		
		public void flagsOK(NSObject sender)
		{
			Unused.Value = sender;
			
			foreach (KeyValuePair<string, int> entry in m_flags)
				m_docFlags[entry.Key] = entry.Value;
				
			NSApplication.sharedApplication().stopModal();
			window().orderOut(this);
			window().release();
		}
		
		public void flagsCancel(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().stopModal();
			window().orderOut(this);
			window().release();
		}
			
		#region Fields --------------------------------------------------------
		private Dictionary<string, int> m_docFlags;
		private Dictionary<string, int> m_flags;
		#endregion
	}
}
