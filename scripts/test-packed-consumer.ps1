#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Proves the packed Purview.OpenAPI.XmlComments package works for a real consumer, through PackageReference alone.

.DESCRIPTION
	The integration tests import the package's props and targets from source and point them at a built task assembly.
	This script covers what they cannot: NuGet's buildTransitive import, the packed tasks/netstandard2.0 path the props
	resolve, and the development-dependency shape.

	It packs the package to an isolated feed, copies scripts/packed-consumer out of the repository (so nothing in the
	repository applies to it), restores it with an isolated package cache, then:

	  1. builds it and fails on POAX001 - two libraries carry the injected type, so it is a plain duplicate;
	  2. asserts the generated XML-comment cache adds T:Ambient.InjectedAttribute exactly once;
	  3. asserts the package left nothing in the build output (no task assembly, no MSBuild assembly);
	  4. runs the API, which requests its own /openapi/v1.json, and asserts a 200 carrying both libraries' descriptions.

.PARAMETER PackOutput
	Folder to pack into. Defaults to a scratch folder.

.PARAMETER Configuration
	Configuration to pack. Defaults to Release.

.EXAMPLE
	./scripts/test-packed-consumer.ps1
#>
[CmdletBinding()]
param (
	[string] $PackOutput,
	[string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $repositoryRoot 'package.json') -Raw | ConvertFrom-Json).version
$scratch = Join-Path ([IO.Path]::GetTempPath()) "purview-openapi-xmlcomments-packed-$([Guid]::NewGuid().ToString('N'))"
# Absolute, because pack runs from the repository root and the consumer's nuget.config lives in the scratch folder.
$feed = $PackOutput ? $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PackOutput) : (Join-Path $scratch 'feed')
$packages = Join-Path $scratch 'packages'
$consumer = Join-Path $scratch 'consumer'

function Assert-That([bool] $condition, [string] $message) {
	if (-not $condition) { throw "Packed consumer: $message" }
	Write-Host "  ok  $message" -ForegroundColor Green
}

function Invoke-DotNet([string] $workingDirectory, [string[]] $arguments) {
	Push-Location $workingDirectory
	try {
		$output = & dotnet @arguments 2>&1 | ForEach-Object { "$_" }
		return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
	}
	finally { Pop-Location }
}

Write-Host "Packing Purview.OpenAPI.XmlComments $version ($Configuration) to $feed"
$pack = Invoke-DotNet $repositoryRoot @('pack', 'src/src/XmlComments/XmlComments.csproj', '-c', $Configuration, '-o', $feed)
if ($pack.ExitCode -ne 0) { Write-Host $pack.Output; throw 'dotnet pack failed.' }

Copy-Item (Join-Path $PSScriptRoot 'packed-consumer') $consumer -Recurse
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<clear />
		<add key="packed" value="$feed" />
		<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
	</packageSources>
	<packageSourceMapping>
		<packageSource key="packed">
			<package pattern="Purview.OpenAPI.XmlComments" />
		</packageSource>
		<packageSource key="nuget.org">
			<package pattern="*" />
		</packageSource>
	</packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $consumer 'nuget.config')

$previousPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = $packages
try {
	Write-Host 'Building the consumer'
	$build = Invoke-DotNet $consumer @('build', 'Api/Api.csproj', '-nodeReuse:false', "-p:PurviewOpenAPIXmlCommentsVersion=$version")
	if ($build.ExitCode -ne 0) { Write-Host $build.Output; throw 'The consumer build failed.' }
	Assert-That ($build.Output -notmatch 'POAX001') 'the build reports no POAX001'

	$generated = Get-ChildItem (Join-Path $consumer 'Api/obj') -Recurse -Filter 'OpenApiXmlCommentSupport.generated.cs' | Select-Object -First 1
	Assert-That ($null -ne $generated) 'the OpenAPI XML-comment generator ran'
	$entries = @(Select-String -Path $generated.FullName -Pattern 'cache\.Add\(@"T:Ambient\.InjectedAttribute"').Count
	Assert-That ($entries -eq 1) "the cache adds T:Ambient.InjectedAttribute once (found $entries)"

	$output = Join-Path $consumer 'Api/bin/Debug/net10.0'
	$leaked = @(Get-ChildItem $output -Filter '*.dll' | Where-Object { $_.Name -like 'Purview.OpenAPI.XmlComments*' -or $_.Name -like 'Microsoft.Build*' })
	Assert-That ($leaked.Count -eq 0) 'the package left no assembly in the build output'

	Write-Host 'Running the consumer'
	$run = Invoke-DotNet (Join-Path $consumer 'Api') @("bin/Debug/net10.0/Api.dll")
	if ($run.ExitCode -ne 0) { Write-Host $run.Output }
	Assert-That ($run.ExitCode -eq 0) '/openapi/v1.json answers a success status'
	Assert-That ($run.Output -match 'Widget from library A\.') "library A's description is in the document"
	Assert-That ($run.Output -match 'Widget from library B\.') "library B's description is in the document"
}
finally {
	$env:NUGET_PACKAGES = $previousPackages
}

Write-Host "Packed consumer passed ($scratch)." -ForegroundColor Green
