param(
    [Parameter(Mandatory=$true)]
    [string]$ReceiverExe
)

$root = Join-Path $env:ProgramData "GDG\VirtualPrinter"
New-Item -ItemType Directory -Force -Path $root | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "Jobs") | Out-Null

$config = @{
    executablePath = $ReceiverExe
    arguments = ""
    workingDirectory = (Split-Path -Parent $ReceiverExe)
    jobsDirectory = (Join-Path $root "Jobs")
    keepOxps = $true
    redMonPort = "GDGVP1:"
    redMonPrinter = "GDG Virtual Printer"
    redMonOutputPrinter = ""
}

$config | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $root "bridge.json")
Write-Host "Configurazione scritta in $root\bridge.json"
