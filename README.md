# LM Studio Trigger for Boray POWer

A small Windows tray application that reports **LM Studio requests as user activity** to [Boray POWer](http://www.boray.se/software/boraypower/).

Boray POWer switches between two Windows power plans: **High Performance** as soon as the mouse or keyboard is used, and **Power Saver** after a configurable idle time. On a PC that runs 24/7 as an LLM server, this is not enough. Remote requests to LM Studio (from LAN, VPN or, for example, PocketPal) produce no mouse or keyboard input, so the machine stays in power-saving mode.

**LM Studio Trigger** closes this gap. Boray POWer itself is **not modified**.

---

## Contents

- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Building](#building)
- [Installation and autostart](#installation-and-autostart)
- [Usage](#usage)
- [Configuration](#configuration)
- [Timing](#timing)
- [Testing](#testing)
- [Known limitations](#known-limitations)
- [Troubleshooting](#troubleshooting)
- [Background: analysis of Boray POWer](#background-analysis-of-boray-power)
- [License](#license)

---

## How it works

1. The program checks all TCP connections on the LM Studio port (default: **1234**) every **2 seconds**.
2. It counts only connections in the **`Established`** state. It does not matter where they come from (localhost, LAN, VPN, mobile app).
3. As long as at least one connection exists, the program sends a **0-pixel mouse movement** via `SendInput`, immediately and then every **10 seconds**. The mouse pointer does not move.
4. Boray POWer detects inactivity with the Windows function `GetLastInputInfo`. The simulated input resets this timer, so Boray POWer switches to, or stays on, **High Performance**.
5. When the request ends, the program stops sending input. Boray POWer then waits for its usual idle time and switches back to **Power Saver**.

```
LM Studio client ──► Port 1234 (Established)
                          │
                LMStudioTrigger detects connection
                          │
                SendInput (0-pixel mouse movement)
                          │
                GetLastInputInfo timer is reset
                          │
          Boray POWer ──► High Performance
```

---

## Requirements

| Component | Note |
|---|---|
| Windows 10 | Tested on Windows 10 |
| Boray POWer V1.6.2 | Must be running and configured (Set high / Set low) |
| LM Studio | Local server enabled, default port 1234 |
| .NET Framework 4.x | Already included in Windows 10. Provides the compiler `csc.exe`. |

**No administrator rights** are required.

---

## Building

The source code is written in C# 5 and can be compiled with the compiler that ships with Windows. Nothing needs to be installed.

1. Put `LMStudioTrigger.cs` into a folder.
2. Open PowerShell in that folder and run:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:LMStudioTrigger.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LMStudioTrigger.cs
```

The compiler's notice that it only supports up to C# 5 is normal and not an error. Errors appear as lines containing `error CS…`.

3. Check that the EXE was created:

```powershell
Get-Item .\LMStudioTrigger.exe | Select-Object Name, Length, LastWriteTime
```

---

## Installation and autostart

1. Copy `LMStudioTrigger.exe` to a permanent location, for example next to `BorayPOWer.exe`.
2. Press `Win + R`, type `shell:startup` and press Enter.
3. In the Startup folder that opens, create a **shortcut** to `LMStudioTrigger.exe`.

> **Important:** The program must run in the **same logged-in user session** as Boray POWer. `GetLastInputInfo` is session-specific. Run as a Windows service or in a different session, the simulated input has no effect on Boray POWer.

---

## Usage

After startup, a round **"AI" icon** appears in the notification area. It may also be hidden under the overflow arrow.

| Icon | Meaning |
|---|---|
| 🟢 Green | LM Studio is currently in use, at least one active connection |
| ⚪ Gray | No request |

The tooltip also shows the number of active connections.

**Right-click menu:**

- **Status** opens a window showing:
  - the monitored port
  - the current state (active / no request)
  - the number and addresses of active connections
  - the time of the last activity
  - the time of the last simulated input and its result (`1` = accepted by Windows)
  - an error message, if any
- **Beenden** (Exit) closes the program.

A **double-click** on the icon also opens the status window. Starting the program a second time is prevented.

> The menu entries and the status window are currently in German. The strings are in `LMStudioTrigger.cs` and can be translated there.

---

## Configuration

The settings are constants at the top of the `TriggerContext` class in `LMStudioTrigger.cs`. Recompile after changing them.

```csharp
const int LmStudioPort = 1234;      // LM Studio server port
const int CheckIntervalMs = 2000;   // Connection check interval in ms
const int NudgeIntervalSec = 10;    // Interval between simulated inputs in s
```

> `NudgeIntervalSec` **must be smaller** than the idle time configured in Boray POWer. Otherwise Boray POWer may switch back to power-saving mode during an ongoing request.

---

## Timing

Example with Boray POWer set to an **idle time of 2 minutes** and a **CPU load threshold of 20 %**:

| Time | Event |
|---|---|
| t = 0 s | Request to LM Studio, connection `Established` |
| ≤ 2 s | LM Studio Trigger detects the connection and sends an input. Boray POWer switches to High Performance. |
| every 10 s | Another input while the connection exists |
| End of request | No more inputs |
| + 2 min | Boray POWer checks the average CPU load |
| CPU load ≤ 20 % | Switch to Power Saver |
| CPU load > 20 % | Boray POWer waits for the idle time again |

According to the author, Boray POWer measures CPU load as an average over 30 seconds and only checks it before switching to power-saving mode.

---

## Testing

### Test 1: Does Boray POWer react to simulated input?

This script waits until the Power Saver plan is active, then sends a simulated input and shows the active plan for 10 seconds. For the test, temporarily set the idle time in Boray POWer to, for example, 30 seconds, and do not touch the mouse or keyboard after starting.

The GUID below is the Windows default **Power Saver** plan. Adjust it if you use other plans (`powercfg /list`).

```powershell
$low = 'a1841308-3541-4fab-bc81-f71556f20b4a'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class In {
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct INP { public uint type; public MI mi; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INP[] i, int size);
  public static uint Nudge() { var a = new INP[1]; a[0].type = 0; a[0].mi.flags = 0x0001; return SendInput(1, a, Marshal.SizeOf(typeof(INP))); }
}
"@
"Do NOT touch mouse or keyboard. Waiting for Power Saver plan..."
while (-not ((powercfg /getactivescheme) -match $low)) { Start-Sleep 2 }
"$(Get-Date -f HH:mm:ss) Power Saver active. SendInput follows in 5 s."
Start-Sleep 5
"SendInput result: " + [In]::Nudge()
1..10 | ForEach-Object { Start-Sleep 1; "$(Get-Date -f HH:mm:ss) " + (powercfg /getactivescheme) }
```

Expected result: `SendInput result: 1`, followed by **High Performance** being shown.

### Test 2: Real-world test with LM Studio

1. Start `LMStudioTrigger.exe`.
2. Wait until Boray POWer is in power-saving mode (Boray POWer's tray icon is dimmed).
3. Send a request to LM Studio from another device without touching the server PC.
4. The AI icon turns green and Boray POWer switches to High Performance.
5. After the request, the AI icon turns gray again. After Boray's idle time has elapsed, the plan switches back.

After testing, set the idle time in Boray POWer back to your usual value.

---

## Known limitations

- **Connection, not content:** The program only detects that a connection exists, not what is being requested. Any connection to port 1234 counts, including, for example, plain model list queries.
- **Permanently open connections:** If a client keeps the connection open after the request (keep-alive), the icon stays green and the PC stays on High Performance. In that case, check the status window to see which address holds the connection.
- **Screen saver and monitor:** The simulated input also resets the timers for the screen saver and monitor power-off. The monitor therefore does not turn off during a request.
- **Session-bound:** Works only in the logged-in user session, not as a service.
- **Lock screen / logged-out user:** Not tested.

---

## Troubleshooting

| Problem | Possible cause / solution |
|---|---|
| No tray icon | Look under the overflow arrow for hidden icons |
| "already running" message on start | The program is already active, check the notification area |
| Icon does not turn green on a request | Check the port (`LmStudioPort`). Is the LM Studio server running? |
| Icon is green, but Boray POWer does not switch | Does the program run in the same user session as Boray POWer? Check the result of the simulated input in the status window (`1` = ok, `0` = rejected by Windows) |
| Icon stays green after the request | A client keeps the connection open, see [Known limitations](#known-limitations) |
| Boray POWer switches back during the request | `NudgeIntervalSec` is not smaller than Boray's idle time |

You can also list the connections on port 1234 manually:

```powershell
Get-NetTCPConnection -LocalPort 1234 -ErrorAction SilentlyContinue |
  Where-Object State -ne 'Listen' |
  Select-Object RemoteAddress, RemotePort, State, OwningProcess
```

---

## Background: analysis of Boray POWer

The solution is based on an examination of the metadata and embedded strings of `BorayPOWer.exe` V1.6.2. The program was not decompiled or modified.

| Item | Finding |
|---|---|
| Language | VB.NET (Visual Studio 2008) |
| Framework | .NET Framework 2.0 |
| Inactivity detection | `GetLastInputInfo` from `user32.dll` |
| Plan switching | `powercfg -setactive <GUID>`, query via `powercfg -getactivescheme` |
| CPU load | Performance counter `% Processor Time`, averaged over 30 seconds |
| `Interop.IWshRuntimeLibrary.dll` | Only used for Boray POWer's autostart shortcut |

Because Boray POWer uses `GetLastInputInfo`, input generated with `SendInput` counts as real user activity. This means Boray POWer does not need to be modified.

---

## License

**LM Studio Trigger:** _License to be determined._

**Boray POWer** is **not** part of this repository and is not distributed here. Boray POWer © Anders Persson. According to the program's notice, it is "Boray Music Ware": if you use it, you are asked to buy at least one of the author's tunes. Download and information: <http://www.boray.se/software/boraypower/>

LM Studio is a product of its respective owners. This project is not affiliated with Boray Software or LM Studio.
