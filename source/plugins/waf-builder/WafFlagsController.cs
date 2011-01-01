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
using System.Collections.Generic;

namespace WafBuilder
{
	[ExportClass("WafFlagsController", "NSWindowController", Outlets = "verbosity jobs")]
	internal sealed class WafFlagsController : NSWindowController
	{
		public WafFlagsController(Dictionary<string, int> flags) : base(NSObject.AllocAndInitInstance("WafFlagsController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("waf-flags"), this);	
			
			m_verbosity = new IBOutlet<NSTextField>(this, "verbosity").Value;
			m_jobs = new IBOutlet<NSTextField>(this, "jobs").Value;
			
//			Unused.Value = window().setFrameAutosaveName(NSString.Create("waf-flags window"));
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
			
			if (m_flags.ContainsKey("verbosity"))
				m_verbosity.setIntegerValue(m_flags["verbosity"]);
			else
				m_verbosity.setIntegerValue(0);
			if (m_flags.ContainsKey("jobs"))
				m_jobs.setIntegerValue(m_flags["jobs"]);
			else
				m_jobs.setIntegerValue(8);
				
			ActiveObjects.Add(this);
		}
		
		public void toggle(NSButton sender)
		{
			m_flags[sender.title().ToString()] = sender.state();
		}
		
		public void changeVerbosity(NSTextField sender)
		{
			m_flags["verbosity"] = sender.intValue();
		}
		
		public void changeJobs(NSTextField sender)
		{
			m_flags["jobs"] = sender.intValue();
		Console.WriteLine("changeJobs: {0}", sender.intValue()); Console.Out.Flush();
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
		
		#region Fields
		private NSTextField m_verbosity;
		private NSTextField m_jobs;
		private Dictionary<string, int> m_docFlags;
		private Dictionary<string, int> m_flags;
		#endregion
	}
}
