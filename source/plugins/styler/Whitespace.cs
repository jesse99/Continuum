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
using Shared;
using System;
using System.Diagnostics;

namespace Styler
{
	internal sealed class Whitespace : IWhitespace
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			m_showSpaces = defaults.boolForKey(NSString.Create("show spaces"));
			m_showTabs = defaults.boolForKey(NSString.Create("show tabs"));
		}
		
		public bool ShowSpaces
		{
			[ThreadModel(ThreadModel.Concurrent)]
			get {return m_showSpaces;}
			set
			{
				m_showSpaces = value;
				
				NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
				defaults.setBool_forKey(m_showSpaces, NSString.Create("show spaces"));
			}
		}
		
		public bool ShowTabs
		{
			[ThreadModel(ThreadModel.Concurrent)]
			get {return m_showTabs;}
			set
			{
				m_showTabs = value;
				
				NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
				defaults.setBool_forKey(m_showTabs, NSString.Create("show tabs"));
			}
		}
		
		#region Fields 
		private Boss m_boss;
		private volatile bool m_showSpaces;
		private volatile bool m_showTabs;
		#endregion
	}
}
