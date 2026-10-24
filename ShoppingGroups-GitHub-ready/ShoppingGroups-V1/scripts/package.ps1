$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactDir = Join-Path $repoRoot 'artifacts'
[IO.Directory]::CreateDirectory($artifactDir) | Out-Null
$archivePath = Join-Path $artifactDir 'ShoppingGroups-V1.zip'
$rootFiles = Get-ChildItem -LiteralPath $repoRoot -File -Force | Where-Object {
    ($_.Name -notlike '.env*' -or $_.Name -eq '.env.example') -and $_.Name -ne '.neon' -and $_.Extension -ne '.zip'
}
$projectFiles = foreach ($folder in @('.github', 'deploy', 'docs', 'scripts', 'src', 'tests', 'web')) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $folder) -Recurse -File -Force | Where-Object {
        $_.FullName -notmatch '[\\/](node_modules|bin|obj|dist)[\\/]' -and $_.Extension -ne '.tsbuildinfo'
    }
}
Add-Type -AssemblyName System.IO.Compression
$output = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($output, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($rootFiles) + @($projectFiles)) {
        $entryName = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
        if ($entryName.StartsWith('../')) { throw 'A source file escaped the project directory.' }
        $entry = $zip.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
        $destination = $entry.Open()
        $source = [IO.File]::OpenRead($file.FullName)
        try { $source.CopyTo($destination) }
        finally { $source.Dispose(); $destination.Dispose() }
    }
}
finally { $zip.Dispose(); $output.Dispose() }
Write-Output $archivePath
