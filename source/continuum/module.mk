# ------------------------------------------------------------------------
# Variables
exe-name := continuum
app-name := Continuum

app-path := bin/$(app-name).app
contents-path := $(app-path)/Contents
macos-path := $(contents-path)/MacOS
exe-path := bin/$(exe-name).exe

program-targets += $(app-path)
smoke-files += $(exe-path)
clean-files += $(exe-path) $(exe-path).mdb
clean-dirs += $(app-path)

source-files := bin/$(exe-name)-sources

# ------------------------------------------------------------------------
# Binary targets 	

# assembly 
$(source-files): source/continuum/*.cs source/AssemblyVersion.cs 
	@echo "$^" > $@

$(exe-path): $(source-files) bin/csc_flags $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:exe @$<

# bundle
other-files += bin/mobjc-glue.dylib bin/plugins

nib-resources := $(shell echo $(strip $(nib-files)) | sed "s/ /,/g")
other-resources := $(shell echo $(strip $(other-files)) | sed "s/ /,/g")

$(app-path): $(exe-path) source/plugins/app/Info.plist $(other-files) $(nib-files) $(plugin-targets)
	@echo "building $(app-path)"
	@$(PACK) --app=$(app-path) --exe=$(exe-path) --mono-flags='$(MONO_FLAGS)' --plist=source/plugins/app/Info.plist           \
	--resources=$(ui-resources),$(other-resources) --resources=English.lproj:$(nib-resources) \
	--vars=APPNAME:$(app-name),VERSION:$(version),BUILDNUM:$(build-num)
