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

using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace Find
{
	internal sealed class Startup : IStartup
	{			
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		// Note that we do not want to use IFactoryPrefs here because we don't set
		// these in the main pref window.
		public void OnStartup()
		{
			NSMutableDictionary dict = NSMutableDictionary.Create();

			NSMutableArray dirs = NSMutableArray.Create();
			foreach (string path in DefaultDirs)
			{
				dirs.addObject(NSString.Create(path));
			}
			dict.setObject_forKey(dirs, NSString.Create("default find directories"));

			dict.setObject_forKey(NSString.Create(DefaultInclude), NSString.Create("default include glob"));

			dict.setObject_forKey(NSString.Create(AlwaysExclude), NSString.Create("always exclude globs"));

			NSUserDefaults.standardUserDefaults().registerDefaults(dict);		
		}
		
		internal static readonly string[] DefaultDirs = new string[]
		{
			"/System/Library/Frameworks/AppKit.framework/Versions/C/Headers",
			"/System/Library/Frameworks/Foundation.framework/Versions/C/Headers",
		};
		
		internal const string DefaultInclude = "*.cs;*.h;*.m";

		internal const string AlwaysExclude = ".*;*.app;*.dll;*.dylib;*.exe;*.gif;*.icns;*.jpeg;*.jpg;*.mdb;*.nib;*.pdb;*.X11";
				
		#region Fields --------------------------------------------------------
		private Boss m_boss;
		#endregion
	} 
}
