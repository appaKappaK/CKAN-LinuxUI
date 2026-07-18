.PHONY: package clean test

DESTDIR:=../_build/deb/fakeroot
CONFIGURATION?=Release
LINUXGUISTAGE:=../_build/package/ckan-linux/linux-x64/usr
PACKAGESTAMP:=../_build/deb/package.stamp
CONTROLSRC:=control.in
CONTROLDEST:=$(DESTDIR)/DEBIAN/control
CHANGELOGSRC:=../CKAN-LINUX-CHANGELOG.md
CHANGELOGDEST:=$(DESTDIR)/usr/share/doc/ckan-linux/changelog.gz
COPYRIGHTSRC:=copyright
COPYRIGHTDEST:=$(DESTDIR)/usr/share/doc/ckan-linux/copyright
VERSION:=$(shell sed -n -E 's/^..[[:space:]]+v([^[:space:]]+).*/\1/p' ../CHANGELOG.md | head -1).$(shell git show -s --format='%cd' --date=format:'%y%j')
DEB:=../_build/deb/ckan-linux_$(VERSION)_amd64.deb

package: $(DEB)

$(DEB): $(PACKAGESTAMP) $(CONTROLDEST) $(CHANGELOGDEST) $(COPYRIGHTDEST)
	umask 0022 && mkdir -p $(shell dirname $@)
	fakeroot dpkg-deb --build $(DESTDIR)/ $@

$(LINUXGUISTAGE)/bin/ckan-linux:
	cd .. && ./build.sh LinuxGUIPackage --configuration=$(CONFIGURATION)

$(PACKAGESTAMP): $(LINUXGUISTAGE)/bin/ckan-linux
	rm -rf $(DESTDIR)/usr
	umask 0022 && mkdir -p $(DESTDIR)
	umask 0022 && cp -a $(LINUXGUISTAGE) $(DESTDIR)/
	touch $@

$(CONTROLDEST): $(CONTROLSRC) ../CHANGELOG.md
	umask 0022 && mkdir -p $(shell dirname $@)
	umask 0022 && sed -e 's/@VERSION@/$(VERSION)/' $< > $@

$(CHANGELOGDEST): $(CHANGELOGSRC)
	umask 0022 && mkdir -p $(shell dirname $@)
	umask 0022 && gzip --best -n -c $< > $@

$(COPYRIGHTDEST): $(COPYRIGHTSRC)
	umask 0022 && mkdir -p $(shell dirname $@)
	umask 0022 && cp $< $@

clean:
	rm -rf ../_build/deb

test: $(DEB)
	dpkg-deb --info $<
	dpkg-deb --contents $<
