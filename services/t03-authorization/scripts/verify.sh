#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore Scutum.T03.sln
dotnet build Scutum.T03.sln --configuration Release --no-restore
dotnet test Scutum.T03.sln --configuration Release --no-build --logger "trx;LogFileName=security.trx" --results-directory TestResults
python3 scripts/check_source.py
dotnet run --project src/Scutum.T03.ConsoleUi --configuration Release --no-build
