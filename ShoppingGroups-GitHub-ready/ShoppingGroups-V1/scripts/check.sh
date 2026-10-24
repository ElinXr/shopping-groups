#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
dotnet run --project tests/ShoppingGroups.Domain.Tests -c Release
dotnet build src/ShoppingGroups.Api -c Release
dotnet build tests/ShoppingGroups.Api.Tests -c Release
npm ci --prefix web --ignore-scripts
npm test --prefix web
npm run test:config
npm run build --prefix web
trap 'docker compose -f compose.tests.yaml down --volumes' EXIT
docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests
