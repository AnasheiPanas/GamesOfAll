param(
    [ValidateSet("native", "emulated", "stats", "discovery")]
    [string]$Mode = "native"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition))
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

$sources = @(Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName })
$sources += (Join-Path $root "tools\preview\PreviewEntry.cs")

$out = Join-Path $env:TEMP "opencode\GamesOfAllPreview.exe"
$cscArgs = @(
    "/nologo", "/target:winexe", "/codepage:65001", "/main:GamesOfAll.PreviewEntry",
    "/out:$out",
    "/r:System.dll", "/r:System.Core.dll", "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll", "/r:System.Web.Extensions.dll"
) + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de la vista previa." }
Write-Host "Vista previa: $out"
