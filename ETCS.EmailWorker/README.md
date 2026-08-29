# ETCS Email Worker — Deploy & Install
Email sending runs in **ETCS.EmailWorker** (Windows Service)

## Install / start Windows Service

Run **elevated** PowerShell / CMD on the server:

```powershell
sc.exe create "ETCS Email Worker" binPath= "C:\inetpub\wwwroot\Email_Background_Worker\ETCS.EmailWorker.exe" start= auto
sc.exe start "ETCS Email Worker"
```

## Update an existing install

```powershell
sc.exe stop "ETCS Email Worker"
dotnet publish ETCS.EmailWorker -c Release -o C:\Services\ETCS.EmailWorker
sc.exe start "ETCS Email Worker"
```

## Verify

1. Service is **Running**.
2. Logs show: `Email delivery worker started`.
3. New `EmailNotification` rows move from `Queued` → `Sent` / `Failed` within about one poll interval (default 30s).

## Local development (no service)

```powershell
dotnet run --project ETCS.EmailWorker
```
