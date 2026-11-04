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

## Read a file

File, Open loads a Markdown file. File, Close returns to the welcome page. File, Exit quits.

You can also drop a `.md` or `.markdown` file on the window, or pass a path on the command line:

```powershell
SandMarkDown.exe "C:\notes\readme.md"
```

The status bar shows the open file path. Ctrl+F finds text on the page. F3 moves to the next match. Alt+Left and Alt+Right move through files opened in this window, including a file you closed. Ctrl+mouse wheel, Ctrl+Plus, and Ctrl+Minus change the reading size. Ctrl+0 returns to actual size. The same commands are under View. The reading size is remembered. Links to other Markdown files open in this window. Web links open in the browser.

When a file has more than one heading, those headings stay beside the page so you can jump.

Double-click a `.md` file to open it when SandMarkDown is the default app for Markdown files. 
