# GDG Virtual Printer RedMon Bridge v0.2

Questa soluzione implementa una **Print Support Virtual Printer** moderna Windows 11
con input **OXPS** e un bridge full-trust compatibile, a livello di variabili
d'ambiente, con un'applicazione che prima veniva avviata da RedMon.

## Requisiti di compilazione

- Windows 11 24H2 o successivo (build 26100+)
- Visual Studio 2022/2026 con workload .NET Desktop Development
- Windows 11 SDK 10.0.26100.x
- .NET 8 SDK
- MSIX Packaging Tools / Windows Application Packaging Project

Non esiste alcun riferimento a `Windows.Store`, `Microsoft.Store` o WinUI.

## Perché ora `Windows.Storage` viene risolto

I progetti che usano API WinRT targettizzano espressamente:

`net8.0-windows10.0.26100.0`

Il progetto WinRT aggiunge inoltre:

- `Microsoft.Windows.CsWinRT` 2.2.0
- `CsWinRTWindowsMetadata=10.0.26100.0`
- `CsWinRTComponent=true`

La classe `BridgePaths`, mancante nella precedente versione, è ora inclusa in
`GDG.VirtualPrinter.Core`.

## Architettura

1. `GDG.VirtualPrinter.VirtualPrinter`
   - componente C#/WinRT
   - riceve `PrintWorkflowVirtualPrinterDataAvailable`
   - salva l'OXPS nel publisher cache
   - scrive i metadati
   - lancia il full-trust launcher

2. `GDG.VirtualPrinter.Launcher`
   - sposta l'OXPS completo in `C:\ProgramData\GDG\VirtualPrinter\Jobs`
   - legge `bridge.json`
   - avvia il programma configurato
   - imposta le variabili REDMON_* nell'ambiente del processo figlio

3. `GDG.VirtualPrinter.Host`
   - applicazione full-trust minima usata come entry point del pacchetto

4. `GDG.VirtualPrinter.TestReceiver`
   - progetto **.NET Framework 4.8**
   - dimostra che il programma ricevente non deve essere .NET 8
   - scrive tutte le variabili in `C:\ProgramData\GDG\VirtualPrinter\Logs`

5. `GDG.VirtualPrinter.Packaging`
   - MSIX/WAP
   - registra `windows.printSupportVirtualPrinterWorkflow`
   - `PreferredInputFormat="application/oxps"`

## Variabili RedMon esposte

- REDMON_PORT
- REDMON_JOB
- REDMON_PRINTER
- REDMON_OUTPUTPRINTER
- REDMON_MACHINE
- REDMON_USER
- REDMON_DOCNAME
- REDMON_BASENAME
- REDMON_FILENAME
- REDMON_SESSIONID
- TEMP
- TMP

`TEMP` e `TMP` vengono ereditate normalmente dal processo padre.

Sono inoltre disponibili:

- GDG_SOURCE_APP
- GDG_JOB_SESSION_ID
- GDG_OXPS_FILENAME
- GDG_PRINTER_URI

### Nota su REDMON_JOB

Le nuove API `PrintWorkflowConfiguration` non espongono il vecchio Win32 spooler
JobId di RedMon. In questa versione `REDMON_JOB` viene valorizzata con
`PrintWorkflowConfiguration.SessionId`. Il valore è stabile per il job ma non va
assunto numerico.

## Configurazione

Al primo avvio viene creato:

`C:\ProgramData\GDG\VirtualPrinter\bridge.json`

Esempio:

```json
{
  "executablePath": "C:\\MySoftware\\Processor.exe",
  "arguments": "",
  "workingDirectory": "C:\\MySoftware",
  "jobsDirectory": "C:\\ProgramData\\GDG\\VirtualPrinter\\Jobs",
  "keepOxps": true,
  "redMonPort": "GDGVP1:",
  "redMonPrinter": "GDG Virtual Printer",
  "redMonOutputPrinter": ""
}
```

## Test con .NET Framework 4.8

Compilare `GDG.VirtualPrinter.TestReceiver`, poi:

```powershell
.\scripts\Install-TestReceiverConfig.ps1 `
  -ReceiverExe "C:\...\GDG.VirtualPrinter.TestReceiver.exe"
```

Stampando su **GDG Virtual Printer**, il receiver scrive un file di log con tutte
le variabili ricevute.

## Firma MSIX

Il progetto Packaging è intenzionalmente configurato con:

`AppxPackageSigningEnabled=false`

per non includere certificati privati o di test nel repository. Per distribuire
o installare il pacchetto MSIX occorre configurare un certificato il cui Subject
corrisponda al `Publisher` del manifest, oppure sostituire il Publisher con quello
del proprio certificato.

## Origine

La struttura del DesktopBridge e il PDC derivano concettualmente dal progetto
Cube.Psa.Samples di CubeSoft (Apache License 2.0). Vedere THIRD-PARTY-NOTICES.md.


## Novità v0.3 - UI e account di esecuzione

`GDG.VirtualPrinter.Host` è ora una vera UI WinForms di configurazione.

Consente di impostare:

- eseguibile
- argomenti
- directory di lavoro
- cartella OXPS
- mantenimento/cancellazione OXPS
- REDMON_PORT
- REDMON_PRINTER
- REDMON_OUTPUTPRINTER
- account di esecuzione

Sono disponibili due modalità:

- `CurrentUser`
- `SpecificAccount`

Per `SpecificAccount` la password NON viene scritta in `bridge.json`.
Viene salvata nel Windows Credential Manager con target predefinito:

`GDG.VirtualPrinter.RunAs`

La UI consente inoltre di:

- verificare le credenziali
- concedere all'account scelto il diritto `Modify` sulla cartella dei job
  tramite `icacls`
- lanciare un test del programma configurato con variabili REDMON_* simulate

### REDMON_USER e GDG_RUNAS_USER

`REDMON_USER` resta distinto dall'account di esecuzione.

- `REDMON_USER` = identità Windows disponibile nel contesto del launcher. La nuova
  API PSA non espone direttamente il vecchio campo Win32 "job submitter" di RedMon;
  quindi non va ancora considerato equivalente al 100% in scenari multiutente/RDS.
- `GDG_RUNAS_USER` = identità con cui viene avviato l'eseguibile configurato

Esempio:

```text
REDMON_USER=DOMAIN\utente
GDG_RUNAS_USER=DOMAIN\gdgprint
```

### Nota sui privilegi

La modifica ACL della cartella job può richiedere privilegi amministrativi a seconda
della directory scelta e delle policy della macchina.


## Novità v0.4 - RDS / multiutente

La v0.4 aggiunge una correlazione esplicita tra il job PSA e il job Win32 dello spooler.

Il resolver interroga la coda tramite `EnumJobsW` / `JOB_INFO_2` e prova a
correlare il job usando:

1. nome della coda (`GDG Virtual Printer`)
2. titolo documento
3. finestra temporale di 5 minuti
4. prossimità temporale
5. JobId come ultimo criterio di ordinamento

Se la correlazione è ambigua, NON viene scelto arbitrariamente un job.

Quando risolto, vengono valorizzati:

- `SpoolerJobId`
- `SubmitterUser`
- `SourceMachine`
- `SpoolerDocumentName`
- `SpoolerSubmittedUtc`

Poi `RdsSessionResolver` usa Terminal Services / WTS per cercare la sessione
dell'utente proprietario del job.

Se esiste una sola sessione attiva per quell'utente, o una sola sessione totale,
vengono valorizzati:

- `RdsSessionId`
- `RdsSessionName`
- `RdsClientName`
- `IsRemoteSession`

Se lo stesso account ha più sessioni compatibili e non è possibile distinguerle,
lo stato diventa `Ambiguous` e il bridge non inventa un SessionId.

### Variabili RedMon in v0.4

Quando la correlazione riesce:

- `REDMON_JOB` = vero JobId Win32 dello spooler
- `REDMON_USER` = proprietario del job (`JOB_INFO_2.pUserName`)
- `REDMON_MACHINE` = macchina sorgente (`JOB_INFO_2.pMachineName`)
- `REDMON_SESSIONID` = SessionId RDS/Terminal Services risolto
- `REDMON_DOCNAME` = titolo PSA del job
- `REDMON_FILENAME` = file OXPS completo

Rimangono inoltre disponibili le variabili diagnostiche:

- `GDG_WORKFLOW_SESSION_ID`
- `GDG_SPOOLER_JOB_ID`
- `GDG_RDS_SESSION_ID`
- `GDG_RDS_SESSION_NAME`
- `GDG_RDS_CLIENT_NAME`
- `GDG_IS_REMOTE_SESSION`
- `GDG_SPOOLER_RESOLUTION`
- `GDG_RDS_RESOLUTION`
- `GDG_RUNAS_USER`

### Distinzione fondamentale

`REDMON_USER` identifica il proprietario del job di stampa.
`GDG_RUNAS_USER` identifica invece l'account tecnico con cui viene eseguito il
processor configurato.

### Permessi RDS

La query di sessioni appartenenti ad altri utenti può richiedere il diritto
"Query Information" sulle sessioni RDS. In un RD Session Host il deployment
va quindi collaudato con l'account/contesto effettivo del package.

### Limite noto

`PrintWorkflowConfiguration.SessionId` è un ID della sessione del print workflow,
NON il SessionId Terminal Services. Per questo viene mantenuto separatamente in
`GDG_WORKFLOW_SESSION_ID`.

La correlazione spooler è best-effort perché la nuova API PSA non espone
direttamente il Win32 JobId. Lo stato di risoluzione è sempre esportato e non
vengono costruiti dati fittizi in caso di ambiguità.


## Novità v0.5 - formato consegnato al processor

La configurazione espone:

- OXPS originale
- XPS
- Entrambi

Il valore predefinito è `Xps`.

### OXPS originale

`REDMON_FILENAME` punta al file `.oxps`.

```text
GDG_OXPS_FILENAME=<file.oxps>
GDG_XPS_FILENAME=
GDG_PROCESSOR_FILENAME=<file.oxps>
GDG_OUTPUT_FORMAT=Oxps
```

### XPS

L'OXPS viene convertito in Microsoft XPS (MSXPS) tramite Windows XPS Object Model.
`REDMON_FILENAME` punta al file `.xps`.

```text
GDG_OXPS_FILENAME=<file.oxps>
GDG_XPS_FILENAME=<file.xps>
GDG_PROCESSOR_FILENAME=<file.xps>
GDG_OUTPUT_FORMAT=Xps
```

Se `KeepOxps=false`, l'OXPS viene eliminato dopo la conversione e l'esecuzione
del processor.

### Entrambi

Vengono mantenuti entrambi i file.

Per compatibilità RedMon, `REDMON_FILENAME` punta all'OXPS originale, mentre:

```text
GDG_OXPS_FILENAME=<file.oxps>
GDG_XPS_FILENAME=<file.xps>
GDG_PROCESSOR_FILENAME=<file.oxps>
GDG_OUTPUT_FORMAT=Both
```

consentono al processor di scegliere quale usare.

### Implementazione conversione

La conversione NON usa `XpsConverter.exe`.

È stato aggiunto il progetto nativo:

`GDG.VirtualPrinter.XpsConverter.Native`

che usa le API XPS OM:

- `IXpsOMObjectFactory1::CreatePackageFromFile1`
- `IXpsOMPackage1::WriteToFile1`
- `XPS_DOCUMENT_TYPE_XPS`

Queste API leggono OpenXPS e serializzano il modello in Microsoft XPS.

Il progetto C++ deve essere compilato per la stessa piattaforma del package:
Win32/x86, x64 o ARM64.


## Novità v0.6 - .NET 10 e dipendenze minime

La versione di riferimento passa a .NET 10:

- `GDG.VirtualPrinter.Core` -> `net10.0-windows10.0.26100.0`
- `GDG.VirtualPrinter.VirtualPrinter` -> `net10.0-windows10.0.26100.0`
- `GDG.VirtualPrinter.Launcher` -> `net10.0-windows10.0.26100.0`
- `GDG.VirtualPrinter.Host` -> `net10.0-windows10.0.26100.0`

`GDG.VirtualPrinter.TestReceiver` resta intenzionalmente .NET Framework 4.8 per
dimostrare che il processor esterno può continuare ad essere legacy.

### UI di configurazione

La UI WinForms è mantenuta integralmente. Rimangono disponibili:

- eseguibile del processor
- argomenti
- working directory
- cartella job
- CurrentUser / SpecificAccount
- Credential Manager
- verifica credenziali
- ACL della cartella job
- configurazione REDMON_*
- Test applicazione
- formato consegnato al processor:
  - OXPS originale
  - XPS
  - Entrambi

### Rimosso il progetto C++

`GDG.VirtualPrinter.XpsConverter.Native` è stato eliminato.

Non è quindi più necessario installare il workload Visual C++ per compilare la
soluzione.

### Conversione OXPS -> XPS

`XpsFormatConverter` è ora puro C# e usa direttamente COM/XPS Object Model
installato in Windows:

- `CLSID_XpsOMObjectFactory`
- `IXpsOMObjectFactory1::CreatePackageFromFile1`
- `IXpsOMPackage1::WriteToFile1`
- `XPS_DOCUMENT_TYPE_XPS`

L'interop COM è contenuto nel sorgente C# e usa soltanto `ole32.dll`, presente
nel sistema operativo. Non vengono introdotti package NuGet o DLL runtime per
la conversione.

### Dipendenze

La sola dipendenza NuGet specifica del componente PSA rimane:

`Microsoft.Windows.CsWinRT 2.3.1`

Non sono usati:

- WinUI
- Windows App SDK
- WPF
- Microsoft.UI.Xaml
- Microsoft.Windows.CsWin32
- XpsConverter.exe
- WDK runtime
- DLL C++ proprietarie

Le API Credential Manager, spooler, RDS e XPS OM vengono richiamate direttamente
dalle DLL di sistema Windows.

### Toolchain previsto

Per la soluzione v0.6:

- Visual Studio 2026
- .NET 10 SDK
- Windows 11 SDK build 26100 o successivo
- MSIX/Desktop Bridge tooling

Il workload C++ non è richiesto.


## v0.6.1
Correzione della chiamata COM XPS OM: i parametri BOOL `reuseObjects` e `optimizeMarkupSize` vengono ora passati come `false` anziché come interi `0`.


## v0.6.2 - launcher invisibile e manifest OXPS

- `GDG.VirtualPrinter.Launcher`: `OutputType=WinExe`.
- `GDG.VirtualPrinter.TestReceiver`: `OutputType=WinExe`.
- CurrentUser: `CreateNoWindow=true`, `WindowStyle=Hidden`.
- SpecificAccount: `CreateProcessWithLogonW` con `CREATE_NO_WINDOW`,
  `CREATE_UNICODE_ENVIRONMENT`, `LOGON_WITH_PROFILE` e `SW_HIDE`.
- Environment `REDMON_*` / `GDG_*` preservato anche con RunAs.
- Manifest:
  - `Version="0.6.2.0"`
  - `PreferredInputFormat="application/oxps"`
  - `SupportedFormats` assente
  - `SupportedFormat` assente
  - FullTrust launcher: `GDG.VirtualPrinter.Host\GDG.VirtualPrinter.Launcher.exe`
