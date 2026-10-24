$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
& (Join-Path $PSScriptRoot 'package.ps1') | Out-Null
$sourcePath = Join-Path $repoRoot 'artifacts/ShoppingGroups-V1.zip'
$targetPath = Join-Path $repoRoot 'artifacts/ShoppingGroups-GitHub-ready.zip'
$workflow = [IO.File]::ReadAllText((Join-Path $repoRoot '.github/workflows/ci.yml')).Replace("`r`n", "`n")
$workflow = $workflow.Replace('name: Verify', 'name: Verify uploaded project')
$workflow = $workflow.Replace('on: [push, pull_request]', 'on: [push, pull_request, workflow_dispatch]')
$workflow = $workflow.Replace("jobs:`n", "defaults:`n  run:`n    working-directory: ShoppingGroups-V1`njobs:`n")
$workflow = $workflow.Replace('cache-dependency-path: web/package-lock.json', 'cache-dependency-path: ShoppingGroups-V1/web/package-lock.json')
Add-Type -AssemblyName System.IO.Compression
$sourceZip = [IO.Compression.ZipFile]::OpenRead($sourcePath)
$targetFile = [IO.File]::Open($targetPath, [IO.FileMode]::Create)
$targetZip = [IO.Compression.ZipArchive]::new($targetFile, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $sourceZip.Entries) {
        $copy = $targetZip.CreateEntry('ShoppingGroups-V1/' + $entry.FullName, [IO.Compression.CompressionLevel]::Optimal)
        $inputStream = $entry.Open()
        $outputStream = $copy.Open()
        try { $inputStream.CopyTo($outputStream) }
        finally { $inputStream.Dispose(); $outputStream.Dispose() }
    }
    $workflowEntry = $targetZip.CreateEntry('.github/workflows/verify.yml')
    $writer = [IO.StreamWriter]::new($workflowEntry.Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($workflow) }
    finally { $writer.Dispose() }
}
finally { $targetZip.Dispose(); $targetFile.Dispose(); $sourceZip.Dispose() }
Write-Output $targetPath
