# Compatibility entry point: package the complete latest update.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'package-summary-update.ps1')
