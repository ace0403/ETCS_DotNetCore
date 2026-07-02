# ETCS POS Bridge — Windows Service Installer

Install the bridge as a Windows Service (`ETCSPosBridge`) with **automatic start** on boot.

Two options:

| Method | Best for | Deliverable |
|--------|----------|-------------|
| **Setup.exe** | Kiosk operators (double-click) | `ETCS.Pos.Bridge.Setup.exe` |
| **PowerShell** | IT / dev / scripted deploy | `Install-PosBridge.ps1` |

Both install to `C:\Program Files\ETCS\POSBridge`, register the same service, set auto-start, and start the bridge on `http://127.0.0.1:5050`.

## Requirements

- Windows 10/11 (64-bit)
- .NET Framework 4.8 or later
- Administrator rights (UAC prompt)

---

## Option A — Setup.exe (recommended for kiosks)

### Build the installer (dev machine)

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) on your build PC
2. Run:

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Build-PosBridgeSetup.ps1
```

Output: a **`POSBridge`** folder ready to copy to kiosks:

```
ETCS.Pos.Bridge\Installer\POSBridge\
  ETCS.Pos.Bridge.Setup.exe    <- run this
  ETCS.Pos.Bridge.exe
  Newtonsoft.Json.dll
  ...
```

### Install on a kiosk

1. Copy the entire **`POSBridge`** folder to the canteen PC (e.g. `D:\POSBridge` or USB)
2. Open the folder and double-click **`ETCS.Pos.Bridge.Setup.exe`**
3. Approve UAC → Next → Install
4. Verify: `Invoke-RestMethod http://127.0.0.1:5050/health`

The setup embeds the binaries from the same folder at build time. Keeping `Setup.exe` beside the `.exe` and DLLs lets IT re-copy or audit files without rebuilding.

Uninstall via **Settings → Apps** or **Add/Remove Programs** (“ETCS POS Bridge”).

---

## Option B — PowerShell scripts

### Quick install (development)

From the repo, build and install in one step:

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Install-PosBridge.ps1 -Build
```

## Quick install (kiosk deployment — POSBridge folder)

After `Build-PosBridgeSetup.ps1`, use the generated **`POSBridge`** folder, or package:

```
POSBridge/
  ETCS.Pos.Bridge.exe
  Newtonsoft.Json.dll
  ... (all Release binaries)
  Install-PosBridge.ps1      (optional — copy from Installer/)
  Uninstall-PosBridge.ps1    (optional)
```

On the kiosk (PowerShell instead of Setup.exe):

```powershell
cd D:\POSBridge
powershell -ExecutionPolicy Bypass -File ..\Install-PosBridge.ps1 -SourcePath .
```

Or from `Installer` when `POSBridge` subfolder exists:

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Install-PosBridge.ps1
```

The script will:

- Copy files to `C:\Program Files\ETCS\POSBridge`
- Register the `ETCSPosBridge` Windows Service
- Set start type to **Automatic**
- Configure restart-on-failure
- Start the service and verify `http://127.0.0.1:5050/health`

## Script parameters

### Install-PosBridge.ps1

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-SourcePath` | `..\bin\Release\net48` | Folder containing `ETCS.Pos.Bridge.exe` |
| `-InstallDir` | `C:\Program Files\ETCS\POSBridge` | Installation target |
| `-Build` | off | Run `dotnet build -c Release` before install |

Examples:

```powershell
# Install from Release output (already built)
.\Install-PosBridge.ps1

# Install from a deploy zip folder (exe in same directory as script)
.\Install-PosBridge.ps1 -SourcePath .

# Custom install location
.\Install-PosBridge.ps1 -SourcePath . -InstallDir 'D:\ETCS\POSBridge'
```

### Uninstall-PosBridge.ps1

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PosBridge.ps1
```

Stops the service, unregisters it, and removes the install folder.

## Verify

In **Services** (`services.msc`), look for display name **ETCS POS Bridge** (internal name `ETCSPosBridge`).

```powershell
Get-Service ETCSPosBridge
Invoke-RestMethod http://127.0.0.1:5050/health
```

Expected: service **Running**, start type **Automatic**, health `{ "status": "ok" }`.

If the service is missing, you likely ran the bridge in **console mode** (`--console`) or the installer did not run **as Administrator**. Re-run:

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Install-PosBridge.ps1 -Build
```

Approve the UAC prompt and wait for `Service 'ETCSPosBridge' installed and running`.

## Pos.Web configuration

Set in `ETCS.Pos.Web` appsettings:

```json
"PosWeb": {
  "BridgeBaseUrl": "http://127.0.0.1:5050"
}
```

## Troubleshooting

| Issue | Action |
|-------|--------|
| Execution policy blocks script | Use `-ExecutionPolicy Bypass` as shown above |
| `.NET Framework 4.8` error | Install from https://dotnet.microsoft.com/download/dotnet-framework/net48 |
| Health check fails | Check Event Viewer → Windows Logs → Application for `ETCSPosBridge` |
| Port 5050 in use | Stop other bridge/console instance: `Stop-Service ETCSPosBridge` |

## Which should I use?

- **Kiosk / canteen staff** → copy **`POSBridge`** folder, run `ETCS.Pos.Bridge.Setup.exe` inside it
- **CI/CD or IT scripts** → `Install-PosBridge.ps1 -Build` or `-SourcePath .\POSBridge`
- **No Inno on build machine** → distribute `POSBridge` folder + `Install-PosBridge.ps1` only

Source for Setup.exe: [`ETCS.Pos.Bridge.Setup.iss`](ETCS.Pos.Bridge.Setup.iss) — compiled by [`Build-PosBridgeSetup.ps1`](Build-PosBridgeSetup.ps1).
