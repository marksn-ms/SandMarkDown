# SandMarkDown

SandMarkDown is a local Markdown reader for Windows. It opens a `.md` file, renders it for reading, and closes. It does not edit.

Style: sandmark-app. Theme: dark. Brand colors and the logo match the Sandmark Technologies pack in `docs/tokens.css`.

## Build

```powershell
dotnet test
dotnet build src/SandMarkDown/SandMarkDown.csproj -c Release
```

The reader is `src/SandMarkDown/bin/Release/net10.0-windows/SandMarkDown.exe`.

It needs the .NET 10 desktop runtime and the Microsoft Edge WebView2 Runtime. Both are already present on a current Windows 11 development machine.

## Install for double-click

From the repo:

```powershell
.\deploy\Install-SandMarkDown.cmd
```

That publishes the current Release build to `%LOCALAPPDATA%\Sandmark Technologies\SandMarkDown\Current` and associates `.md` and `.markdown` with it for the current user. No admin rights. Run it again after you change the reader. `.\deploy\Install-SandMarkDown.cmd -Uninstall` removes that copy.

## Microsoft Store package

```powershell
.\deploy\Build-Msix.ps1 -LocalCheck
```

That publishes a self-contained build and packs `artifacts\msix\SandMarkDown_<version>_x64-localcheck.msix` with placeholder identity, to validate the manifest and layout. For the Store upload, pass the three identity values from Partner Center, Product identity, with `-IdentityName`, `-Publisher`, and `-PublisherDisplayName`. The Store signs the package after certification. Listing text is drafted in `docs/store-listing.md`.

## Read a file

File, Open loads a Markdown file. File, Close returns to the welcome page. File, Exit quits.

You can also drop a `.md` or `.markdown` file on the window, or pass a path on the command line:

```powershell
SandMarkDown.exe "C:\notes\readme.md"
```

The status bar shows the open file path. Ctrl+F finds text on the page. F3 moves to the next match. Alt+Left and Alt+Right move through files opened in this window, including a file you closed. Ctrl+mouse wheel, Ctrl+Plus, and Ctrl+Minus change the reading size. Ctrl+0 returns to actual size. The same commands are under View. The reading size is remembered. Links to other Markdown files open in this window. Web links open in the browser.

When a file has more than one heading, those headings stay beside the page so you can jump.

When you save the open file in another program, the page updates and keeps its place. File, Print (Ctrl+P) prints in dark text on white. The window opens where you left it.

Double-click a `.md` file to open it when SandMarkDown is the default app for Markdown files.

## Privacy and safety

SandMarkDown has no accounts and sends no telemetry. A Markdown file cannot run script. Images from the web stay hidden until you choose Show images for that file. Links open only when you click them, and a link to a program shows it in File Explorer instead of running it.

Unexpected errors are written to `%LOCALAPPDATA%\Sandmark Technologies\SandMarkDown\logs\error.log` on this PC. Nothing is sent anywhere.

## License

SandMarkDown is released under the [MIT License](LICENSE). Libraries it uses are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
