# ------------------------------------------------------------------------
# Public variables
CSC ?= gmcs
MONO ?= mono
PACK ?= cocoa-pack
SMOKE ?= smoke
NUNIT ?= nunit-console2

# TODO: don't hard code these paths
GENDARME ?= /Users/jessejones/Source/mono-tools/gendarme/bin/gendarme.exe
GENDARME_RULES ?= rules.xml

ifdef RELEASE
	CSC_FLAGS += -checked+ -debug+ -warn:4 -nowarn:1591 -optimize+ -d:TRACE
	MONO_FLAGS += --desktop
else
	CSC_FLAGS += -checked+ -debug+ -warnaserror+ -warn:4 -nowarn:1591 -d:DEBUG -d:TRACE
	MONO_FLAGS += --desktop --debug
endif

# ------------------------------------------------------------------------
# Internal variables
dummy := $(shell mkdir bin 2> /dev/null)
dummy := $(shell mkdir bin/plugins 2> /dev/null)
dummy := $(shell if [[ "$(CSC_FLAGS)" != `cat bin/csc_flags 2> /dev/null` ]]; then echo "$(CSC_FLAGS)" > bin/csc_flags; fi)

base_version := 0.2.xxx.0										# major.minor.build.revision
version := $(shell ./get_version.sh $(base_version) build_num)	# this will increment the build number stored in build_num
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
all: $(program-targets)

plugins: $(plugin-targets)

app: $(program-targets) bin/continuum.exe.config

# Note that running this way (instead of via open or the Finder) allows us to see 
# console output in the terminal instead of the system log.
run-app: app
	$(macos-path)/launcher
	
test: bin/tests.dll
	cd bin && "$(NUNIT)" -nologo tests.dll

test1: bin/tests.dll
	cd bin && "$(NUNIT)" -nologo -fixture=$(TEST1) tests.dll

# ------------------------------------------------------------------------
# Misc targets
keys:
	sn -k keys
	
bin/test-files: $(test-files)
	@echo "$^" > $@

bin/tests.dll: bin/test-files $(gear-dll) bin/csc_flags
	$(CSC) -out:$@ $(CSC_FLAGS) -unsafe -d:TEST -pkg:mono-nunit -r:$(cocoa-dlls),$(gear-dll),ICSharpCode.SharpZipLib.dll,Mono.Posix.dll,bin/Mono.Cecil.dll,bin/Mono.Cecil.Mdb.dll,System.Configuration.dll -target:library @bin/test-files

update-libraries:
	cp `pkg-config --variable=Libraries mobjc` bin
	cp `pkg-config --variable=Libraries mcocoa` bin
	cp `pkg-config --variable=Libraries gear` bin
	
tar-bin:
	tar --create --compress --file=Continuum-$(version).tar.gz \
		README bin/Continuum.app

clean:
	-rm bin/csc_flags
	-rm $(clean-files)
	-rm -rf $(clean-dirs)
	-rm -rf bin/plugins
	-rm bin/*-sources
	-rm -rf bin/*.nib

bin/continuum.exe.config:
	@echo "generating bin/continuum.exe.config"
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
	@echo "			<add name = \"App\" level = \"Verbose\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Database\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Errors\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"FindInDatabase\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"ObjectModel\" level = \"Verbose\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Open Selection\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Refactor Commands\" level = \"Info\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Refactor Evaluate\" level = \"Warning\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Startup\" level = \"Verbose\"/>" >> bin/continuum.exe.config
	@echo "			<add name = \"Styler\" level = \"Warning\"/>" >> bin/continuum.exe.config
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
	@echo "		<trace autoflush = \"true\" indentsize = \"4\">	" >> bin/continuum.exe.config
	@echo "			<listeners>" >> bin/continuum.exe.config
	@echo "				<remove name = \"Default\"/>" >> bin/continuum.exe.config
	@echo "				<add name = \"logger\" type = \"Continuum.PrettyTraceListener,continuum\" initializeData = \"$(here)/continuum.log\"/>" >> bin/continuum.exe.config
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

smoke: app
	@-list='$(smoke-files)'; for f in $$list; do	\
		$(SMOKE) $(smokey_flags) $$f;				\
	done
	
gendarme_flags := --severity all --confidence all --ignore gendarme.ignore --quiet
gendarme: app
	@-$(MONO) $(GENDARME) $(gendarme_flags) --config $(GENDARME_RULES) $(smoke-files)
	
help:
	@echo "continuum version $(version)"
	@echo " "
	@echo "The primary targets are:"
	@echo "plugins          - build the plugins"
	@echo "app              - build the app"
	@echo "run-app          - run the app"
	@echo "smoke            - run smokey on the app and plugins"
	@echo "gendarme         - run gendarme on the app and plugins"
	@echo "update-libraries - copy the external lib dependencies into bin"
	@echo " "
	@echo "Variables include:"
	@echo "RELEASE - define to enable release builds, defaults to not defined"
	
tar:
	tar --exclude \*/.svn --exclude \*/.svn/\* --create --compress --file=continuum-$(version).tar.gz Dictionary.txt MIT.X11 Makefile gen_version.sh get_version.sh source

