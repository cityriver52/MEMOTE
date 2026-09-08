# memoNOW

**memoNOW** is a tiny Windows scratchpad for things you need to keep in your head only for the next few minutes or hours.

It is intentionally *not* a knowledge base, archive, calendar, or project manager. Add a short memo, keep it visible, and delete it as soon as it is done.

## Prototype scope

The first prototype deliberately stays small:

- Quick one-line memo input
- Global hotkey: `Win + Shift + Space`
- Up to 10 active memos
- One-click completion (completed memos are deleted immediately)
- Tray-resident operation
- `Esc` hides the window
- Closing/minimizing hides to the tray instead of quitting
- Unfinished memos survive app/PC restarts
- Local-only storage; no account, sync, tags, folders, due dates, or history

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK to build from source

## Run from source

```powershell
dotnet run --project .\src\memoNOW\memoNOW.csproj
```

## Build

```powershell
dotnet build .\src\memoNOW\memoNOW.csproj -c Release
```

The executable is produced under `src\memoNOW\bin\Release\net8.0-windows\`.

## Data location

Active memos are stored locally at:

```text
%LOCALAPPDATA%\memoNOW\memos.json
```

There is intentionally no completed-item history. If a memo matters long-term, it belongs somewhere else.

## Prototype controls

| Action | Control |
| --- | --- |
| Open/focus memoNOW | `Win + Shift + Space` |
| Add memo | Type and press `Enter` |
| Complete memo | Click `Done` |
| Hide window | `Esc`, minimize, or close |
| Re-open from tray | Double-click tray icon / tray menu |
| Quit completely | Tray icon → `Exit` |

## Design rule

> memoNOW is RAM, not storage.

Features that encourage accumulating information should be treated with suspicion. The prototype optimizes for capture speed and disposal speed first.
