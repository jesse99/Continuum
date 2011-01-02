# ------------------------------------------------------------------------
# Variables
lib-name := text-view
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)
source-files := bin/$(lib-name)-sources

plugin-targets += $(lib-path) $(xml-path)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@

$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources) -target:library @$<
