# Web POS — E2E Verification Checklist

Run on a Windows canteen terminal PC with iBonus reader hardware.

## Prerequisites

1. **ETCS POS Bridge** installed as Windows Service (`ETCSPosBridge`, auto-start)
   - Install: see [`ETCS.Pos.Bridge/Installer/README.md`](../ETCS.Pos.Bridge/Installer/README.md)
   - Verify: `http://127.0.0.1:5050/health` → `{ "status": "ok" }`
2. ETCS.API with `Pos:ApiKey` and ibonus connection string
3. ETCS.Pos.Web with `PosWeb:ApiKey` and `PosWeb:ApiBaseUrl` in **server** `appsettings.json` only (never exposed to the browser); `PosWeb:BridgeBaseUrl` (`http://127.0.0.1:5050`)
4. Legacy ibonus stored procedures present (see `ETCS.API/Database/PosStoredProcedures/README.md`)
5. **Bridge setup file** available for download from Pos.Web (see [Bridge setup download](#bridge-setup-download) below)

## Install bridge

**Option A — Setup.exe (kiosk):** Run `ETCS.Pos.Bridge.Setup.exe` (build with `ETCS.Pos.Bridge\Installer\Build-PosBridgeSetup.ps1`).

**Option A2 — Download from Pos.Web:** When the bridge is offline, the POS screen shows **Download Bridge setup**. Requires the setup file on the Pos.Web server (see [Bridge setup download](#bridge-setup-download)).

**Option B — PowerShell:**

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Install-PosBridge.ps1 -Build
```

Or from a deployment zip (exe + scripts in same folder):

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-PosBridge.ps1 -SourcePath .
```

Confirm:

```powershell
Get-Service ETCSPosBridge          # Status: Running, StartType: Automatic
Invoke-RestMethod http://127.0.0.1:5050/health
```

Uninstall:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PosBridge.ps1
```

## Bridge setup download

Pos.Web can serve `ETCS.Pos.Bridge.Setup.exe` when the bridge offline overlay is shown. Configure **one** of:

| Method | Configuration |
|--------|----------------|
| **Server path** | `PosWeb:BridgeSetupPath` in `appsettings.json` (absolute or relative to content root) |
| **wwwroot fallback** | Copy `ETCS.Pos.Bridge.Setup.exe` to `ETCS.Pos.Web/wwwroot/downloads/` |

Build the installer:

```powershell
cd ETCS.Pos.Bridge\Installer
powershell -ExecutionPolicy Bypass -File .\Build-PosBridgeSetup.ps1
```

Development default (`appsettings.Development.json`) points to `..\ETCS.Pos.Bridge\Installer\POSBridge\ETCS.Pos.Bridge.Setup.exe` after a build.

Download URL: `GET /Pos/DownloadBridgeSetup` (linked from the offline overlay as **Download Bridge setup**).

Do not commit the `.exe` to git.

## Button parity matrix (vs old WinForms POS)

| Button | Expected outcome |
|--------|------------------|
| **Cashless** | Optional pre-print → ping reader → iBonus SOAP **5002** → `spGetSpendLimitInfo` pass → `spInsertWindposPurchase` per line (`skucode` = `ItemMaster.ItemCode`, discounted line amounts) → tax receipt → cart cleared (undo state kept) |
| **Cashless limit fail** | After 5002: `spDeleteAccesslogBylimit` + alert (daily/weekly message) |
| **Undo Cashless** | Cart or last txn required → confirm → iBonus **5003** → undo receipt (`POST /print/undo-receipt`) → no MealDB undo |
| **Cash** | `spInsertCashPurcahse` with `branchCode` + numeric `terminalcode` → receipt → cart cleared |
| **Undo Cash** | `spUndoCashPurhcase` with undo amount field |
| **Credit/Debit Card** | `spInsertCreditCardPurcahse` with card number |
| **Reset** | Clears cart, discount, cash fields, undo state |
| **Remove Selected** | Removes highlighted line, recalculates payable |
| **Apply Discount** | One-time discount %; per-line amounts reduced for cashless PostPurchase |

## Manual test steps

1. Open `https://localhost:7210/Pos` (or deployed host) in kiosk browser
2. Select branch and terminal; confirm reader IP populates
3. Add products — **Code** column shows `ItemCode`, not MealItem Id
4. Apply discount; confirm payable = sum of discounted lines
5. **Cashless**: tap card → verify ibonus access log + wind POS purchase rows with ItemCode SKUs
6. **Undo cashless**: undo without items in cart (uses last txn) → undo receipt prints
7. **Cash** / **Card** / **Undo cash**: verify legacy SP rows in ibonus
8. **Reset** / **Remove** / **Discount**: match old cart behaviour
9. Reboot kiosk → confirm `ETCSPosBridge` auto-starts and Pos.Web shows bridge connected
10. In browser DevTools → Network: checkout calls go to `/Pos/Api/*` on the Pos.Web host only (no direct `ETCS.API` URL, no `X-API-KEY` in requests)
11. In console, `window.posConfig` must not contain `apiKey` or `apiBaseUrl`

## Automated smoke (no hardware)

```powershell
# Service should already be running after install:
Invoke-RestMethod http://127.0.0.1:5050/health

# Dev console mode (stops Windows Service first if port conflict):
Stop-Service ETCSPosBridge -ErrorAction SilentlyContinue
cd ETCS.Pos.Bridge\bin\Debug\net48
.\ETCS.Pos.Bridge.exe --console
```

API smoke (server-side key in `ETCS.Pos.Web/appsettings.json`; browser uses BFF):

```powershell
# Direct API (ops / server config validation only):
$headers = @{ 'X-API-KEY' = 'your-pos-api-key' }
Invoke-RestMethod -Uri 'https://localhost:7204/api/pos/schools' -Headers $headers

# BFF proxy (same path the POS browser uses after login to Pos.Web):
Invoke-RestMethod -Uri 'https://localhost:7210/Pos/Api/Students/CASH/SpendInfo' -SkipCertificateCheck
```
