#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Finds the documentation IDs that make the Microsoft.AspNetCore.OpenApi XML-comment cache throw "An item with the same
	key has already been added", says where each one comes from, and prints the configuration that fixes it.

.DESCRIPTION
	The generator adds, with Dictionary.Add, every <member> of every project-reference XML documentation file in
	AdditionalFiles, and every public documented symbol of the API's own compilation. This script asks MSBuild for exactly
	the files the generator reads (with Purview.OpenAPI.XmlComments' filtering turned off, so the raw inputs are seen),
	then:

	  - lists every ID that occurs in two or more referenced documentation files ("BetweenReferences");
	  - lists every ID that occurs in one referenced file and in the API's own documentation file
	    ("ReferenceAndProject" - what POAX001 reports; needs the API to have been built);
	  - reads the generated cache, when the API was built with EmitCompilerGeneratedFiles=true, and lists every key
	    added more than once - the definitive answer to "will it throw";
	  - reports whether the generator is present, whether Purview.OpenAPI.XmlComments is installed and enabled, the IDs
	    already excluded, and any project file that looks like it removes the generator;
	  - says, for each finding, whether the current configuration already handles it, and prints what to add when not.

	Run it from, or point it at, the project that calls AddOpenApi(). It restores and evaluates the project and builds its
	project references (ResolveReferences does), but does not compile the project itself.

.PARAMETER Project
	The API project: a .csproj file or the folder holding one. Defaults to the current folder.

.PARAMETER TargetFramework
	The target framework to inspect, for a multi-targeted project. Defaults to the project's TargetFramework, or the first
	of its TargetFrameworks.

.PARAMETER Configuration
	The build configuration. Defaults to Debug.

.PARAMETER Json
	Writes the report as JSON instead of text.

.OUTPUTS
	Exit code 0 when nothing would throw with the current configuration, 1 when something would, 2 when the project
	could not be inspected.

.EXAMPLE
	pwsh ./find-duplicate-documentation-ids.ps1 -Project src/Api

.EXAMPLE
	pwsh ./find-duplicate-documentation-ids.ps1 -Project src/Api/Api.csproj -TargetFramework net10.0 -Json
#>
[CmdletBinding()]
param (
	[string] $Project = '.',
	[string] $TargetFramework,
	[string] $Configuration = 'Debug',
	[switch] $Json
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ProjectFile([string] $path) {
	$item = Get-Item -LiteralPath $path
	if (-not $item.PSIsContainer) { return $item.FullName }
	$candidates = @(Get-ChildItem -LiteralPath $item.FullName -Filter '*.csproj' -File)
	if ($candidates.Count -ne 1) {
		throw "Expected one .csproj in '$($item.FullName)', found $($candidates.Count). Pass -Project <file>.csproj."
	}
	return $candidates[0].FullName
}

function Invoke-MSBuildQuery([string] $projectFile, [string[]] $arguments) {
	$output = & dotnet msbuild $projectFile -nologo -nodeReuse:false "-p:Configuration=$Configuration" @arguments 2>&1 |
		ForEach-Object { "$_" }
	$text = $output -join "`n"
	if ($LASTEXITCODE -ne 0) { throw "dotnet msbuild failed for '$projectFile':`n$text" }
	$start = $text.IndexOf('{')
	if ($start -lt 0) { throw "dotnet msbuild returned no JSON for '$projectFile':`n$text" }
	return $text.Substring($start) | ConvertFrom-Json
}

# Mirrors XmlCommentGenerator.NormalizeDocId, the generator's cache key.
function Get-NormalizedId([string] $id) {
	$tilde = $id.IndexOf('~')
	if ($tilde -lt 0 -or -not $id.StartsWith('M:') -or $id.Contains('.op_Implicit(') -or $id.Contains('.op_Explicit(')) {
		return $id
	}
	return $id.Substring(0, $tilde)
}

# The normalised IDs of a compiler XML documentation file, or $null when the file is not one.
function Get-DocumentationIds([string] $path) {
	if (-not (Test-Path -LiteralPath $path)) { return $null }
	try { [xml] $document = Get-Content -LiteralPath $path -Raw } catch { return $null }
	if ($null -eq $document.DocumentElement -or $document.DocumentElement.LocalName -ne 'doc') { return $null }
	$members = $document.SelectNodes('/doc/members/member[@name]')
	if ($null -eq $document.SelectSingleNode('/doc/members')) { return $null }
	return @($members | ForEach-Object { Get-NormalizedId $_.GetAttribute('name') })
}

function Get-Items($query, [string] $name) {
	if ($query.PSObject.Properties.Name -notcontains 'Items') { return @() }
	if ($query.Items.PSObject.Properties.Name -notcontains $name) { return @() }
	return @($query.Items.$name)
}

try {
	$projectFile = Resolve-ProjectFile $Project
	$projectDirectory = Split-Path -Parent $projectFile

	$frameworkArguments = @()
	$evaluation = Invoke-MSBuildQuery $projectFile @(
		'-restore',
		'-getProperty:TargetFramework', '-getProperty:TargetFrameworks'
	)
	$framework = $TargetFramework
	if (-not $framework) { $framework = $evaluation.Properties.TargetFramework }
	if (-not $framework) { $framework = ($evaluation.Properties.TargetFrameworks -split ';' | Where-Object { $_ })[0] }
	if (-not $framework) { throw "'$projectFile' declares no TargetFramework." }
	$frameworkArguments = @("-p:TargetFramework=$framework")

	$settings = Invoke-MSBuildQuery $projectFile (@(
		'-getProperty:PurviewOpenAPIXmlCommentsTaskAssembly',
		'-getProperty:PurviewOpenAPIXmlCommentDeduplication',
		'-getProperty:IntermediateOutputPath',
		'-getProperty:GenerateDocumentationFile',
		'-getItem:PurviewOpenAPIXmlCommentExclude',
		'-getItem:DocFileItem'
	) + $frameworkArguments)


	$packageInstalled = [bool] $settings.Properties.PurviewOpenAPIXmlCommentsTaskAssembly
	$packageEnabled = $packageInstalled -and $settings.Properties.PurviewOpenAPIXmlCommentDeduplication -eq 'true'
	$excluded = @(Get-Items $settings 'PurviewOpenAPIXmlCommentExclude' | ForEach-Object { Get-NormalizedId $_.Identity })

	# The raw inputs: the package's filtering is turned off for this query only.
	$references = Invoke-MSBuildQuery $projectFile (@(
		'-t:ResolveReferences',
		'-getItem:AdditionalFiles',
		'-getItem:Analyzer',
		'-p:PurviewOpenAPIXmlCommentDeduplication=false'
	) + $frameworkArguments)

	# Package analyzers are only added once package assets are resolved, so this is read after ResolveReferences.
	$generatorPresent = @(Get-Items $references 'Analyzer' |
		Where-Object { $_.Filename -eq 'Microsoft.AspNetCore.OpenApi.SourceGenerators' }).Count -gt 0

	$referenceDocumentation = [ordered] @{}
	foreach ($file in Get-Items $references 'AdditionalFiles' | Where-Object { $_.Extension -eq '.xml' }) {
		$ids = Get-DocumentationIds $file.FullPath
		if ($null -ne $ids) { $referenceDocumentation[$file.FullPath] = $ids }
	}

	$sources = @{}
	foreach ($path in $referenceDocumentation.Keys) {
		foreach ($id in $referenceDocumentation[$path]) {
			if (-not $sources.ContainsKey($id)) { $sources[$id] = [System.Collections.Generic.List[string]]::new() }
			$sources[$id].Add([IO.Path]::GetFileName($path))
		}
	}

	$docFile = @(Get-Items $settings 'DocFileItem' | Select-Object -First 1)
	$projectDocumentationPath = $docFile.Count -gt 0 ? $docFile[0].FullPath : $null
	$projectIds = $projectDocumentationPath ? (Get-DocumentationIds $projectDocumentationPath) : $null
	$projectIdSet = [System.Collections.Generic.HashSet[string]]::new([string[]] @($projectIds ?? @()))

	$findings = [System.Collections.Generic.List[object]]::new()
	foreach ($id in ($sources.Keys | Sort-Object { $_ } -CaseSensitive)) {
		$count = $sources[$id].Count
		if ($count -gt 1) {
			$findings.Add([pscustomobject] @{
					Id = $id; Kind = 'BetweenReferences'; Sources = @($sources[$id]); InProject = $projectIdSet.Contains($id)
					Handled = $packageEnabled
				})
		}
		elseif ($projectIdSet.Contains($id)) {
			$findings.Add([pscustomobject] @{
					Id = $id; Kind = 'ReferenceAndProject'; Sources = @($sources[$id]); InProject = $true
					Handled = $packageEnabled -and $excluded -contains $id
				})
		}
	}

	$cache = $null
	$intermediate = Join-Path $projectDirectory $settings.Properties.IntermediateOutputPath
	$generated = if (Test-Path -LiteralPath $intermediate) {
		Get-ChildItem -LiteralPath $intermediate -Recurse -Filter 'OpenApiXmlCommentSupport.generated.cs' -File |
			Select-Object -First 1
	}
	if ($generated) {
		$keys = [regex]::Matches((Get-Content -LiteralPath $generated.FullName -Raw), 'cache\.Add\(@"((?:[^"]|"")*)"') |
			ForEach-Object { $_.Groups[1].Value.Replace('""', '"') }
		$cache = [pscustomobject] @{
			Path          = $generated.FullName
			Written       = $generated.LastWriteTime.ToString('s')
			DuplicateKeys = @($keys | Group-Object -CaseSensitive | Where-Object Count -gt 1 |
					ForEach-Object { [pscustomobject] @{ Id = $_.Name; Count = $_.Count } })
		}
	}

	$workarounds = [System.Collections.Generic.List[string]]::new()
	$directory = Get-Item -LiteralPath $projectDirectory
	$scanned = [System.Collections.Generic.List[string]]::new()
	$scanned.Add($projectFile)
	while ($null -ne $directory) {
		foreach ($name in 'Directory.Build.props', 'Directory.Build.targets') {
			$candidate = Join-Path $directory.FullName $name
			if (Test-Path -LiteralPath $candidate) { $scanned.Add($candidate) }
		}
		$directory = $directory.Parent
	}
	foreach ($file in $scanned) {
		Select-String -LiteralPath $file -Pattern 'Microsoft\.AspNetCore\.OpenApi\.SourceGenerators' |
			Where-Object { $_.Line -match 'Remove' } |
			ForEach-Object { $workarounds.Add("$($_.Path):$($_.LineNumber)") }
	}

	$recommendations = [System.Collections.Generic.List[string]]::new()
	if (-not $generatorPresent) {
		$recommendations.Add('The project does not reference Microsoft.AspNetCore.OpenApi, so its XML-comment generator cannot fail here.')
	}
	if ($workarounds.Count -gt 0) {
		$recommendations.Add("Remove the generator workaround at $($workarounds -join ', ') once the findings below are handled: it drops every XML description.")
	}
	$unhandled = @($findings | Where-Object { -not $_.Handled })
	if (-not $packageInstalled -and $findings.Count -gt 0) {
		$recommendations.Add('Install Purview.OpenAPI.XmlComments in this project: <PackageReference Include="Purview.OpenAPI.XmlComments" Version="<latest>" PrivateAssets="all" />')
	}
	elseif ($packageInstalled -and -not $packageEnabled -and $findings.Count -gt 0) {
		$recommendations.Add('Purview.OpenAPI.XmlComments is installed but PurviewOpenAPIXmlCommentDeduplication is not true; remove the override.')
	}
	$needExclude = @($findings | Where-Object { $_.Kind -eq 'ReferenceAndProject' -and $excluded -notcontains $_.Id })
	if ($needExclude.Count -gt 0) {
		$items = ($needExclude | ForEach-Object { "`t<PurviewOpenAPIXmlCommentExclude Include=`"$($_.Id)`" />" }) -join "`n"
		$recommendations.Add("Add to the API project (these are each in one reference and in the API itself, so they are not duplicates among the references):`n<ItemGroup>`n$items`n</ItemGroup>")
	}
	if ($null -eq $projectIds) {
		$recommendations.Add('The API has not been built (or does not generate documentation), so IDs shared with its own compilation could not be checked. Build it and run this again.')
	}
	if ($null -eq $cache -and $generatorPresent) {
		$recommendations.Add('No generated cache was found. Build with -p:EmitCompilerGeneratedFiles=true to confirm the result against the cache itself.')
	}
	$cacheDuplicates = $cache ? @($cache.DuplicateKeys) : @()
	if ($cacheDuplicates.Count -gt 0) {
		$recommendations.Add("The generated cache from the last build adds $($cacheDuplicates.Count) key(s) more than once, so that build throws. Rebuild after changing the configuration; if it persists, the colliding copies are not coming from referenced XML files.")
	}

	$report = [pscustomobject] @{
		Project                 = $projectFile
		TargetFramework         = $framework
		GeneratorPresent        = $generatorPresent
		PackageInstalled        = $packageInstalled
		PackageEnabled          = $packageEnabled
		Excluded                = $excluded
		Workarounds             = @($workarounds)
		ReferenceDocumentation  = @($referenceDocumentation.Keys)
		ProjectDocumentation    = $projectDocumentationPath
		ProjectDocumentationRead = $null -ne $projectIds
		Findings                = @($findings)
		Cache                   = $cache
		WouldThrow              = ($unhandled.Count -gt 0) -or ($cacheDuplicates.Count -gt 0)
		Recommendations         = @($recommendations)
	}

	if ($Json) {
		$report | ConvertTo-Json -Depth 6
	}
	else {
		Write-Host "Project:            $($report.Project) ($framework)"
		Write-Host "Generator present:  $generatorPresent"
		Write-Host "Package installed:  $packageInstalled$($packageInstalled ? " (enabled: $packageEnabled)" : '')"
		Write-Host "Excluded IDs:       $(@($excluded).Count -gt 0 ? ($excluded -join ', ') : 'none')"
		Write-Host "Reference docs:     $(@($report.ReferenceDocumentation).Count) file(s)"
		Write-Host "Project docs read:  $($report.ProjectDocumentationRead)"
		Write-Host ''
		if ($findings.Count -eq 0) {
			Write-Host 'No documentation ID reaches the generator twice from the referenced documentation.' -ForegroundColor Green
		}
		foreach ($finding in $findings) {
			$state = $finding.Handled ? 'handled' : 'NOT HANDLED'
			$colour = $finding.Handled ? 'Green' : 'Red'
			$also = $finding.InProject ? ' + this project' : ''
			Write-Host ("[{0}] {1}  {2}: {3}{4}" -f $state, $finding.Kind, $finding.Id, ($finding.Sources -join ', '), $also) -ForegroundColor $colour
		}
		if ($cache) {
			Write-Host ''
			Write-Host "Generated cache ($($cache.Written)): $($cache.Path)"
			if ($cacheDuplicates.Count -eq 0) { Write-Host '  every key is added once' -ForegroundColor Green }
			foreach ($key in $cacheDuplicates) { Write-Host "  $($key.Id) is added $($key.Count) times" -ForegroundColor Red }
		}
		if ($recommendations.Count -gt 0) {
			Write-Host ''
			Write-Host 'Next:'
			foreach ($recommendation in $recommendations) { Write-Host "- $recommendation" }
		}
		Write-Host ''
		Write-Host ($report.WouldThrow ? 'Result: /openapi/v1.json would still throw.' : 'Result: nothing would throw with the current configuration.') -ForegroundColor ($report.WouldThrow ? 'Red' : 'Green')
	}

	exit ($report.WouldThrow ? 1 : 0)
}
catch {
	# Not Write-Error: under ErrorActionPreference=Stop it rethrows, and the documented exit code 2 becomes 1.
	[Console]::Error.WriteLine("find-duplicate-documentation-ids: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))")
	exit 2
}
