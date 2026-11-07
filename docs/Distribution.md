# Windows distribution

SandMarkDown is published as one Windows x64 application from this repository. GitHub Releases hosts the files. [Velopack](https://docs.velopack.io/) builds the installer and the update packages. The package includes the .NET runtime, so the reader does not ask the user to install .NET first.

The Microsoft Store package in `deploy/Build-Msix.ps1` is a separate path. This document is the GitHub distribution.

Authenticode signing is not part of this setup. No signing certificate and no extra secret are required. Signing can be added later with `vpk pack --signParams` or `--azureTrustedSignFile`. Do not commit certificates or passwords.

The product version is `<Version>` in `src/SandMarkDown/SandMarkDown.csproj`. `distribution/catalog.json` names the product, the repository, and the file names. It does not store a version.

## Build a package locally

Install the .NET 10 SDK named in `global.json`. No secrets are required.

```powershell
./distribution/pack.ps1 -Help
./distribution/pack.ps1
```

The script publishes `win-x64` self-contained, then runs the pinned `vpk` tool from `dotnet-tools.json`. Velopack 1.2.161 is also the `Velopack` package version. Those two versions stay together.

Results:

- `artifacts/dist/` is the Velopack output. It can also contain an older full package when a delta was built.
- `artifacts/upload/` is the set that GitHub Releases receives, including `SHA256SUMS.txt`.

To try a delta locally, pack once, copy the `*-full.nupkg` aside, then pack again with a higher `-Version` and `-BasePackages` pointing at that copy. `-Version` is only for a local experiment. GitHub Actions uses the project version.

`x64` is the only runtime. The application is WPF, so it is a Windows program. arm64 is not built.

A portable zip is included because Velopack already produces it and it is useful when an installer is inconvenient. Unzip it and run `SandMarkDown.exe`. That copy can still update. There is no second installer format. The Store MSIX stays on its own path.

## Change the version and publish

1. Set a higher `MAJOR.MINOR.PATCH` in `<Version>` in `src/SandMarkDown/SandMarkDown.csproj`. Do not add another version file.
2. Open a pull request. CI builds and tests. It does not publish.
3. Merge to `main`.

The release workflow runs only when that project file changes on `main`. It compares `<Version>` with the parent commit:

- An unchanged version publishes nothing. Merging this distribution work does not publish, because the version stays `1.0.0`.
- A missing parent version publishes nothing.
- A higher version publishes `vX.Y.Z` when that tag does not already exist.
- A lower version fails the workflow and publishes nothing.
- A tag that already exists is left alone. The workflow fails rather than replacing it.

If the workflow fails before it creates the tag, fix the failure and run the Release workflow by hand from the Actions tab, on `main`. A manual run publishes the current version when its tag does not exist. It does not replace an existing tag.

`distribution/ReleasePlan` is the program that makes this decision. `dotnet run --project distribution/ReleasePlan -- --help` explains it. It needs no secrets.

Failed tests, a failed package build, or a failed provenance step stop the job before `gh release create`.

## Files on a release

The channel is Velopack's default `win` channel. Velopack 1.2.161 puts that channel in the installer and portable names, and leaves it out of the full package name. The feed file is `releases.win.json`.

| File | Purpose |
| --- | --- |
| `SandMarkDown-win-Setup.exe` | Per-user installer. It does not need administrator rights. |
| `SandMarkDown-win-Portable.zip` | Unzip and run. Velopack can still update this copy. |
| `SandMarkDown-<version>-full.nupkg` | Full update package. |
| `SandMarkDown-<version>-delta.nupkg` | Smaller update, present when the previous full package was available. |
| `releases.win.json` | Velopack feed for the `win` channel. |
| `SHA256SUMS.txt` | SHA-256 of the other files. The checksum file is not listed inside itself. |

The feed on each release lists only the packages attached to that release. The installed application reads `releases.win.json` from recent stable releases and combines them. GitHub's latest release is the newest `vX.Y.Z` tag, which is the right release for this one-product repository.

The installer puts the program in `%LocalAppData%\SandMarkDown`. Shortcuts point at a stable launcher in that folder, so an update can replace `current` without moving the shortcut.

## Checksums

`SHA256SUMS.txt` uses lowercase hashes and two spaces before the file name.

```powershell
Get-Content SHA256SUMS.txt | ForEach-Object {
    if ($_ -match '^([0-9a-f]{64})  (.+)$') {
        $actual = (Get-FileHash $Matches[2] -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Matches[1]) { throw "$($Matches[2]) does not match" }
    }
}
```

Run that command in the folder that contains the downloaded files.

## Provenance

This repository is public, so the release job records a build provenance attestation with `actions/attest-build-provenance` before it creates the GitHub Release. Verify a downloaded file with:

```powershell
gh attestation verify .\SandMarkDown-win-Setup.exe --repo marksn-ms/SandMarkDown
```

If the repository were private, GitHub Artifact Attestations would need GitHub Enterprise Cloud. The workflow skips that step in that case, and the release notes say whether provenance was recorded.

## Updates

The application calls `VelopackApp.Build().Run()` before WPF starts, and it does not apply a downloaded update on its own. After install, a launch with no file path checks GitHub. Help, Check for updates does the same check and reports the result. When a newer version exists, the user can download it and restart. Nothing is installed without that confirmation.

Opening a file from the command line, or by double-click, skips the startup prompt so reading is not interrupted. The Help command still works.

A `dotnet run` build is not an installed copy, so the startup check stays quiet and the Help command explains that.

`SANDMARKDOWN_UPDATE_PATH` can point at a local Velopack output directory for a side-by-side experiment. Unset it for normal use. The check contacts GitHub only for an installed copy, and only to see whether a newer version exists. It does not send a reading history or a file name.

## Installation, upgrade, and uninstall

Setup installs for the current user under `%LocalAppData%\SandMarkDown`. It does not need administrator rights.

Reading settings, recent files, the WebView2 profile, and the error log stay in `%LocalAppData%\Sandmark Technologies\SandMarkDown`. That folder is not inside the install folder. An upgrade replaces the program and leaves those files in place. Uninstall deletes the install folder, the shortcuts, and the Apps list entry. It does not delete the settings folder.

The first run after install registers SandMarkDown for Open with. It does not change the user's default app. Windows still asks the user to confirm the default. Help, Set as Default for Markdown does the same registration and opens Settings. Uninstall removes that registration when it still points at the copy being removed. It does not clear a default the user chose for a different program.

`deploy/Install-SandMarkDown.ps1` is the development copy. It publishes into `%LocalAppData%\Sandmark Technologies\SandMarkDown\Current`. That is not the Velopack install. Use Setup for a copy that should update from GitHub.

The WebView2 Runtime is not inside the package. Current Windows 11 installs already have it. If it is missing, the reader shows the same message it shows for a local build.

Windows SmartScreen may warn that the publisher is unknown, because the installer is not signed. The user can choose to run it.

## Sandmark distribution standard

SensorAnalysis.net publishes three programs from one repository, so each product has its own tag prefix and a client must not use `/releases/latest`. SandMarkDown is one program in its own repository. The website should use the standard latest-release endpoint.

Read identity from the default branch:

`https://raw.githubusercontent.com/marksn-ms/SandMarkDown/main/distribution/catalog.json`

Read the current release from:

`https://api.github.com/repos/marksn-ms/SandMarkDown/releases/latest`

| Catalog or release field | SandMarkDown |
| --- | --- |
| Product name | `SandMarkDown` |
| Description | `A local Markdown reader.` |
| GitHub repository | `marksn-ms/SandMarkDown` |
| Latest stable release | `GET /repos/marksn-ms/SandMarkDown/releases/latest` |
| Tag | `vX.Y.Z` |
| Release date | `published_at` on that release |
| Release notes | `body` on that release |
| Installer download | `https://github.com/marksn-ms/SandMarkDown/releases/latest/download/SandMarkDown-win-Setup.exe` |
| Checksums | `SHA256SUMS.txt` on the same release |
| Provenance | `gh attestation verify <file> --repo marksn-ms/SandMarkDown` |
| Supportal product slug | `sandmarkdown` |

The planned product page is `sandmarkdown.html` on the Sandmark Technologies site. It can link to the installer URL above and to Supportal with `slug=sandmarkdown`. This repository does not modify that site.

The latest release is enough because this repository publishes only SandMarkDown, only stable `vX.Y.Z` tags, and never replaces an existing tag. Drafts and prereleases are not created by the workflow.
