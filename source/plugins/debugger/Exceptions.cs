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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace Debugger
{
	internal sealed class Exceptions : IExceptions
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string[] Ignored
		{
			get
			{
				if (m_ignored == null)
					m_ignored = DoLoad();
				
				return m_ignored;
			}
			set
			{
				Contract.Requires(value != null);
				
				DoSave(value);
				m_ignored = value;
			}
		}
		
		#region Private Methods
		private void DoSave(string[] types)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			Array.Sort(types);
			
			NSArray value = NSArray.Create(types);
			defaults.setObject_forKey(value, NSString.Create("ignored-exceptions"));
		}
		
		private string[] DoLoad()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSArray value = defaults.arrayForKey(NSString.Create("ignored-exceptions"));
			
			var types = new List<string>();
			if (!NSObject.IsNullOrNil(value))
			{
				foreach (NSString s in value)
				{
					types.Add(s.ToString());
				}
			}
			
			return types.ToArray();
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private string[] m_ignored;
		#endregion
	}
}
