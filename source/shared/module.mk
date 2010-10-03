# ------------------------------------------------------------------------
# Variables
lib-name := shared
lib-path := bin/$(lib-name).dll
nib-path1 := bin/get-string.nib
nib-path2 := bin/get-text.nib
nib-path3 := bin/get-item.nib

smoke-files += $(lib-path)
clean-files += $(lib-path) $(lib-path).mdb
test-files += $(filter-out source/shared/AssemblyInfo.cs,$(wildcard source/shared/*.cs)) source/shared/interfaces/*.cs

source-files := bin/$(lib-name)-sources
nib-files += $(nib-path1) $(nib-path2) $(nib-path3)

# ------------------------------------------------------------------------
# Binary targets 	
gmcs-resources := $(gear-dll),bin/mobjc.dll,bin/mcocoa.dll
gmcs-files := $(shell echo $(gmcs-resources) | sed "s/,/ /g")

$(nib-path1): source/shared/get-string.nib
	rm -rf $@
	cp -R $^ $@

$(nib-path2): source/shared/get-text.nib
	rm -rf $@
	cp -R $^ $@

$(nib-path3): source/shared/get-item.nib
	rm -rf $@
	cp -R $^ $@

$(source-files): source/AssemblyVersion.cs source/shared/*.cs  source/shared/interfaces/*.cs
	@echo "$^" > $@

$(lib-path): $(source-files) bin/csc_flags $(gmcs-files)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(gmcs-resources),$(gear-dll),System.Configuration.dll,Mono.Posix.dll,bin/Mono.Cecil.dll,bin/Mono.Cecil.Mdb.dll -target:library @$<
