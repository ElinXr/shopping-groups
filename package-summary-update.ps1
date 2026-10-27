# Compatibility entry point: package the complete latest update.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'package-appearance-update.ps1')
