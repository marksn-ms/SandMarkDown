# Microsoft Store listing draft

Paste these into Partner Center for the SandMarkDown submission.

## Product identity

Kept in `packaging/StoreIdentity.json`, which `deploy/Build-Msix.ps1 -Store` reads.

- Store ID: 9N30KVRMNNJP
- Package family name: SandmarkTechnologiesLLC.SandMarkDown_k6qbe8szwam6r
- Store page, once published: https://apps.microsoft.com/detail/9N30KVRMNNJP
- Store app link: `ms-windows-store://pdp/?productid=9N30KVRMNNJP`

## Upload package

```powershell
.\deploy\Build-Msix.ps1 -Store
```

Upload `artifacts\msix\SandMarkDown_<version>_x64.msix`. Raise `<Version>` in `src/SandMarkDown/SandMarkDown.csproj` before each new submission.

## Product name

SandMarkDown

## Short description

A fast, private Markdown reader. Open a file, read it, close it.

## Description

SandMarkDown is a Markdown reader for Windows from Sandmark Technologies. It opens a `.md` file, renders it cleanly, and gets out of the way. It does not edit.

- Reads headings, tables, task lists, footnotes, and code blocks, with colored code for common languages.
- Keeps the headings beside the page so you can jump around a long file.
- Finds text on the page with Ctrl+F.
- Updates the page when you save the file in another program, and keeps your place.
- Follows links between Markdown files, with Back and Forward.
- Prints dark text on white.
- Remembers your reading size and window position.

Private by design. There is no account, no analytics, and no telemetry. Your files stay on your PC. A Markdown file cannot run script. Images from the web stay hidden until you choose to show them.

## Features (up to 20, one per line)

- Clean dark reading view
- Heading list for long files
- Find on page
- Live reload when the file changes
- Colored code blocks
- Links between Markdown files, with Back and Forward
- Print
- No account, no telemetry
- Web images hidden until you allow them

## Search terms (up to 7)

markdown, md viewer, markdown reader, readme, notes, documentation, offline

## Category

Productivity

## URLs

- Privacy policy: https://sandmarktech.com/privacy.html#sandmarkdown
- Website: https://sandmarktech.com
- Support: https://sandmarktech.com/supportal/submit?slug=sandmarkdown

## Copyright

© 2026 Sandmark Technologies

## Store logo

`packaging/store-logo-300.png` (300 × 300).

## Screenshots still needed

At least one, 1366 × 768 or larger, of the real window. Suggested set:

1. A long README with the heading list showing.
2. Find on page with matches highlighted.
3. A code-heavy file with colored fences.
4. The About box.

## Notes for certification

SandMarkDown is a full-trust desktop app (WPF on .NET 10, self-contained) and needs the `runFullTrust` capability to read Markdown files the user opens from File Explorer and to show them in Microsoft Edge WebView2. It makes no network requests on its own.
