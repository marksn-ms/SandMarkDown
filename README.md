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

## Read a file

File, Open loads a Markdown file. File, Close returns to the welcome page. File, Exit quits.

You can also drop a `.md` or `.markdown` file on the window, or pass a path on the command line:

```powershell
SandMarkDown.exe "C:\notes\readme.md"
```

The status bar shows the open file path. Ctrl+mouse wheel, Ctrl+Plus, and Ctrl+Minus change the reading size. Ctrl+0 returns to actual size. The same commands are under View. Links to other Markdown files open in this window. Web links open in the browser.

Double-click a `.md` file to open it when SandMarkDown is the default app for Markdown files.
"# SandMarkDown" 
