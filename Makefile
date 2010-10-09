# ------------------------------------------------------------------------
# Public variables
CSC ?= gmcs
MONO ?= mono
PACK ?= cocoa-pack
SMOKE ?= smoke
NUNIT ?= nunit-console2
GENDARME ?= /usr/local/bin/gendarme
ASCIIDOC ?= asciidoc

ifdef RELEASE
	CSC_FLAGS += -checked+ -debug+ -warn:4 -nowarn:1591 -optimize+ -d:TRACE
	MONO_FLAGS += --desktop
else
	CSC_FLAGS += -checked+ -debug+ -warnaserror+ -warn:4 -nowarn:1591 -d:DEBUG -d:TRACE -d:CONTRACTS_FULL
	MONO_FLAGS += --desktop --debug
endif

ifeq ($(PROFILE),1)
	CSC_FLAGS += -d:PROFILE
endif

# ------------------------------------------------------------------------
# Internal variables
dummy := $(shell mkdir bin 2> /dev/null)
dummy := $(shell mkdir bin/plugins 2> /dev/null)
dummy := $(shell if [[ "$(CSC_FLAGS)" != `cat bin/csc_flags 2> /dev/null` ]]; then echo "$(CSC_FLAGS)" > bin/csc_flags; fi)

base_version := 0.8.xxx.0										# major.minor.build.revision
version := $(shell mget_version.sh $(base_version) build_num)	# this will increment the build number stored in build_num
version := $(strip $(version))

build-num := $(shell echo "$(version)" | cut -d . -f 3)

plugins-path := bin/plugins
here := $(shell pwd)

gear-dll := bin/gear.dll
non-ui-resources := $(gear-dll),bin/shared.dll
non-ui-files := $(shell echo $(non-ui-resources) | sed "s/,/ /g")
non-ui-files += bin/csc_flags

cocoa-dlls := bin/mobjc.dll,bin/mcocoa.dll
ui-resources := $(cocoa-dlls),$(gear-dll),bin/shared.dll
ui-files := $(shell echo $(ui-resources) | sed "s/,/ /g")
ui-files += bin/csc_flags

contents := $(abspath bin/Continuum.app/Contents)

all:

program-targets :=
plugin-targets :=
nib-files :=
other-files := bin/continuum.exe.config
smoke-files :=
clean-files :=
clean-dirs :=
test-files :=
include source/shared/module.mk
include $(shell find source/plugins/ -name "*.mk" -print)
include source/continuum/module.mk

# ------------------------------------------------------------------------
# Primary targets		
all: $(program-targets) readme

plugins: $(plugin-targets)

app: bin/install-tool $(program-targets) bin/continuum.exe.config

debug: app
	-ln `which mono` "$(contents)/MacOS/Continuum"
	osascript -e 'tell application "Foreshadow" to debug "$(contents)/Resources/continuum.exe" using "$(contents)/MacOS/Continuum"'

# Note that running this way (instead of via open or the Finder) allows us to see 
# console output in the terminal instead of the system log.
run-app: app
	cd $(resources-path)
	$(macos-path)/launcher
	
check: bin/tests.dll
	cd bin && "$(NUNIT)" -nologo tests.dll

check1: bin/tests.dll
	cd bin && "$(NUNIT)" -nologo -fixture=$(TEST1) tests.dll
	
readme: README.asciidoc
	$(ASCIIDOC) --backend=xhtml11 $^

# ------------------------------------------------------------------------
# Misc targets
keys:
	sn -k keys
	
bin/test-files: $(test-files)
	@echo "$^" > $@

bin/tests.dll: bin/test-files $(gear-dll) bin/csc_flags
	$(CSC) -out:$@ $(CSC_FLAGS) -unsafe -d:TEST -pkg:mono-nunit -r:$(cocoa-dlls),$(gear-dll),ICSharpCode.SharpZipLib.dll,Mono.Posix.dll,bin/Mono.Cecil.dll,bin/Mono.Cecil.Mdb.dll,System.Configuration.dll,bin/Mono.Debugger.Soft.dll -target:library @bin/test-files

bin/install-tool: bin/install-tool.i386 bin/install-tool.ppc
	lipo -create -output bin/install-tool -arch i386 bin/install-tool.i386 -arch ppc bin/install-tool.ppc

bin/install-tool.i386: install-tool.c
	gcc install-tool.c -arch i386 -o bin/install-tool.i386 -framework Security

bin/install-tool.ppc: install-tool.c
	gcc install-tool.c -arch ppc -o bin/install-tool.ppc -framework Security

update-libraries:
	cp `pkg-config --variable=Libraries mobjc` bin
	cp `pkg-config --variable=Libraries mcocoa` bin
	cp `pkg-config --variable=Libraries gear` bin

clean:
	-rm -f bin/csc_flags
	-rm -f $(clean-files)
	-rm -rf $(clean-dirs)
	-rm -rf bin/plugins
	-rm -f bin/*-sources
	-rm -rf bin/*.config
	-rm -rf bin/*.nib
	-rm -rf bin/TestResult.xml
	-rm -rf bin/test-files
	-rm -rf bin/tests.dll
	-rm -rf bin/tests.dll.mdb
	-rm -rf bin/install-tool*
	-rm  -f bin/csc_flags
	
# This is enough to ensure that everything is rebuilt, and even more important, that
# the config file will be portable. (We can't do a full clean because the plugin directory
# structure is built as a side effect of evaluating the make file).
mini-clean:
	-rm bin/*-sources
	-rm -rf bin/*.config
	-rm -rf bin/*.nib
	-rm -rf bin/install-tool*
	
dist: mini-clean app
	tar --create --compress --exclude \*/.svn --exclude \*/.svn/\* --file=Continuum-$(version).tar.gz \
		BUILDING CHANGES CHANGE_LOG Dictionary.txt MIT.X11 Makefile README.asciidoc README.html Tables.rtf gendarme.ignore install-tool.c make-foreshadow source bin/Continuum.app

dist-clean:
	-rm -rf bin
	-rm -f build_num
	-rm -f foreshadow.log
	-rm -f source/AssemblyVersion.cs

# When building Continuum the config file used is the one in bin.
bin/continuum.exe.config:
	@echo "generating bin/continuum.exe.config"
	@echo "<!-- Note that make-foreshadow uses the foreshadow.exe.config file >" > bin/continuum.exe.config
	@echo "<?xml version = \"1.0\" encoding = \"utf-8\" ?>" > bin/continuum.exe.config
	@echo "<configuration>" >> bin/continuum.exe.config
	@echo "	<configSections>" >> bin/continuum.exe.config
	@echo "		<section name = \"logger\" type = \"Shared.Log+LoggerSection,shared\"/>" >> bin/continuum.exe.config
	@echo "	</configSections>" >> bin/continuum.exe.config
	@echo "	" >> bin/continuum.exe.config
	@echo "	<!-- This is used to control logging for the various plugins. The level" >> bin/continuum.exe.config
	@echo "	may be Off, Error, Warning, Info, or Verbose. Categories default to using Warning" >> bin/continuum.exe.config
	@echo "	but you can change this by adding a category entry whose name is \"*\". -->" >> bin/continuum.exe.config
	@echo "	<logger>" >> bin/continuum.exe.config
	@echo "		<categories>" >> bin/continuum.exe.config
	@echo "			<add name = \"App\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"AutoComplete\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"ContextMenu\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"CsParser\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Database\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Debugger\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Errors\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"FindInDatabase\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"LocalsParser\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"ObjectModel\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Open Selection\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Parser\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Refactor Commands\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Refactor Evaluate\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Startup\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Styler\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"TraceRoots\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "		</categories>" >> bin/continuum.exe.config
	@echo "	</logger>" >> bin/continuum.exe.config
	@echo "	" >> bin/continuum.exe.config
	@echo "	<!-- The standard System.Diagnostics.Trace logging goo. We default to removing the" >> bin/continuum.exe.config
	@echo "	DefaultTraceListener (note that the code adds a custom listener to force asserts to" >> bin/continuum.exe.config
	@echo "	throw). And we setup another custom listener to log trace messages to continuum.log " >> bin/continuum.exe.config
	@echo "	in the working directory. Note that unlike the system listeners PrettyTraceListener " >> bin/continuum.exe.config
	@echo "	supports the Timestamp and ThreadId traceOutputOptions. To enable these add an" >> bin/continuum.exe.config
	@echo "	attribute like 'traceOutputOptions = \"Timestamp,ThreadId\"' to the logger element. -->" >> bin/continuum.exe.config
	@echo "	<system.diagnostics>" >> bin/continuum.exe.config
	@echo "		<trace autoflush = \"true\" indentsize = \"4\">" >> bin/continuum.exe.config
	@echo "			<listeners>" >> bin/continuum.exe.config
	@echo "				<remove name = \"Default\"/>" >> bin/continuum.exe.config
	@echo "				<add name = \"logger\" type = \"Continuum.PrettyTraceListener,continuum\" initializeData = \"/tmp/continuum.log\"/>" >> bin/continuum.exe.config
	@echo "			</listeners>" >> bin/continuum.exe.config
	@echo "		</trace>" >> bin/continuum.exe.config
	@echo "	</system.diagnostics>" >> bin/continuum.exe.config
	@echo "</configuration>" >> bin/continuum.exe.config
	@echo "" >> bin/continuum.exe.config
	
smokey_flags := --not-localized -set:naming:jurassic -set:dictionary:Dictionary.txt
smokey_flags += -exclude-check:C1030	# UnusedArg
smokey_flags += -exclude-check:D1020	# NativeMethods
smokey_flags += -exclude-check:D1047	# TooManyArgs
smokey_flags += -exclude-check:P1003	# AvoidBoxing
smokey_flags += -exclude-check:P1004	# AvoidUnboxing
smokey_flags += -exclude-check:P1005	# StringConcat
smokey_flags += -exclude-check:P1022	# PropertyReturnsCollection

gendarme_flags := --severity all --confidence all --ignore gendarme.ignore --quiet
gendarme: app
	@-$(GENDARME) $(gendarme_flags) $(smoke-files)
	
help:
	@echo "continuum version $(version)"
	@echo " "
	@echo "The primary targets are:"
	@echo "plugins          - build the plugins"
	@echo "app              - build the app"
	@echo "run-app          - run the app"
	@echo "gendarme         - run gendarme on the app and plugins"
	@echo "update-libraries - copy the external lib dependencies into bin"
	@echo " "
	@echo "Variables include:"
	@echo "RELEASE - define to enable release builds, defaults to not defined"

