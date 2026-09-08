# memoNOW

**memoNOW** is a tiny Windows scratchpad for things you need to keep in your head only for the next few minutes or hours.

It is intentionally *not* a knowledge base, archive, calendar, or project manager. Add a short memo, keep it visible, and delete it as soon as it is done.

## Prototype scope

The first prototype deliberately stays small:

- Quick one-line memo input
- User-configurable global hotkey (default: `Win + Shift + Space`)
- Up to 10 active memos
- One-click completion (completed memos are deleted immediately)
- Tray-resident operation
- `Esc` hides the window
- Closing/minimizing hides to the tray instead of quitting
- Unfinished memos survive app/PC restarts
- Local-only storage; no account, sync, tags, folders, due dates, or history

## Global shortcut settings

The default shortcut is `Win + Shift + Space`. If Windows or another application already uses it, click **ショートカット設定** at the bottom of memoNOW, then click the capture field and press the new key combination you want to use.

The shortcut can also be changed from the tray menu via **Shortcut settings...**.

memoNOW tests the new shortcut before accepting it. If Windows reports that the combination is already in use, memoNOW keeps the previous shortcut instead. The chosen shortcut is saved locally and restored on the next launch.

For safety, a shortcut must include at least one of `Ctrl`, `Alt`, `Shift`, or `Win`.

## Recommended distribution: GitHub Release

For normal use, the target Windows PC does **not** need the .NET SDK or .NET Runtime installed.

Every push to `main` is built by GitHub Actions. If the build succeeds, the `latest` GitHub Release is automatically updated and the newest self-contained Windows x64 executable is attached as `memoNOW.exe`.

- Release page: https://github.com/cityriver52/memoNOW/releases/tag/latest
- Direct latest EXE: https://github.com/cityriver52/memoNOW/releases/download/latest/memoNOW.exe

On the target PC:

1. Open the `memoNOW latest` Release.
2. Under **Assets**, click `memoNOW.exe`.
3. Put the downloaded EXE in any writable folder and run it.

No installer, package manager, administrator rights, or .NET installation is required by memoNOW itself.

The Actions artifact is retained as a CI/debugging output, but normal users should use the Release download above.

> Note: organization security policy, Windows Defender, SmartScreen, AppLocker, WDAC, or other endpoint-management rules can still block an unsigned executable. memoNOW does not attempt to bypass those controls.

## Requirements

### To run the portable build

- Windows 10 or Windows 11, x64
- No .NET installation required

### To build from source

- .NET 8 SDK

## Run from source

```powershell
dotnet run --project .\src\memoNOW\memoNOW.csproj
```

## Build from source

```powershell
dotnet build .\src\memoNOW\memoNOW.csproj -c Release
```

## Create the portable EXE locally

```powershell
.\scripts\publish-portable.ps1
```

The result is written to:

```text
artifacts\memoNOW-win-x64\memoNOW.exe
```

The portable publish is self-contained and single-file. Trimming is intentionally disabled because memoNOW uses WPF/WinForms desktop APIs and reliability is more important than minimizing the executable size.

## Data location

Active memos are stored locally at:

```text
%LOCALAPPDATA%\memoNOW\memos.json
```

Shortcut settings are stored locally at:

```text
%LOCALAPPDATA%\memoNOW\settings.json
```

There is intentionally no completed-item history. If a memo matters long-term, it belongs somewhere else.

## Prototype controls

| Action | Control |
| --- | --- |
| Open/focus memoNOW | Configurable global shortcut (default `Win + Shift + Space`) |
| Change global shortcut | Bottom **ショートカット設定** button / tray menu |
| Add memo | Type and press `Enter` |
| Complete memo | Click `Done` |
| Hide window | `Esc`, minimize, or close |
| Re-open from tray | Double-click tray icon / tray menu |
| Quit completely | Tray icon → `Exit` |

## Design rule

> memoNOW is RAM, not storage.

Features that encourage accumulating information should be treated with suspicion. The prototype optimizes for capture speed and disposal speed first.
