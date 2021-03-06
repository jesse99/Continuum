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
using System.IO;

namespace DefaultBuilder
{
	internal sealed class CSharpBuilder : IBuilder
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
				
			Boss boss = ObjectModel.Create("MakeBuilder");
			m_make = boss.Get<IBuilder>();
			m_make.Init(makefile);
		}
		
		public static string[] Globs
		{
			get {return ms_srcGlobs;}
		}
		
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
			exe = Path.ChangeExtension(exe, ".exe");
			
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
exe-name := {0}

# If there are files you do not want to compile append them to excluded-srcs.
excluded-srcs := 
src-files := $(filter-out $(excluded-srcs),$(SRC-FILES))

# ------------------------------------------------------------------------
# Primary targets
all: build

build: $(src-files)
	$(CSC) -out:bin/$(exe-name) -target:exe $(CSC-FLAGS) $(src-files)

run: build
	$(MONO) $(MONO-FLAGS) bin/$(exe-name)

exe-path := $(abspath bin/$(exe-name))
debug: build
	osascript -e 'tell application ""Continuum"" to debug ""$(exe-path)""'

# ------------------------------------------------------------------------
# Misc targets
clean:
	-rm bin/$(exe-name)
	-rm bin/$(exe-name).mdb

dist-clean:
	-rm -rf bin
", exe);
			File.WriteAllText(makefile, contents);
		}
		
		private void DoGenerateDefaultMake()
		{
			string contents = string.Format(@"# Machine generated - DO NOT EDIT
SRC-FILES := {0}
", Helpers.GetFiles(m_path, ms_srcGlobs));
			
			string path = Path.Combine(m_path, "default.mk");
			Helpers.WriteFile(path, contents);
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
