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

using MCocoa;
using MObjc;
using MObjc.Helpers;
using Shared;
using System;

namespace DirectoryEditor
{
	internal sealed class DirectoryItemStyler
	{
		public DirectoryItemStyler(string path)
		{
			m_path = path;
			DoSetDefaultPrefs();
			DoReadPrefs();
			
			ActiveObjects.Add(this);
		}
		
		// Should be used for paths, but not bundles.
		public NSColor GetPathColor()
		{
			return m_pathColor ?? NSColor.blackColor();
		}
		
		// Used for files and bundles.
		public NSColor GetFileColor(string fileName)
		{
			return GetFileColor(fileName, m_fileGlobs, m_fileColors);
		}
		
		internal string[][] FileGlobs {get {return m_fileGlobs;}}
		internal NSColor[] FileColors {get {return m_fileColors;}}
		
		[ThreadModel(ThreadModel.Concurrent)]
		internal static NSColor GetFileColor(string fileName, string[][] fileGlobs, NSColor[] fileColors)
		{
			for (int i = 0; i < FilesCount; ++i)
			{
				if (fileGlobs[i] != null)
				{
					foreach (string glob in fileGlobs[i])
					{
						if (Glob.Match(glob, fileName))
						{
							return fileColors[i] ?? NSColor.blackColor();
						}
					}
				}
			}
			
			return NSColor.blackColor();
		}
		
		public void Reload()
		{
			DoReadPrefs();
		}
		
		public const int FilesCount = 7;
		
		#region Private Methods
		private void DoReadPrefs()
		{
			// Release the old stuff (if any).
			if (m_pathColor != null)
			{
				m_pathColor.release();
				m_pathColor = null;
			}
			
			for (int i = 0; i < FilesCount; ++i)
			{
				if (m_fileColors[i] != null)
				{
					m_fileColors[i].release();
					m_fileColors[i] = null;
				}
			}
			
			// Load the new stuff.
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			var data = defaults.objectForKey(NSString.Create(m_path + "-path color")).To<NSData>();
			m_pathColor = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>().Retain();
			
			for (int i = 1; i <= FilesCount; ++i)
			{
				data = defaults.objectForKey(NSString.Create(m_path + "-files" + i + " color")).To<NSData>();
				m_fileColors[i - 1] = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>().Retain();
				
				string globs = defaults.stringForKey(NSString.Create(m_path + "-files" + i + " globs")).description();
				m_fileGlobs[i - 1] = Glob.Split(globs);
			}
		}
		
		private void DoSetColor(NSMutableDictionary dict, string name, string globs, int r, int g, int b)
		{
			NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(r/255.0f, g/255.0f, b/255.0f, 1.0f);
			NSData data = NSArchiver.archivedDataWithRootObject(color);
			dict.setObject_forKey(data, NSString.Create(m_path + "-" + name + " color"));
			
			if (globs != null)
			{
				NSString value = NSString.Create(globs);
				dict.setObject_forKey(value, NSString.Create(m_path + "-" + name + " globs"));
			}
		}
		
		private void DoSetDefaultPrefs()
		{
			NSMutableDictionary dict = NSMutableDictionary.Create();
			
			// colors
			DoSetColor(dict, "path", null, 0, 0, 0);
			DoSetColor(dict, "files1", "*.cs *.c *.cpp *.h *.hpp *.m *.rs *.js", 0, 0, 0);
			DoSetColor(dict, "files2", "Makefile Make.shared *.am *.make *.mk SConstruct SConscript wscript wscript_build *.rc", 127, 21, 24);
			DoSetColor(dict, "files3", "*.nib *.xib *.icns *.png *.jpeg *.jpg *.gif *.ignore", 29, 151, 26);
			DoSetColor(dict, "files4", "*.xml *.xsd *.xsdl *.xsl *.schema *.config *.plist *.sdef *.html *.json", 83, 83, 151);
			DoSetColor(dict, "files5", "*.css *.sh *.py *.rb *.pl *.ref *.R", 61, 82, 194);
			DoSetColor(dict, "files6", "*.dll *.exe *.dylib", 255, 0, 0);
			DoSetColor(dict, "files7", string.Empty, 0, 0, 0);
			
			NSUserDefaults.standardUserDefaults().registerDefaults(dict);
		}
		#endregion
		
		#region Fields
		private string m_path;
		private NSColor m_pathColor;
		private NSColor[] m_fileColors = new NSColor[FilesCount];
		private string[][] m_fileGlobs = new string[FilesCount][];
		#endregion
	}
}
