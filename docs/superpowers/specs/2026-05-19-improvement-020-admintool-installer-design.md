# IMPROVEMENT-020 — AdminTool Installer Design

**Date:** 2026-05-19
**Status:** Approved
**Area:** Admin Tool / Distribution

---

## 1. Goal

Create a WiX v5 MSI installer for the Perpetuum AdminTool targeting a non-technical audience. The installer bundles the complete self-contained .NET 8 runtime, installs to Program Files, creates Start Menu and Desktop shortcuts, and produces an artifact in CI on every push to `develop`.

In-place upgrades are handled by running the new installer over the old installation (`MajorUpgrade`). No in-app update check is required.

---

## 2. Decisions

| Question | Decision |
|---|---|
| Installer technology | WiX Toolset v5 — matches `Perpetuum.ServerService2Installer` |
| Runtime bundling | Self-contained publish (`SelfContained=true`, `RuntimeIdentifier=win-x64`) |
| Update mechanism | Re-run new installer (MajorUpgrade pattern) |
| Shortcuts | Start Menu + Desktop (mandatory, no user choice) |
| CI trigger | Same as server: artifact uploaded on `push` to `develop` |

---

## 3. Project Structure

New project at `src/Perpetuum.AdminToolInstaller/` mirroring the server installer:

```
src/Perpetuum.AdminToolInstaller/
  Perpetuum.AdminToolInstaller.wixproj
  Package.wxs
  Package.en-us.wxl
  Folders.wxs
  AdminToolComponents.wxs
```

Added to `PerpetuumServer2.sln`.

---

## 4. AdminTool csproj Changes

`src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj` gains a Release-scoped property group:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

Debug builds remain framework-dependent for faster developer iteration.

---

## 5. WiX File Contents

### `Perpetuum.AdminToolInstaller.wixproj`

```xml
<Project Sdk="WixToolset.Sdk/5.0.2">
  <PropertyGroup>
    <Platforms>x64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="WixToolset.UI.wixext" Version="5.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj" />
  </ItemGroup>
</Project>
```

### `Package.wxs`

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="Perpetuum Admin Tool" Manufacturer="Open Perpetuum"
           Version="1.0.0.0" UpgradeCode="{GENERATE-FRESH-GUID}">
    <MajorUpgrade DowngradeErrorMessage="!(loc.DowngradeError)" />
    <Icon Id="AppIcon.ico" SourceFile="$(var.Perpetuum.AdminTool.TargetDir)Perpetuum.AdminTool.exe" />
    <Property Id="ARPPRODUCTICON" Value="AppIcon.ico" />
    <ui:WixUI Id="WixUI_InstallDir" InstallDirectory="INSTALLFOLDER" />
    <Feature Id="Main">
      <ComponentGroupRef Id="AdminToolComponents" />
    </Feature>
  </Package>
</Wix>
```

### `Package.en-us.wxl`

```xml
<WixLocalization xmlns="http://wixtoolset.org/schemas/v4/wxl" Culture="en-US">
  <String Id="DowngradeError">A newer version of Perpetuum Admin Tool is already installed.</String>
</WixLocalization>
```

### `Folders.wxs`

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <Directory Id="INSTALLFOLDER" Name="PerpetuumAdminTool">
    </Directory>
  </Fragment>
</Wix>
```

`ProgramFiles6432Folder`, `ProgramMenuFolder`, and `DesktopFolder` are predefined WiX v5 system folder IDs — no explicit declaration needed, matching the server installer's `Folders.wxs` pattern.

### `AdminToolComponents.wxs`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="AdminToolComponents" Directory="INSTALLFOLDER">
      <Files Include="$(var.Perpetuum.AdminTool.TargetDir)**" />

      <Component Id="ShortcutStartMenu" Guid="{GENERATE-FRESH-GUID-A}">
        <Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder"
                  Name="Perpetuum Admin Tool"
                  Target="[INSTALLFOLDER]Perpetuum.AdminTool.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\OpenPerpetuum\AdminTool"
                       Name="installed" Type="integer" Value="1" KeyPath="yes" />
      </Component>

      <Component Id="ShortcutDesktop" Guid="{GENERATE-FRESH-GUID-B}">
        <Shortcut Id="DesktopShortcut" Directory="DesktopFolder"
                  Name="Perpetuum Admin Tool"
                  Target="[INSTALLFOLDER]Perpetuum.AdminTool.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\OpenPerpetuum\AdminTool"
                       Name="desktop" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </ComponentGroup>
  </Fragment>
</Wix>
```

---

## 6. CI Workflow Changes

Add a `build-admintool-installer` job to `.github/workflows/dotnet.yml` running in parallel with the existing `build` job:

```yaml
build-admintool-installer:
  runs-on: windows-latest
  env:
    Workspace: ${{ github.workspace }}
  steps:
    - uses: actions/checkout@v4
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 8.0.x
    - name: Install WiX toolset
      run: dotnet tool install --global wix --version 5.0.2
    - name: Add WiX UI extension
      run: wix extension add WixToolset.UI.wixext/5.0.2
    - name: Restore dependencies
      run: dotnet restore
    - name: Build AdminTool installer
      run: dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj --no-restore --configuration Release --verbosity quiet -p:Platform=x64
    - name: Upload AdminTool installer
      uses: actions/upload-artifact@v4
      with:
        name: Perpetuum-AdminTool-Installer-${{ github.sha }}
        path: src/Perpetuum.AdminToolInstaller/bin/x64/Release/en-US/*.msi
      if: ${{ github.event_name == 'push' }}
```

---

## 7. Files Changed

| File | Change |
|---|---|
| `src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj` | New |
| `src/Perpetuum.AdminToolInstaller/Package.wxs` | New |
| `src/Perpetuum.AdminToolInstaller/Package.en-us.wxl` | New |
| `src/Perpetuum.AdminToolInstaller/Folders.wxs` | New |
| `src/Perpetuum.AdminToolInstaller/AdminToolComponents.wxs` | New |
| `src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj` | Add Release self-contained property group |
| `PerpetuumServer2.sln` | Add new installer project |
| `.github/workflows/dotnet.yml` | Add `build-admintool-installer` job |

---

## 8. Validation Steps

1. Build locally: `dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj -c Release -p:Platform=x64`
2. Verify `.msi` appears at `src/Perpetuum.AdminToolInstaller/bin/x64/Release/en-US/`
3. Run the installer on a clean Windows machine (or VM) with no .NET runtime installed — AdminTool should launch without any prereq prompts
4. Verify Start Menu and Desktop shortcuts are created
5. Run the installer a second time (simulate upgrade) — existing install should be replaced without error
6. Uninstall via Add/Remove Programs — verify all files and shortcuts are removed

---

## 9. Risks

- **Self-contained output size:** ~150 MB install. Acceptable for this audience; no mitigation needed.
- **`SelfContained=true` on AdminTool csproj:** scoped to Release only — Debug builds are unaffected. Verify existing CI server build job is not impacted (it builds `Perpetuum.ServerService2.csproj`, not the AdminTool).
- **WiX extension caching in CI:** `wix extension add` is per-user; on a fresh runner it will always download. This is expected and takes only a few seconds.
