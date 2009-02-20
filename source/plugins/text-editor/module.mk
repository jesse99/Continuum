# ------------------------------------------------------------------------
# Variables
lib-name := text-editor
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path1 := bin/$(lib-name).nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)	
source-files := bin/$(lib-name)-sources

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path1)
smoke-files += $(lib-path)
other-files += source/plugins/$(lib-name)/UnicodeNames.txt.gz
test-files += source/plugins/$(lib-name)/*.cs

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@
	
$(nib-path1): source/plugins/$(lib-name)/$(lib-name).nib
	rm -rf $@
	cp -R $^ $@
	
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs 
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources),bin/Mono.Cecil.dll,ICSharpCode.SharpZipLib.dll -target:library @$<


