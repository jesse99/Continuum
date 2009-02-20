# ------------------------------------------------------------------------
# Variables
lib-name := object-model
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)			
source-files := bin/$(lib-name)-sources
other-files += bin/Mono.Cecil.dll

plugin-targets += $(lib-path) $(xml-path)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): source/plugins/$(lib-name)/Bosses.xml
	cp $^ $@
		
$(source-files): source/AssemblyVersion.cs source/plugins/$(lib-name)/*.cs 
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files) bin/Mono.Cecil.dll
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources),bin/Mono.Cecil.dll,bin/Mono.Cecil.Mdb.dll -target:library @$<
