// Copyright (C) 2011 Jesse Jones
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
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DefaultBuilder
{
	internal sealed class MonoMacBuilder : IBuilder
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(string path)
		{
			m_path = path;
			
			string makefile = Path.Combine(m_path, "Makefile");
			if (!File.Exists(makefile))
				DoCreateMakefile(makefile);
				
			string plist = Path.Combine(m_path, "Info.plist");
			if (!File.Exists(plist))
				DoCreatePlist(plist);
				
			Boss boss = ObjectModel.Create("MakeBuilder");
			m_make = boss.Get<IBuilder>();
			m_make.Init(makefile);
		}
		
//		public static string[] Globs
//		{
//			get {return ms_srcGlobs;}
//		}
		
		public string DefaultTarget
		{
			get {return m_make.DefaultTarget;}
		}
		
		public string[] Targets
		{
			get {return m_make.Targets;}
		}
		
		public bool StderrIsExpected
		{
			get {return m_make.StderrIsExpected;}
		}
		
		public string Command
		{
			get {return m_make.Command;}
		}
		
		public Process Build(string target)
		{
			DoGenerateDefaultMake();
			return m_make.Build(target);
		}
		
		public void SetBuildFlags()
		{
			m_make.SetBuildFlags();
		}
		
		public void SetBuildVariables()
		{
			m_make.SetBuildVariables();
		}
		
		#region Private Methods
		private void DoCreateMakefile(string makefile)
		{
			string exe = Path.GetFileName(m_path);
			
			string contents = string.Format(@"# Machine generated - may be manually edited
include default.mk
 
# ------------------------------------------------------------------------
# Public variables
CSC ?= gmcs
MONO ?= mono
DEBUG ?= 1

ifeq ($(DEBUG),1)
	CSC-FLAGS ?= -checked+ -debug+ -warn:4 -warnaserror+ -d:DEBUG -d:TRACE -d:CONTRACTS_FULL
	MONO-FLAGS ?= --debug
else
	CSC-FLAGS ?= -debug+ -optimize+ -warn:4 -d:TRACE -d:CONTRACTS_PRECONDITIONS
	MONO-FLAGS ?= --debug
endif

# ------------------------------------------------------------------------
# Internal variables
dummy := $(shell mkdir bin 2> /dev/null)
exe-path := bin/{0}.exe
app-path := bin/{0}.app

# major.minor.maintenance
version := 0.1.0
build-num := 1

# If there are files you do not want to compile append them to excluded-srcs.
excluded-srcs := 
src-files := $(filter-out $(excluded-srcs),$(SRC-FILES))

# ------------------------------------------------------------------------
# Primary targets
all: build

build: $(app-path)

run: build
	./$(app-path)/Contents/MacOS/launcher

abs-app-path := $(abspath $(app-path))
debug: build
	osascript -e 'tell application ""Continuum"" to debug ""$(abs-app-path)""'
	
update-libraries:
	cp ~/.config/MonoDevelop/addins/MonoDevelop.MonoMac.2.4.0.12/MonoMac.dll bin
	cp ~/.config/MonoDevelop/addins/MonoDevelop.MonoMac.2.4.0.12/MonoMac.dll.mdb bin

# ------------------------------------------------------------------------
# Binary targets
$(app-path): $(exe-path)
	$(COCOA-PACK) --app=$(app-path) --mono-flags=--debug --exe=$(exe-path) --require-mono=2.8 --plist=Info.plist --resources=$(RSRC-FILES),bin/MonoMac.dll,bin/MonoMac.dll.mdb --vars=VERSION:$(version),BUILDNUM:$(build-num),APPNAME:{0}

$(exe-path): $(src-files)
	$(CSC) -out:$(exe-path) -target:exe $(CSC-FLAGS) -r:System.Drawing,bin/MonoMac.dll $(src-files)

# ------------------------------------------------------------------------
# Misc targets
clean:
	-rm $(exe-path)
	-rm $(exe-path).mdb
	-rm -rf $(app-path)

dist-clean:
	-rm -rf bin
	-rm -f Info.plist
	-rm -f default.mk
	-rm -f Makefile
", exe);
			File.WriteAllText(makefile, contents);
		}
		
		private void DoCreatePlist(string plist)
		{
			string exe = Path.GetFileName(m_path);
			
			string contents = string.Format(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple Computer//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">

<!-- See http://developer.apple.com/documentation/MacOSX/Conceptual/BPRuntimeConfig/Articles/ConfigFiles.html#//apple_ref/doc/uid/20002091
for more information on this file. -->

<!-- If the icon doesn't show up try rebuilding the LaunchServices database:
	cd ~/Library/Preferences/	
	rm LS*
	rm .LS*	
-->

<plist version=""1.0"">
<dict>
	<!-- Keys you will want to change. -->
	<key>CFBundleIdentifier</key>
	<string>${{APPNAME}}</string>
	
	<!-- Keys you may want to change. -->
	<key>CFBundleDevelopmentRegion</key>
	<string>English</string>
	
	<key>NSMainNibFile</key>
	<string>MainMenu</string>
	
	<!-- <key>CFBundleIconFile</key> -->
	<!-- <string>AppIcon.icns</string> -->
	
	<key>LSMinimumSystemVersion</key>
	<string>10.6.0</string>
	
	<!-- Keys that should not be changed. -->
	<key>CFBundleExecutable</key>
	<string>launcher</string>
	
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	
	<key>CFBundleName</key>
	<string>${{APPNAME}}</string>
	
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	
	<!-- This is what is shown to the user via the Finder's get info command. -->
	<key>CFBundleShortVersionString</key>	<!-- major.minor.maintenance -->
	<string>${{VERSION}}</string>
	
	<key>CFBundleSignature</key>
	<string>????</string>
	
	<key>CFBundleVersion</key>
	<string>${{BUILDNUM}}</string>
	
	<key>NSAppleScriptEnabled</key>
	<string>YES</string>
	
	<key>NSPrincipalClass</key>
	<string>NSApplication</string>
</dict>
</plist>
", exe);
			File.WriteAllText(plist, contents);
		}
		
		private string DoGetCocoaPackPath()
		{
			string path = System.Reflection.Assembly.GetEntryAssembly().Location;
			path = Path.GetDirectoryName(path);		// Contents/Resources
			path = Path.Combine(path, "cocoa-pack");	// Contents/Resources/cocoa-pack
			return path;
		}
		
		private void DoGenerateDefaultMake()
		{
			string contents = string.Format(@"# Machine generated - DO NOT EDIT
COCOA-PACK ?= ""{0}""

SRC-FILES := {1}
RSRC-FILES := {2}
", 
	DoGetCocoaPackPath(),
	Helpers.GetFiles(m_path, ms_srcGlobs), 
	DoGetResourceFiles());
			
			string path = Path.Combine(m_path, "default.mk");
			Helpers.WriteFile(path, contents);
		}
		
		private string DoGetResourceFiles()
		{
			var files = new List<string>();
			
			DoAddFiles(files, Directory.GetDirectories(m_path, "*.nib", SearchOption.AllDirectories));
			DoAddFiles(files, Directory.GetFiles(m_path, "*.xib", SearchOption.AllDirectories));
			
			return string.Join(",", files.ToArray());
		}
		
		private void DoAddFiles(List<string> files, IEnumerable<string> paths)
		{
			files.AddRange(from p in paths where !p.Contains("/bin/") select Helpers.GetRelativePath(m_path, p));
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_path;
		private IBuilder m_make;
		
		private static string[] ms_srcGlobs = new string[]{"*.cs"};
		#endregion
	}
}
