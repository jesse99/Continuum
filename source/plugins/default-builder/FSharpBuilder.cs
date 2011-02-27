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
	internal sealed class FSharpBuilder : IBuilder
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
FSC ?= fsc
MONO ?= mono
DEBUG ?= 1

ifeq ($(DEBUG),1)
	FSC-FLAGS ?= --checked+ --debug+ --warn:4 --mlcompatibility --warnaserror+ --nologo -d:DEBUG -d:TRACE -d:CONTRACTS_FULL
	MONO-FLAGS ?= --debug
else
	FSC-FLAGS ?= --debug+ --optimize+ --warn:4 --mlcompatibility --nologo -d:TRACE -d:CONTRACTS_PRECONDITIONS
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

# The auto-generated SRC-FILES may not work because compilation order is
# important in fsc. To work around this you can either alphabetize your files
# so that dependencies appear first or explicitly list src-files.
build: $(src-files)
	$(FSC) --out:bin/$(exe-name) --target:exe $(FSC-FLAGS) $(src-files)

run: build
	$(MONO) $(MONO-FLAGS) bin/$(exe-name)

# Note that this doesn't work (with mono 2.10)
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
		
		private int DoCompareProgram(string lhs, string rhs)
		{
			string f1 = Path.GetFileName(lhs).ToLower();
			string f2 = Path.GetFileName(rhs).ToLower();
			
			if (f1 != "program.fs" && f2 == "program.fs")
			{
				return -1;
			}
			else if (f1 == "program.fs" && f2 != "program.fs")
			{
				return +1;
			}
			else
			{
				if (f1 != "main.fs" && f2 == "main.fs")
					return -1;
				else if (f1 == "main.fs" && f2 != "main.fs")
					return +1;
				else
					return 0;
			}
		}
		
		private int DoCompareExtension(string lhs, string rhs)
		{
			string e1 = Path.GetExtension(lhs);
			string e2 = Path.GetExtension(rhs);
			
			if (e1 == ".fsi" && e2 == ".fs")
				return -1;
			else if (e1 == ".fs" && e2 == ".fsi")
				return +1;
			else
				return 0;
		}
		
		private int DoCompareFileName(string lhs, string rhs)
		{
			return Path.GetFileNameWithoutExtension(lhs).CompareTo(Path.GetFileNameWithoutExtension(rhs));
		}
		
		// Unfortunately compilation order is important with F#. There's no good way to
		// handle this when auto-generating source file lists so we'll arrange them in some
		// well defined order so users can use the auto-generated list if they name their
		// files appropriately.
		private string DoSortFiles(string fileNames)
		{
			string[] files = fileNames.Split(' ');
			Array.Sort(files, (lhs, rhs) =>
			{
				int result = 0;
				
				if (result == 0)
					DoCompareProgram(lhs, rhs);
				
				if (result == 0)
					DoCompareExtension(lhs, rhs);
				
				if (result == 0)
					result = DoCompareFileName(lhs, rhs);
					
				return result;
			});
			return string.Join(" ", files);
		}
		
		private void DoGenerateDefaultMake()
		{
			string contents = string.Format(@"# Machine generated - DO NOT EDIT
SRC-FILES := {0}
", DoSortFiles(Helpers.GetFiles(m_path, ms_srcGlobs)));
			
			string path = Path.Combine(m_path, "default.mk");
			Helpers.WriteFile(path, contents);
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_path;
		private IBuilder m_make;
		
		private static string[] ms_srcGlobs = new string[]{"*.fs", "*.fsi"};
		#endregion
	}
}
