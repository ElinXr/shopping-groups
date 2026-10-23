$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet run --project tests/ShoppingGroups.Domain.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Domain tests failed.' }
    dotnet build src/ShoppingGroups.Api -c Release
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    dotnet build tests/ShoppingGroups.Api.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
    npm ci --prefix web --ignore-scripts
    if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
    npm test --prefix web
    if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
    npm run build --prefix web
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
}
finally {
    docker compose -f compose.tests.yaml down --volumes
    Pop-Location
}
