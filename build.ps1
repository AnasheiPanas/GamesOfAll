# Compila GamesOfAll.exe (WinForms sobre .NET Framework, sin dependencias externas).
param(
    [switch]$Run,
    [switch]$Shortcut
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "No se encontro el compilador de C# de .NET Framework 4." }

$icon = Join-Path $root "assets\gamesofall.ico"
if (-not (Test-Path $icon)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\make-icon.ps1")
}

$sources = @(Get-ChildItem (Join-Path $root "src") -Filter *.cs |
    Sort-Object Name |
    ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No hay archivos .cs en src." }

$exe = Join-Path $root "GamesOfAll.exe"
$manifest = Join-Path $root "assets\app.manifest"

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/debug-",
    "/codepage:65001",
    "/platform:anycpu",
    "/out:$exe",
    "/win32manifest:$manifest",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.Web.Extensions.dll"
)
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
$cscArgs += $sources

Write-Host "Compilando..." -ForegroundColor Cyan
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo (codigo $LASTEXITCODE)." }

Write-Host "Listo: $exe" -ForegroundColor Green

if ($Shortcut) {
    $ws = New-Object -ComObject WScript.Shell
    $lnkPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "GamesOfAll.lnk"
    $lnk = $ws.CreateShortcut($lnkPath)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $root
    $lnk.IconLocation = $exe
    $lnk.Description = "GamesOfAll - tu biblioteca de juegos"
    $lnk.Save()
    Write-Host "Acceso directo creado en el escritorio." -ForegroundColor Green
}

if ($Run) {
    Start-Process -FilePath $exe -WorkingDirectory $root
}
