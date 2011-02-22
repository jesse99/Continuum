# ------------------------------------------------------------------------
# Variables
exe-name := continuum
app-name := Continuum

app-path := bin/$(app-name).app
contents-path := $(app-path)/Contents
macos-path := $(contents-path)/MacOS
resources-path := $(contents-path)/Resources
exe-path := bin/$(exe-name).exe

program-targets += $(app-path)
smoke-files += $(exe-path)
clean-files += $(exe-path) $(exe-path).mdb
clean-dirs += $(app-path)

source-files := bin/$(exe-name)-sources

# ------------------------------------------------------------------------
# Binary targets

# assembly 
source/AssemblyVersion.cs: build_num
	@mgen_version.sh $(version) source/AssemblyVersion.cs

$(source-files): source/continuum/*.cs source/AssemblyVersion.cs
	@echo "$^" > $@

$(exe-path): $(source-files) bin/csc_flags $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:exe @$<

# bundle
other-files += bin/mobjc-glue.dylib bin/plugins bin/install-tool

nib-resources := $(shell echo $(strip $(nib-files)) | sed "s/ /,/g")
other-resources := $(shell echo $(strip $(other-files)) | sed "s/ /,/g")

# TODO: When running Continuum from the command line it will import all environment
# variables which are set in the shell, but that doesn't happen when launching Continuum
# from the Finder. This can cause build problems for make files that depend on things
# like /usr/local/bin being in the PATH. 
#
# For now we fix this using the --append-var option, but that isn't a very good solution.
# What we probably need to do is add support for project environment variables (we sort
# of have this now but the environment variables are pulled out of the make file instead 
# of added by the user. We'd probably have to do something similar for PATH so that
# the project will build on other machines without manual intervention).
#
# Another option would be to set the UseShellExecute property in the ProcessStartInfo, but
# we'd also have to do some fairly icky goo to get stdout and stderr to redirect properly.
$(app-path): $(exe-path) source/plugins/app/Info.plist $(other-files) $(nib-files) $(plugin-targets)
	@echo "building $(app-path)"
	@$(PACK) --app=$(app-path) --exe=$(exe-path) --mono-flags='$(MONO_FLAGS)' --plist=source/plugins/app/Info.plist           \
	--resources=$(ui-resources),$(other-resources),cocoa-pack --resources=English.lproj:$(nib-resources) \
	--vars=APPNAME:$(app-name),VERSION:$(version),BUILDNUM:$(build-num) \
	--append-var=PATH:/usr/local/bin --append-var=PKG_CONFIG_PATH:/usr/lib/pkgconfig:/usr/local/lib/pkgconfig \
	--require-mono=2.8
