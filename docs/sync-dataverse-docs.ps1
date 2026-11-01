<#
.SYNOPSIS
    Clones or updates the Microsoft Power Apps documentation used for Dataverse development.

.DESCRIPTION
    Maintains a shallow, sparse checkout of MicrosoftDocs/powerapps-docs in
    docs/powerapps-docs. Only the Dataverse developer documentation path is
    checked out. The local clone is ignored by git.

.EXAMPLE
    pwsh ./docs/sync-dataverse-docs.ps1
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot 'powerapps-docs')
)

$ErrorActionPreference = 'Stop'
$repositoryUrl = 'https://github.com/MicrosoftDocs/powerapps-docs.git'
$sparsePath = 'powerapps-docs/developer/data-platform'

if (Test-Path (Join-Path $Destination '.git')) {
    Write-Host "Pulling latest Dataverse documentation into $Destination..."
    git -C $Destination pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        throw "git pull failed with exit code $LASTEXITCODE."
    }
}
elseif (Test-Path $Destination) {
    throw "Destination '$Destination' exists but is not a git repository. Remove it or choose another path."
}
else {
    Write-Host "Cloning Dataverse documentation into $Destination..."
    git clone --depth 1 --filter=blob:none --sparse $repositoryUrl $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "git clone failed with exit code $LASTEXITCODE."
    }

    git -C $Destination sparse-checkout set $sparsePath
    if ($LASTEXITCODE -ne 0) {
        throw "git sparse-checkout failed with exit code $LASTEXITCODE."
    }
}

$docsPath = Join-Path $Destination $sparsePath
Write-Host "Dataverse documentation is ready at $docsPath"
