#!/usr/bin/env bash
# usage: run-acceptance.sh <repo-checkout> <brief-name>   (acceptance file in /home/claude/acceptance)
set -euo pipefail
R=$1; B=$2; D=$R/tests/App.Tests/Acceptance
mkdir -p $D && cp /home/claude/acceptance/AcceptanceBase.cs /home/claude/acceptance/$B.acceptance.cs $D/
export PATH=/home/claude/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 TEST_POSTGRES="Host=localhost;Username=postgres;Password=x"
cd $R && dotnet test tests/App.Tests -p:TreatWarningsAsErrors=false -p:EnforceCodeStyleInBuild=false -- --filter-namespace Acceptance 2>&1 | tail -40
