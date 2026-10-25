$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$archivePath = Join-Path $repoRoot 'artifacts/ShoppingGroups-Friendly-Summary-v4.zip'
[IO.Directory]::CreateDirectory((Split-Path -Parent $archivePath)) | Out-Null
$files = @(
    'compose.tests.yaml',
    'src/ShoppingGroups.Domain/WebPushProtocol.cs',
    'src/ShoppingGroups.Domain/NotificationText.cs',
    'src/ShoppingGroups.Api/PushNotifications.cs',
    'src/ShoppingGroups.Api/Migrations/DevicePushNotifications.cs',
    'src/ShoppingGroups.Api/Migrations/DetailedDevicePush.cs',
    'src/ShoppingGroups.Api/AppDb.cs',
    'src/ShoppingGroups.Api/Program.cs',
    'src/ShoppingGroups.Api/Workers.cs',
    'src/ShoppingGroups.Api/Collaboration.cs',
    'src/ShoppingGroups.Api/ListEndpoints.cs',
    'src/ShoppingGroups.Api/AuthEndpoints.cs',
    'web/src/push-device.ts',
    'web/src/push.ts',
    'web/src/PushSettings.tsx',
    'web/src/api.ts',
    'web/src/App.tsx',
    'web/src/styles.css',
    'web/public/sw.js',
    'web/vite.config.ts',
    'web/tests/push-device.test.ts',
    'web/tests/service-worker.test.ts',
    'tests/ShoppingGroups.Domain.Tests/Program.cs',
    'tests/ShoppingGroups.Api.Tests/Program.cs',
    'tests/ShoppingGroups.Api.Tests/PushTestReceiver.cs',
    'docs/PUSH-NOTIFICATIONS.md',
    'docs/API.md',
    'docs/ARCHITECTURE.md',
    'docs/VERIFICATION.md',
    'README.md',
    'scripts/package-push-update.ps1',
    'src/ShoppingGroups.Domain/ProductAmount.cs',
    'src/ShoppingGroups.Api/Models.cs',
    'src/ShoppingGroups.Api/ProductEndpoints.cs',
    'src/ShoppingGroups.Api/AdminEndpoints.cs',
    'src/ShoppingGroups.Api/GroupEndpoints.cs',
    'src/ShoppingGroups.Api/Migrations/ProductExperience.cs',
    'web/src/types.ts',
    'web/src/helpers.ts',
    'web/src/GroupPicker.tsx',
    'web/src/group-selection.ts',
    'web/src/ProductForm.tsx',
    'web/src/products.ts',
    'web/src/history.ts',
    'web/src/NotificationCenter.tsx',
    'web/tests/helpers.test.ts',
    'web/tests/api.test.ts',
    'web/tests/products.test.ts',
    'web/tests/history.test.ts',
    'web/tests/group-selection.test.ts',
    'tests/ShoppingGroups.Api.Tests/ProductExperienceTests.cs',
    'docs/PRODUCT-IMPROVEMENTS-V3.md',
    'scripts/package-product-update.ps1',
    'web/src/SummaryPreview.tsx',
    'web/src/summary-preview.ts',
    'web/tests/summary-preview.test.ts',
    'docs/FRIENDLY-SUMMARY-V4.md',
    'scripts/package-summary-update.ps1'
)
Add-Type -AssemblyName System.IO.Compression
$output = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($output, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $files) {
        $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
        if (-not $path.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Update source escaped the workspace.'
        }
        $entry = $zip.CreateEntry('ShoppingGroups-V1/' + $relative, [IO.Compression.CompressionLevel]::Optimal)
        $source = [IO.File]::OpenRead($path)
        $destination = $entry.Open()
        try { $source.CopyTo($destination) }
        finally { $source.Dispose(); $destination.Dispose() }
    }
}
finally { $zip.Dispose(); $output.Dispose() }
Write-Output $archivePath
