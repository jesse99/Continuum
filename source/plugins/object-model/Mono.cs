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
using System.IO;

namespace ObjectModel
{
	internal sealed class Mono : IMono, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Broadcaster.Register("mono_root changed", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		// If users have the mono source, but are using the mono from the installer then
		// the paths in the gac's mdb files will be wrong. So, we fix them up here.
		public string GetPath(string path)
		{
			// Pre-built mono files will look like "/private/tmp/monobuild/build/BUILD/mono-2.2/mcs/class/corlib/System.IO/File.cs"
			// Mono_root will usually look like "/foo/mono-2.2".
			if (!File.Exists(path))
			{
				if (m_monoRoot == null)
					DoMonoRootChanged("mono_root changed", null);
				
				if (m_monoRoot != null)
				{
					int i = path.IndexOf("/mcs/");
					if (i >= 0)
					{
						string temp = path.Substring(i + 1);
						path = Path.Combine(m_monoRoot, temp);
					}
				}
			}
			
			return path;
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "mono_root changed":
					DoMonoRootChanged(name, value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoMonoRootChanged(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			m_monoRoot = defaults.objectForKey(NSString.Create("mono_root")).To<NSString>().description();
			
			if (!Directory.Exists(Path.Combine(m_monoRoot, "mcs")))
			{
				NSString title = NSString.Create("Mono root appears invalid.");
				NSString message = NSString.Create("It does not contain an 'mcs' directory.");
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private string m_monoRoot;
		#endregion
	}
}	
