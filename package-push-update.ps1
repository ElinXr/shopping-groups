# Compatibility entry point: always package the complete current update.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'package-product-update.ps1')
