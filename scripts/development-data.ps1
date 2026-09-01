[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('seed', 'reset')]
    [string]$Action
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    docker compose exec -T backend dotnet run `
        --project src/backend/EngageOps.Api/EngageOps.Api.csproj `
        --no-build `
        --no-launch-profile `
        -- development-data $Action

    if ($LASTEXITCODE -ne 0) {
        throw "Development data $Action failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
