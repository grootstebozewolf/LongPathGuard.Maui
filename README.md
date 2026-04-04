# LongPathGuard.Maui

[![NuGet](https://img.shields.io/nuget/v/LongPathGuard.Maui.svg)](https://www.nuget.org/packages/LongPathGuard.Maui)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

A build-time MSBuild guard that **fails early and clearly** when any file in your project or NuGet packages has a path that risks exceeding the Windows 260-character `MAX_PATH` limit during `.NET MAUI` iOS / Mac Catalyst publish from Windows with a paired Mac build agent.

Instead of a cryptic `MSB3026 Could not find part of the path` buried deep in the iOS publish output, you get an immediate, actionable error that lists the offending files and tells you exactly how to fix it.

---

## The problem

Building `.NET MAUI` iOS targets **from Windows** (Visual Studio paired to a remote Mac) involves staging native assets — shaders, XCFrameworks, dSYM bundles, etc. — into a temporary path on the Windows side before they are transferred to the Mac:

```
C:\Users\you\AppData\Local\Temp\Xamarin\HotRestart\Signing\...\Payload\...\RuntimeCoreNet\resources\shaders\some_very_long_shader_name.metallib
```

When any file in your NuGet dependency tree has a long enough name **and** your solution lives in a deep folder, the combined path silently exceeds the classic Windows 260-character `MAX_PATH` limit. The build then fails with:

```
error MSB3026: Could not copy "..." to "...". Beginning retry 1 in 5000ms.
```

This is a **nested edge case** — it only happens when:

1. You are building a `net*-ios` or `net*-maccatalyst` target **from Windows**
2. Your solution folder is deep (e.g. `C:\Users\you\Documents\Work\Company\Projects\...`)
3. At least one NuGet package contains a file with a long name (any package — not just one specific vendor)

The Mac itself never sees the problem; the `MAX_PATH` limit is purely a Windows build-host issue.

**Real-world trigger that prompted this package:** ArcGIS Maps SDK for .NET 200.8.1 introduced new shader filenames like `rt_tile_draw_v_square_grid_bgnd_v_terr_occ_on_v_nodecal_v_vsclip_ps.metallib` that push many common Windows solution paths over the limit. Esri acknowledged the issue and planned shorter names in a future release. But the underlying Windows + MAUI iOS build constraint applies to _any_ package with long filenames.

---

## Installation

```xml
<PackageReference Include="LongPathGuard.Maui" Version="1.0.0" />
```

That's it. The guard runs automatically on every `Build`, `Publish`, and `Restore` for `ios` and `maccatalyst` target frameworks. No code changes required.

---

## What happens when it triggers

```
error : LongPathGuard.Maui FAILURE:

One or more files exceed the safe Windows path length.
This causes MSB3026 errors during MAUI iOS/MacCatalyst publish from Windows + paired Mac.

Offending files:
C:\Users\...\packages\some.package\1.0.0\resources\shaders\rt_tile_draw_v_...metallib

Solution root: C:\Users\you\Documents\Work\Company\Projects\MyApp (67 chars)

Fix options:
1. Move solution to short path e.g. C:\com\github\yourname\yourrepo\
2. Enable long paths: HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled = 1 (DWORD) + reboot

Microsoft docs: https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation
```

---

## The two verified fixes

### Option 1 — Move the solution to a shorter path (no system changes required)

Clone or move your repository to a path with a short root, for example:

```
C:\src\MyApp\
C:\dev\MyApp\
C:\com\github\yourname\yourrepo\
```

A total solution root of under ~80 characters leaves plenty of headroom for the deepest temporary staging paths.

### Option 2 — Enable Windows long path support (requires IT / GPO approval)

Set the following registry key and reboot:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem
Value: LongPathsEnabled  Type: DWORD  Data: 1
```

Or via Group Policy: `Local Computer Policy → Computer Configuration → Administrative Templates → System → Filesystem → Enable Win32 long paths`

> **Note:** This is a well-documented, low-risk Windows setting (supported since Windows 10 version 1607). In enterprise environments it typically requires a change-management request.

---

## Configuration

Both guards are controlled via MSBuild properties you can override in your `.csproj` or `Directory.Build.props`:

| Property | Default | Description |
|---|---|---|
| `LongPathGuardEnabled` | `true` | Set to `false` to disable the guard entirely |
| `MaxSafePathLength` | `200` | Files with a full path longer than this (in characters) are flagged. Lower this if your build agent uses especially deep temp folders. |

### Disable for a specific project

```xml
<PropertyGroup>
  <LongPathGuardEnabled>false</LongPathGuardEnabled>
</PropertyGroup>
```

### Lower the threshold for a deep agent path

```xml
<PropertyGroup>
  <MaxSafePathLength>180</MaxSafePathLength>
</PropertyGroup>
```

---

## How it works

The package ships a single MSBuild `.targets` file (no runtime DLL). When imported it adds a `LongPathGuard` target that runs `BeforeTargets="Build;Publish;MauiPrepareForBuild;Restore"` for any `ios` or `maccatalyst` target framework:

1. Collects all files under `$(MSBuildProjectDirectory)` and `$(NuGetPackageRoot)`.
2. Filters to any file whose `%(FullPath)` exceeds `$(MaxSafePathLength)` characters.
3. If any offending files are found, emits an MSBuild `Error` with the full list and the two fix options.
4. If no offending files are found, the target completes silently and the build continues normally.

The check is **vendor-neutral** — it does not single out any specific package. Any NuGet package with long filenames will be caught.

---

## Contributing

Issues and pull requests are welcome at [github.com/grootstebozewolf/LongPathGuard.Maui](https://github.com/grootstebozewolf/LongPathGuard.Maui).

If you hit this with a specific package and version, feel free to open an issue with the package name and the offending filename — it helps document the real-world cases that triggered the guard.

---

## License

[MIT](LICENSE.txt) — © Jeroen / JeroenTechSolutions
