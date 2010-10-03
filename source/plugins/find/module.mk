# ------------------------------------------------------------------------
# Variables
lib-name := find
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path1 := bin/$(lib-name).nib
nib-path2 := bin/find-in-files.nib
nib-path3 := bin/find-progress.nib
nib-path4 := bin/find-results.nib
nib-path5 := bin/find-in-files-options.nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)			
source-files := bin/$(lib-name)-sources

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path1) $(nib-path2) $(nib-path3) $(nib-path4) $(nib-path5)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@
	
$(nib-path1): source/plugins/$(lib-name)/$(lib-name).nib
	rm -rf $@
	cp -R $^ $@
		
$(nib-path2): source/plugins/$(lib-name)/find-in-files.nib
	rm -rf $@
	cp -R $^ $@
		
$(nib-path3): source/plugins/$(lib-name)/find-progress.nib
	rm -rf $@
	cp -R $^ $@
		
$(nib-path4): source/plugins/$(lib-name)/find-results.nib
	rm -rf $@
	cp -R $^ $@
		
$(nib-path5): source/plugins/$(lib-name)/find-in-files-options.nib
	rm -rf $@
	cp -R $^ $@
		
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs 
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:library @$<
