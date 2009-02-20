# ------------------------------------------------------------------------
# Variables
lib-name := make-builder
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path1 := bin/make-flags.nib
nib-path2 := bin/build-variables.nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)			
source-files := bin/$(lib-name)-sources

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path1)  $(nib-path2)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@
		
$(nib-path1): source/plugins/$(lib-name)/make-flags.nib
	rm -rf $@
	cp -R $^ $@
	
$(nib-path2): source/plugins/$(lib-name)/build-variables.nib
	rm -rf $@
	cp -R $^ $@
	
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs 
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:library @$<
