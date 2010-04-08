# ------------------------------------------------------------------------
# Variables
lib-name := debugger
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path := bin/$(lib-name).nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)
source-files := bin/$(lib-name)-sources
test-files += $(filter-out source/plugins/$(lib-name)/AssemblyInfo.cs,$(wildcard source/plugins/$(lib-name)/*.cs))

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@

$(nib-path): source/plugins/$(lib-name)/$(lib-name).nib
	rm -rf $@
	cp -R $^ $@
	
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -unsafe -r:$(ui-resources),bin/Mono.Cecil.dll,bin/Mono.Cecil.Mdb.dll,bin/Mono.Debugger.Soft.dll -target:library @$<
