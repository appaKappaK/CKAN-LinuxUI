# CKAN Linux Packages

The repository can build local Debian and RPM packages for the self-contained
`ckan-linux` desktop application. A public package repository is not currently
published by this fork.

Build either format from the repository root:

```bash
./build.sh deb --configuration=Release
./build.sh rpm --configuration=Release
```

Debian-family builders need `make`, `fakeroot`, and `dpkg-deb` (provided by
the `dpkg` package). RPM-family builders need `make`, `rpmbuild`, and
`rpmlint`. For example:

```bash
sudo apt install make fakeroot dpkg
sudo dnf install make rpm-build rpmlint
```

Generated packages are written below `_build/deb/` and `_build/rpm/`. Package
installation does not replace an existing upstream `ckan` command. The optional
CLI is built separately with `./build.sh CLI` and is not bundled into the
desktop packages.
