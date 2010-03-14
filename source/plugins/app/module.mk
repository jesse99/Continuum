# ------------------------------------------------------------------------
# Variables
lib-name := app
lib-path := $(plugins-path)/$(lib-name)/$(lib-name).dll
xml-path := $(plugins-path)/$(lib-name)/Bosses.xml
nib-path1 := bin/MainMenu.nib
nib-path2 := bin/Preferences.nib

dummy := $(shell mkdir $(plugins-path)/$(lib-name) 2> /dev/null)
source-files := bin/$(lib-name)-sources

app-path := source/plugins/$(lib-name)
sdf-path := $(app-path)/Continuum.sdef

plugin-targets += $(lib-path) $(xml-path)
nib-files += $(nib-path1) $(nib-path2)
other-files += $(app-path)/AppIcon.icns $(app-path)/scripts $(app-path)/refactors $(sdf-path)
smoke-files += $(lib-path)

# ------------------------------------------------------------------------
# Targets
$(xml-path): $(app-path)/Bosses.xml
	cp $^ $@

$(nib-path1): $(app-path)/MainMenu.nib
	rm -rf $@
	cp -R $^ $@

$(nib-path2): $(app-path)/Preferences.nib
	rm -rf $@
	cp -R $^ $@

$(source-files): source/AssemblyVersion.cs $(app-path)/*.cs
	@echo "$^" > $@

$(lib-path): $(source-files) $(ui-files) $(app-path)/Continuum.sdef
	xmllint --noout --dtdvalid /System/Library/DTDs/sdef.dtd --xinclude --nowarning $(sdf-path)
	$(CSC) -out:$@ $(CSC_FLAGS) -r:$(ui-resources),Mono.Posix.dll -target:library @$<
