Name: ckan-linux
Version: %{_version}
Release: 1%{?dist}
Summary: Linux desktop mod manager for Kerbal Space Program
URL: https://github.com/appaKappaK/CKAN-LinuxUI
Packager: CKAN Linux contributors
License: MIT
Requires: fontconfig, libICE, libSM, libX11, libXext
BuildArch: x86_64
Source0: ckan-linux.tar.gz

%description
CKAN Linux is an Avalonia desktop client for browsing, installing, updating,
and removing Kerbal Space Program mods from the CKAN catalog.

%prep

%build

%install
umask 0022
mkdir -p %{buildroot}
tar -xzf %{SOURCE0} -C %{buildroot}

%files
%{_bindir}/ckan-linux
/usr/lib/ckan-linux
%{_datadir}/applications/ckan-linux.desktop
%{_datadir}/icons/hicolor/*/apps/ckan-linux.png
%{_datadir}/icons/hicolor/*/apps/CKAN-LinuxGUI.png
%{_datadir}/icons/hicolor/*/apps/ckan-linuxgui.png
%{_datadir}/doc/ckan-linux/README.md

%changelog
* Sat Jul 18 2026 CKAN Linux contributors
- Replace the legacy Mono multi-interface package with the Avalonia Linux app.
