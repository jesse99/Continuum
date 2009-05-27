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

using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace NantBuilder
{
	[ExportClass("NantFlagsController", "NSWindowController", Outlets = "debug extension indent listener logfile logger quiet target verbose")]
	internal sealed class NantFlagsController : NSWindowController
	{
		public NantFlagsController(Prefs prefs) : base(NSObject.AllocNative("NantFlagsController"))
		{
			m_prefs = prefs;
			
			// Load the nib
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("nant-flags"), this);	
			
			// Set the initial state.
			foreach (KeyValuePair<string, string> entry in ms_valueNames)
			{
				NSTextField field = this[entry.Key].To<NSTextField>();
				field.setStringValue(NSString.Create(m_prefs.GetValue(entry.Value)));
			}
			
			foreach (KeyValuePair<string, string> entry in ms_flagNames)
			{
				NSButton button = this[entry.Key].To<NSButton>();
				button.setIntegerValue(m_prefs.GetFlag(entry.Value) ? 1 : 0);
			}
			
			// Open the window.
			Unused.Value = window().setFrameAutosaveName(NSString.Create("nant-flags window"));
			window().makeKeyAndOrderFront(this);
		}
		
		public void pressedOK(NSObject sender)
		{
			foreach (KeyValuePair<string, string> entry in ms_valueNames)
			{
				NSTextField field = this[entry.Key].To<NSTextField>();
				m_prefs.SetValue(entry.Value, field.stringValue().description());
			}
			foreach (KeyValuePair<string, string> entry in ms_flagNames)
			{
				NSButton button = this[entry.Key].To<NSButton>();
				m_prefs.SetFlag(entry.Value, button.integerValue() == 1);
			}
			m_prefs.Save();
			
			NSApplication.sharedApplication().stopModal();
			window().orderOut(this);
			window().release();
		}
		
		public void pressedCancel(NSObject sender)
		{
			NSApplication.sharedApplication().stopModal();
			window().orderOut(this);
			window().release();
		}
		
		#region Fields
		private Prefs m_prefs;
		
		private static Dictionary<string, string> ms_valueNames = new Dictionary<string, string>
		{
			{"target", "-targetframework"},
			{"extension", "-extension"},
			{"indent", "-indent"},
			{"logger", "-logger"},
			{"logfile", "-logfile"},
			{"listener", "-listener"},
		};
		private static Dictionary<string, string> ms_flagNames = new Dictionary<string, string>
		{
			{"quiet", "-quiet"},
			{"verbose", "-verbose"},
			{"debug", "-debug"},
		};
		#endregion
	}
}
