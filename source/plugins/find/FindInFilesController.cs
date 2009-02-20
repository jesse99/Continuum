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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Find
{
	[ExportClass("FindInFilesController", "BaseFindController", Outlets = "dirPopup directoryList include includeList exclude excludeList")]
	internal sealed class FindInFilesController : BaseFindController
	{
		public FindInFilesController() : base(NSObject.AllocNative("FindInFilesController"), "find-in-files")
		{		
			Unused.Value = window().setFrameAutosaveName(NSString.Create("find in files window"));

			m_include = new IBOutlet<NSString>(this, "include");
			m_exclude = new IBOutlet<NSString>(this, "exclude");
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSString defaultIncludes = defaults.stringForKey(NSString.Create("default include glob"));
			
			m_includeList = new IBOutlet<NSMutableArray>(this, "includeList");
			this.willChangeValueForKey(NSString.Create("includeList"));
			NSMutableArray includes = NSMutableArray.Create();
			includes.addObject(defaultIncludes);
			includes.addObject(NSString.Create("Makefile;Makefile.am;*.make;*.mk"));	// TODO: might want some sort of pref for these
			includes.addObject(NSString.Create("*.h;*.m"));
			includes.addObject(NSString.Create("*.xml;*.xsd;*.config;*.plist"));
			m_includeList.Value = includes;
			this.didChangeValueForKey(NSString.Create("includeList"));

			m_excludeList = new IBOutlet<NSMutableArray>(this, "excludeList");
			this.willChangeValueForKey(NSString.Create("excludeList"));
			m_excludeList.Value = NSMutableArray.Create();
			this.didChangeValueForKey(NSString.Create("excludeList"));

			m_dirPopup = new IBOutlet<NSPopUpButton>(this, "dirPopup");	// pulldown buttons probably look a bit better but I was never able to get them working correctly
			string dir = DoGetDirectory();
			AddDefaultDirs();
			if (dir != null)
			{
				NSString name = NSString.Create(dir);
				m_dirPopup.Value.addItemWithTitle(name);
				m_dirPopup.Value.setTitle(name);
			}				
			
			this.willChangeValueForKey(NSString.Create("include"));
			m_include.Value = defaultIncludes;
			this.didChangeValueForKey(NSString.Create("include"));

			this.addObserver_forKeyPath_options_context(
				this, NSString.Create("include"), 0, IntPtr.Zero);
			this.addObserver_forKeyPath_options_context(
				this, NSString.Create("exclude"), 0, IntPtr.Zero);

			Broadcaster.Register("opened directory", this, this.DoDirOpened);
		}
		
		public void findAll(NSObject sender)
		{
			Unused.Value = sender;
			
			OnUpdateLists();
			
			string dir = Directory.description();
			
 			Re re = new Re
 			{
 				UseRegex = UseRegex, 
 				CaseSensitive = CaseSensitive, 
 				MatchWords = MatchWords, 
 				WithinText = WithinText,
 			};

			var findAll = new FindAll(dir, re.Make(FindText), Include, AllExcludes());
			findAll.Title = string.Format("Find '{0}'", FindText);
			findAll.Run();
						
			Unused.Value = new FindResultsController(findAll);		// FindResultsController will handle retain counts and references
		}
				
		public void replaceAll(NSObject sender)
		{
			Unused.Value = sender;
			
			OnUpdateLists();
			
			string dir = Directory.description();
			
 			Re re = new Re
 			{
 				UseRegex = UseRegex, 
 				CaseSensitive = CaseSensitive, 
 				MatchWords = MatchWords, 
 				WithinText = WithinText,
 			};

			var replaceAll = new ReplaceAll(dir, re.Make(FindText), ReplaceText, Include, AllExcludes());
			replaceAll.Title = string.Format("Replacing '{0}' with '{1}'.", FindText, ReplaceText);
			replaceAll.Run();
			
			Unused.Value = new FindProgressController(replaceAll);		// FindProgressController will handle retain counts and references
		}
		
		public void setDir(NSObject sender)	
		{
			Unused.Value = sender;
			
			NSOpenPanel panel = NSOpenPanel.openPanel();	
			panel.setCanChooseFiles(false);
			panel.setCanChooseDirectories(true);
			panel.setAllowsMultipleSelection(false);
			panel.setCanCreateDirectories(false);

			int result = panel.runModalForDirectory_file_types(null, null, null);
			if (result == Enums.NSOKButton && panel.filenames().count() == 1)
			{
				Directory = panel.filenames().objectAtIndex(0).To<NSString>();
			}
		}
		
		public void options(NSObject sender)
		{
			Unused.Value = sender;
			
			if (m_options == null)
				m_options = new FindInFilesOptionsController(this);
				
			m_options.window().makeKeyAndOrderFront(this);
		}
		
		public void AddDefaultDirs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSArray dirs = defaults.arrayForKey(NSString.Create("default find directories"));
			m_dirPopup.Value.addItemsWithTitles(dirs);
		}
		
		#region Protected Methods ---------------------------------------------
		private void DoDirOpened(string name, object b)
		{
			Boss boss = (Boss) b;
			var editor = boss.Get<IDirectoryEditor>();
			m_dirPopup.Value.addItemWithTitle(NSString.Create(editor.Path));
		}
		
		protected override bool OnFindEnabled()
		{
			return FindText.Length > 0 && Directory.length() > 0;
		}
		
		protected override void OnUpdateLists()
		{
			base.OnUpdateLists();

			NSString name = NSString.Create(string.Join(";", Include));
			if (name.length() > 0 && !m_includeList.Value.containsObject(name))	
			{
				this.willChangeValueForKey(NSString.Create("includeList"));
				m_includeList.Value.insertObject_atIndex(name, 0);
				
				this.didChangeValueForKey(NSString.Create("includeList"));
			}

			name = NSString.Create(string.Join(";", Exclude));
			if (name.length() > 0 && !m_excludeList.Value.containsObject(name))	
			{
				this.willChangeValueForKey(NSString.Create("excludeList"));
				m_excludeList.Value.insertObject_atIndex(name, 0);
				
				this.didChangeValueForKey(NSString.Create("excludeList"));
			}
		}
		#endregion

		#region Private Methods -----------------------------------------------
		private NSString Directory
		{
			get {return m_dirPopup.Value.title();}
			set 
			{
				m_dirPopup.Value.addItemWithTitle(value);
				m_dirPopup.Value.setTitle(value);

				OnEnableButtons();
			}
		}

		private string[] Include
		{
			get 
			{	
				string text = !NSObject.IsNullOrNil(m_include.Value) ? m_include.Value.description() : string.Empty;
				return text.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
			}
		}

		private string[] Exclude
		{
			get 
			{	
				string text = !NSObject.IsNullOrNil(m_exclude.Value) ? m_exclude.Value.description() : string.Empty;
				return text.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
			}
		}
		
		private string[] AllExcludes()
		{
			List<string> excludes = new List<string>(Exclude.Length + 13);
			excludes.AddRange(Exclude);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string text = defaults.stringForKey(NSString.Create("always exclude globs")).description();
			string[] globs = text.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);

			foreach (string glob in globs)
			{
				excludes.Add(glob);
			}
			
			return excludes.ToArray();
		}
		
		private string DoGetDirectory()
		{
			string dir = null;
			
			// First try the directory editor windows.
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var dwindows = boss.Get<IWindows>();
			
			Boss main = dwindows.Main();
			if (main != null)
				dir = main.Get<IDirectoryEditor>().Path;
			
			// Then the text editor windows.
			if (dir == null)
			{
				boss = ObjectModel.Create("TextEditorPlugin");
				var twindows = boss.Get<IWindows>();
				
				main = twindows.Main();
				if (main != null)
				{
					string path = main.Get<ITextEditor>().Path;
					if (path != null)
					{
						boss = dwindows.All().SingleOrDefault(b => path.StartsWith(b.Get<IDirectoryEditor>().Path));
						if (boss != null)
							dir = boss.Get<IDirectoryEditor>().Path;		// use the directory we have open if possible
						else
							dir = System.IO.Path.GetDirectoryName(path);
					}
				}
			}
			
			return dir;
		}
		#endregion
		
		#region Fields --------------------------------------------------------
		private IBOutlet<NSString> m_include;
		private IBOutlet<NSString> m_exclude;
		private IBOutlet<NSMutableArray> m_includeList;
		private IBOutlet<NSMutableArray> m_excludeList;
		private IBOutlet<NSPopUpButton> m_dirPopup;
		private FindInFilesOptionsController m_options;
		#endregion
	}
}
