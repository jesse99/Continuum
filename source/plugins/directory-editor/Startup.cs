// Copyright (C) 2007-2008 Jesse Jones
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
using System.IO;

namespace DirectoryEditor
{
	internal sealed class Startup : IStartup, IFactoryPrefs
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			DoInitPrefs(dict);
		}

		public void OnStartup()
		{
			DoReadPrefs();			
		}
		
		#region Private Methods
		private void DoReadPrefs()
		{
			NSArray paths = NSUserDefaults.standardUserDefaults().arrayForKey(NSString.Create("dir-paths"));
			if (!NSObject.IsNullOrNil(paths) && paths.count() > 0)
			{
				foreach (NSString p in paths)
				{
					if (Directory.Exists(p.ToString()))
						Gear.Ignore.Value = new DirectoryController(p.ToString());
				}
			}
			else
			{
				Boss app = ObjectModel.Create("DirectoryEditorPlugin");
				var open = app.Get<IOpen>();
				open.Open();
			}
		}
				
		// These are the prefs associated with the app's preferences panel
		// (DirectoryController handles the prefs associated with a directory).
		private void DoInitPrefs(NSMutableDictionary dict)	
		{
			string ignores = @"all-am
check-am
check
clean-am
clean-generic
ctags-recursive
ctags
CTAGS
distclean-am
distclean-generic
distclean-tags
distclean
distdir
dvi-am
dvi
extra-bin
GTAGS
html
ID
info-am
info
install-am
install-binSCRIPTS
install-data-am
install-data
install-exec-am
install-exec
install-info
install-man
install-pixmapDATA
install-strip
installcheck-am
installcheck
installdirs-am
installdirs
maintainer-clean-am
maintainer-clean-generic
maintainer-clean-recursive
maintainer-clean
Makefile
mostlyclean-am
mostlyclean-generic
mostlyclean
pdf-am
pdf
push
ps-am
ps
tags-recursive
tags
TAGS
uninstall-am
uninstall-binSCRIPTS
uninstall-info-am
uninstall-info
uninstall-pixmapDATA
zip-bin";
			dict.setObject_forKey(NSString.Create(ignores), NSString.Create("globalIgnores"));
		}
		#endregion

		#region Fields 
		private Boss m_boss; 
		#endregion
	} 
}	