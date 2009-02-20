# ------------------------------------------------------------------------
# Variables
lib-name := directory-editor
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path1 := bin/$(lib-name).nib
nib-path2 := bin/dir-prefs.nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)			
source-files := bin/$(lib-name)-sources

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path1) $(nib-path2)
other-files += source/plugins/$(lib-name)/Build.png source/plugins/$(lib-name)/Cancel.png
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@
	
$(nib-path1): source/plugins/$(lib-name)/$(lib-name).nib
	rm -rf $@
	cp -R $^ $@
	
$(nib-path2): source/plugins/$(lib-name)/dir-prefs.nib
	rm -rf $@
	cp -R $^ $@
	
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs 
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:library @$<
