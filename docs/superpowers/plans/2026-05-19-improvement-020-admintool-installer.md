# AdminTool Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a WiX v5 MSI installer for Perpetuum.AdminTool that bundles the self-contained .NET 8 runtime, installs to Program Files, creates Start Menu and Desktop shortcuts, and produces an artifact in CI.

**Architecture:** Mirror the existing `Perpetuum.ServerService2Installer` WiX project structure. AdminTool.csproj gets Release-only `SelfContained=true` so the WiX project picks up a self-contained publish output. The installer project is wired into the solution and CI runs it as a parallel job.

**Tech Stack:** WiX Toolset v5 (`WixToolset.Sdk/5.0.2`), `WixToolset.UI.wixext/5.0.2`, .NET 8, MSBuild, GitHub Actions

---

## File Map

| Action | File |
|---|---|
| Modify | `src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj` |
| Create | `src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj` |
| Create | `src/Perpetuum.AdminToolInstaller/Package.wxs` |
| Create | `src/Perpetuum.AdminToolInstaller/Package.en-us.wxl` |
| Create | `src/Perpetuum.AdminToolInstaller/Folders.wxs` |
| Create | `src/Perpetuum.AdminToolInstaller/AdminToolComponents.wxs` |
| Modify | `PerpetuumServer2.sln` |
| Modify | `.github/workflows/dotnet.yml` |

---

## Task 1: Add self-contained publish to AdminTool.csproj

**Files:**
- Modify: `src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj`

This enables the WiX installer to bundle the complete .NET 8 runtime. Scoped to Release only so Debug builds remain framework-dependent and fast.

- [ ] **Step 1: Edit AdminTool.csproj**

Open `src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj` and add the following `PropertyGroup` after the existing one:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>annotations</Nullable>
    <Platforms>x64</Platforms>
    <RootNamespace>Perpetuum.AdminTool</RootNamespace>
    <AssemblyName>Perpetuum.AdminTool</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.1" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Perpetuum\Perpetuum.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify Debug build is unaffected**

Run:
```powershell
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64 --verbosity quiet
```

Expected output ends with:
```
Build succeeded.
```

- [ ] **Step 3: Commit**

```powershell
git add src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj
git commit -m "feat(admintool): enable self-contained publish for Release configuration"
```

---

## Task 2: Create WiX installer project files

**Files:**
- Create: `src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj`
- Create: `src/Perpetuum.AdminToolInstaller/Package.wxs`
- Create: `src/Perpetuum.AdminToolInstaller/Package.en-us.wxl`
- Create: `src/Perpetuum.AdminToolInstaller/Folders.wxs`
- Create: `src/Perpetuum.AdminToolInstaller/AdminToolComponents.wxs`

The WiX SDK is a NuGet MSBuild SDK — no separate WiX CLI installation is required for builds. The `ProjectReference` in the `.wixproj` causes WiX to automatically pick up the AdminTool's build output directory (`$(var.Perpetuum.AdminTool.TargetDir)**`).

- [ ] **Step 1: Create `Perpetuum.AdminToolInstaller.wixproj`**

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

Save to: `src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj`

- [ ] **Step 2: Create `Package.wxs`**

The `UpgradeCode` GUID must never change after first release — it is the permanent identity of this installer. The value below is pre-generated and unique; do not reuse it for any other installer.

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="Perpetuum Admin Tool" Manufacturer="Open Perpetuum"
           Version="1.0.0.0" UpgradeCode="{3C8A7D92-1E4B-4F2A-8C6D-5B0E3A9F1D7C}">
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

Save to: `src/Perpetuum.AdminToolInstaller/Package.wxs`

- [ ] **Step 3: Create `Package.en-us.wxl`**

```xml
<WixLocalization xmlns="http://wixtoolset.org/schemas/v4/wxl" Culture="en-US">
  <String Id="DowngradeError">A newer version of Perpetuum Admin Tool is already installed.</String>
</WixLocalization>
```

Save to: `src/Perpetuum.AdminToolInstaller/Package.en-us.wxl`

- [ ] **Step 4: Create `Folders.wxs`**

`ProgramFiles6432Folder`, `ProgramMenuFolder`, and `DesktopFolder` are predefined WiX v5 system folder IDs — they do not need explicit declaration here.

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <Directory Id="INSTALLFOLDER" Name="PerpetuumAdminTool">
    </Directory>
  </Fragment>
</Wix>
```

Save to: `src/Perpetuum.AdminToolInstaller/Folders.wxs`

- [ ] **Step 5: Create `AdminToolComponents.wxs`**

The `<Files Include="...">` glob bundles every file from the AdminTool self-contained publish output. The two shortcut components each use a `RegistryValue` as their `KeyPath` — this is the standard WiX pattern for shortcut components; do not change it to a `FileKeyPath`.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="AdminToolComponents" Directory="INSTALLFOLDER">
      <Files Include="$(var.Perpetuum.AdminTool.TargetDir)**" />

      <Component Id="ShortcutStartMenu" Guid="{6D4B2A8E-3C9F-4E1D-A7B5-0F8C2E6B4D9A}">
        <Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder"
                  Name="Perpetuum Admin Tool"
                  Target="[INSTALLFOLDER]Perpetuum.AdminTool.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\OpenPerpetuum\AdminTool"
                       Name="installed" Type="integer" Value="1" KeyPath="yes" />
      </Component>

      <Component Id="ShortcutDesktop" Guid="{9A1E5C3D-7B4F-4D8E-B2A6-4C0D7E9B3F5A}">
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

Save to: `src/Perpetuum.AdminToolInstaller/AdminToolComponents.wxs`

- [ ] **Step 6: Commit all new WiX files**

```powershell
git add src/Perpetuum.AdminToolInstaller/
git commit -m "feat(installer): add AdminTool WiX installer project"
```

---

## Task 3: Add installer project to solution

**Files:**
- Modify: `PerpetuumServer2.sln`

The WiX project type GUID (`{B7DD6F7E-DEF8-4E67-B5B7-07EF123DB6F0}`) is the same as the server installer. The project is nested under the existing `src` solution folder (`{0BC991A1-133C-49ED-A141-80E2A906898B}`).

- [ ] **Step 1: Add project declaration to `PerpetuumServer2.sln`**

In `PerpetuumServer2.sln`, add the following line immediately after the existing server installer `EndProject` line (after `Project ... = "Perpetuum.ServerService2Installer" ... EndProject`):

```
Project("{B7DD6F7E-DEF8-4E67-B5B7-07EF123DB6F0}") = "Perpetuum.AdminToolInstaller", "src\Perpetuum.AdminToolInstaller\Perpetuum.AdminToolInstaller.wixproj", "{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B}"
EndProject
```

- [ ] **Step 2: Add configuration mappings**

In the `GlobalSection(ProjectConfigurationPlatforms)` section, add these four lines (place them after the existing `{ABBCAAB8...}` block for the server installer):

```
		{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B}.Debug|x64.ActiveCfg = Debug|x64
		{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B}.Debug|x64.Build.0 = Debug|x64
		{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B}.Release|x64.ActiveCfg = Release|x64
		{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B}.Release|x64.Build.0 = Release|x64
```

- [ ] **Step 3: Add to NestedProjects**

In the `GlobalSection(NestedProjects)` section, add this line (after the existing `{ABBCAAB8...}` line):

```
		{D3F8A2B1-6C9E-4D7F-A5B8-2E0C4F9A3D6B} = {0BC991A1-133C-49ED-A141-80E2A906898B}
```

- [ ] **Step 4: Commit**

```powershell
git add PerpetuumServer2.sln
git commit -m "chore(sln): add Perpetuum.AdminToolInstaller project to solution"
```

---

## Task 4: Build installer locally and verify

**Files:** (read-only verification)

WiX SDK packages are NuGet-based and restored automatically by `dotnet restore`. No separate WiX CLI installation is needed for MSBuild builds.

- [ ] **Step 1: Restore dependencies**

```powershell
dotnet restore PerpetuumServer2.sln -p:Platform=x64
```

Expected: exits with no errors.

- [ ] **Step 2: Build the installer in Release**

```powershell
dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj -c Release -p:Platform=x64 --verbosity normal
```

Expected output ends with:
```
Build succeeded.
```

The build takes a few minutes — it compiles the AdminTool self-contained (pulls ~150 MB of .NET runtime) then packages it.

- [ ] **Step 3: Verify .msi output exists**

```powershell
Get-Item "src\Perpetuum.AdminToolInstaller\bin\x64\Release\en-US\*.msi"
```

Expected: one `.msi` file listed (e.g. `Perpetuum.AdminToolInstaller.msi`).

- [ ] **Step 4: Smoke-test the installer**

Run the `.msi` on your machine (or a VM with no .NET runtime installed):
1. Double-click the `.msi` — the setup wizard should appear with the WixUI_InstallDir dialog ("Choose installation folder").
2. Accept the default path (`C:\Program Files\PerpetuumAdminTool\`) or change it; click Install.
3. After install completes, verify:
   - `Perpetuum Admin Tool` shortcut appears on the Desktop.
   - `Perpetuum Admin Tool` appears in the Start Menu.
   - `Perpetuum.AdminTool.exe` launches without any "missing .NET runtime" prompt.
4. Open **Add or Remove Programs** → uninstall → confirm all files and shortcuts are removed.

- [ ] **Step 5: Smoke-test the upgrade path**

Run the same installer a second time (simulate re-running to upgrade):

Expected: installer runs without error and replaces the existing installation. No "downgrade" error should appear.

---

## Task 5: Add AdminTool installer job to CI

**Files:**
- Modify: `.github/workflows/dotnet.yml`

The new job runs in parallel with the existing `build` job. It uploads the `.msi` as a CI artifact on push events.

- [ ] **Step 1: Add job to `.github/workflows/dotnet.yml`**

After the closing `build:` job block, add the following parallel job at the same indentation level:

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
    - name: Restore dependencies
      run: dotnet restore -p:Platform=x64
    - name: Build AdminTool installer
      run: dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj --no-restore --configuration Release --verbosity quiet -p:Platform=x64
    - name: Upload AdminTool installer
      uses: actions/upload-artifact@v4
      with:
        name: Perpetuum-AdminTool-Installer-${{ github.sha }}
        path: ${{ env.Workspace }}/src/Perpetuum.AdminToolInstaller/bin/x64/Release/en-US/*.msi
      if: ${{ github.event_name == 'push' }}
```

The complete updated `dotnet.yml` should look like:

```yaml
# This workflow will build a .NET project
# For more information see: https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net

name: Build Perpetuum.Server Service v2

on:
  push:
    branches: [ "develop" ]
  pull_request:
    branches: [ "develop" ]

jobs:
  build:

    runs-on: windows-latest

    env:
      Workspace: ${{ github.workspace }}
    
    steps:
    - uses: actions/checkout@v4
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 8.0.x
    - name: Restore dependencies
      run: dotnet restore
    - name: Build
      run: dotnet build src/Perpetuum.ServerService2/Perpetuum.ServerService2.csproj --no-restore --configuration Release --verbosity quiet -p:Platform=x64
    - name: Upload a Build Artifact
      uses: actions/upload-artifact@v4
      with:
        name: Perpetuum-Server-v2-${{ github.sha }}
        path: ${{ env.Workspace }}/bin/x64/Release/net8.0
      if: ${{ github.event_name == 'push'}}

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
    - name: Restore dependencies
      run: dotnet restore -p:Platform=x64
    - name: Build AdminTool installer
      run: dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj --no-restore --configuration Release --verbosity quiet -p:Platform=x64
    - name: Upload AdminTool installer
      uses: actions/upload-artifact@v4
      with:
        name: Perpetuum-AdminTool-Installer-${{ github.sha }}
        path: ${{ env.Workspace }}/src/Perpetuum.AdminToolInstaller/bin/x64/Release/en-US/*.msi
      if: ${{ github.event_name == 'push' }}
```

- [ ] **Step 2: Commit**

```powershell
git add .github/workflows/dotnet.yml
git commit -m "ci: add AdminTool installer build job"
```

---

## Task 6: Update backlog

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Mark IMPROVEMENT-020 as DONE**

In `docs/backlog/improvements.md`, update the IMPROVEMENT-020 entry:

```markdown
Status: DONE
```

Add the spec reference line below the status:

```markdown
Spec: `docs/superpowers/specs/2026-05-19-improvement-020-admintool-installer-design.md`
```

- [ ] **Step 2: Commit**

```powershell
git add docs/backlog/improvements.md
git commit -m "docs(backlog): mark IMPROVEMENT-020 done"
```

---

## Manual Validation Checklist

- [ ] Installer runs on a machine with no .NET runtime installed — AdminTool launches
- [ ] Desktop shortcut created automatically
- [ ] Start Menu shortcut created automatically
- [ ] Upgrade: running new installer over old silently replaces without error
- [ ] Uninstall: all files and shortcuts removed via Add/Remove Programs
- [ ] CI push to `develop` produces `Perpetuum-AdminTool-Installer-<sha>` artifact
