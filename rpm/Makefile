.PHONY: package clean test

TOPDIR:=$(shell pwd)/../_build/rpm
CONFIGURATION?=Release
LINUXGUISTAGE:=$(shell pwd)/../_build/package/ckan-linux/linux-x64
SOURCE:=$(TOPDIR)/SOURCES/ckan-linux.tar.gz
CHANGELOGSRC:=../CHANGELOG.md
VERSION:=$(shell sed -n -E 's/^..[[:space:]]+v([^[:space:]]+).*/\1/p' $(CHANGELOGSRC) | head -1 | sed -e 's/-/_/g').$(shell git show -s --format='%cd' --date=format:'%y%j')
RPM:=$(TOPDIR)/RPMS/x86_64/ckan-linux-$(VERSION)-1.x86_64.rpm

package: $(RPM)

$(RPM): $(SOURCE) ckan.spec
	rpmbuild --define "_topdir ${TOPDIR}" --define "_version $(VERSION)" -bb ckan.spec

$(SOURCE): $(LINUXGUISTAGE)/usr/bin/ckan-linux
	mkdir -p "$(TOPDIR)/SOURCES"
	tar -C "$(LINUXGUISTAGE)" -czf "$@" .

$(LINUXGUISTAGE)/usr/bin/ckan-linux:
	cd .. && ./build.sh LinuxGUIPackage --configuration=$(CONFIGURATION)

clean:
	rm -rf "$(TOPDIR)"

test: $(RPM)
	rpm -qlp $<
	rpmlint $<
