{ lib, buildDotnetModule, dotnetCorePackages, fontconfig, freetype
, libGL, libX11, libXcursor, libXi, libXrandr, libICE, libSM, libxcb
}:

let
  versionLine = lib.findFirst (line: lib.hasPrefix "## v" line)
    (throw "No version found in CHANGELOG.md")
    (lib.splitString "\n" (builtins.readFile ../CHANGELOG.md));
  version = builtins.head (builtins.match "## v([^ ]+).*" versionLine);
in
buildDotnetModule {
  pname = "ckan-linux";
  inherit version;
  src = lib.cleanSourceWith {
    src = ../.;
    filter = path: type:
      lib.cleanSourceFilter path type
      && path != toString ../. + "/_build"
      && path != toString ../. + "/result";
  };
  projectFile = "LinuxGUI/CKAN-LinuxGUI.csproj";
  nugetDeps = ./nuget-deps.json;
  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;
  dotnetRestoreFlags = [ "-p:Configuration=Release" ];
  executables = [ "CKAN-LinuxGUI" ];
  runtimeDeps = [ fontconfig freetype libGL libX11 libXcursor libXi libXrandr libICE libSM libxcb ];

  postPatch = ''
    mkdir -p _build/meta
    cat > _build/meta/GlobalAssemblyVersionInfo.cs <<'EOF'
    using System.Reflection;
    [assembly: AssemblyVersion("${lib.versions.majorMinor version}")]
    [assembly: AssemblyFileVersion("${lib.versions.pad 3 version}")]
    [assembly: AssemblyInformationalVersion("${version}")]
    EOF
  '';

  postInstall = ''
    mkdir -p "$out/share/applications" "$out/share/icons/hicolor/256x256/apps"
    substitute LinuxGUI/packaging/ckan-linux.desktop "$out/share/applications/ckan-linux.desktop" \
      --replace-fail /usr/bin/ckan-linux "$out/bin/ckan-linux" \
      --replace-fail 'Icon=ckan-linux' "Icon=$out/share/icons/hicolor/256x256/apps/ckan-linux.png"
    install -m644 assets/ckan-256.png "$out/share/icons/hicolor/256x256/apps/ckan-linux.png"
  '';

  postFixup = ''
    mv "$out/bin/CKAN-LinuxGUI" "$out/bin/ckan-linux"
  '';

  meta = {
    description = "CKAN Linux desktop application";
    homepage = "https://github.com/appaKappaK/CKAN-LinuxUI";
    license = lib.licenses.mit;
    platforms = [ "x86_64-linux" ];
    mainProgram = "ckan-linux";
  };
}
