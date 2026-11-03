# Configuring Purview.OpenAPI.XmlComments

Every setting is MSBuild. The package's defaults are imported before the project body, so a value set in the project
or in `Directory.Build.props` wins.

## Installing

Into the project that calls `AddOpenApi()` - the only project that needs it:

```xml
<ItemGroup>
	<PackageReference Include="Purview.OpenAPI.XmlComments" Version="<latest>" PrivateAssets="all" />
</ItemGroup>
```

With Central Package Management (`Directory.Packages.props`):

```xml
<ItemGroup>
	<PackageVersion Include="Purview.OpenAPI.XmlComments" Version="<latest>" />
</ItemGroup>
```

and `<PackageReference Include="Purview.OpenAPI.XmlComments" PrivateAssets="all" />` in the API project.

For every project in the repository at once (harmless where there is no OpenAPI package):

```xml
<!-- Directory.Packages.props -->
<ItemGroup>
	<GlobalPackageReference Include="Purview.OpenAPI.XmlComments" Version="<latest>" />
</ItemGroup>
```

It is a development dependency: nothing reaches the build output, and nothing flows to packages the project produces.
Check the latest version on nuget.org rather than guessing one.

## Removing the generator workaround

Delete whichever form the repository used - they all disable every XML description:

```xml
<!-- a target -->
<Target Name="..." BeforeTargets="CoreCompile">
	<ItemGroup>
		<Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'Microsoft.AspNetCore.OpenApi.SourceGenerators'" />
	</ItemGroup>
</Target>

<!-- or an item -->
<Analyzer Remove="$(PkgMicrosoft_AspNetCore_OpenApi)\analyzers\dotnet\cs\Microsoft.AspNetCore.OpenApi.SourceGenerators.dll" />
```

Search for it in the API project and every `Directory.Build.props`/`.targets` above it:

```bash
grep -rn "OpenApi.SourceGenerators" --include=*.csproj --include=Directory.Build.* .
```

## Excluding IDs

Needed only for an ID that is in exactly **one** referenced project and in the API itself (`POAX001`); IDs in two or
more referenced projects are handled with no configuration.

```xml
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Purview.Results.GenerateResultAttribute" />
</ItemGroup>
```

- Use the documentation ID exactly as `POAX001`, the script or the exception prints it. Method IDs may be written with
  or without their `~ReturnType` suffix.
- Put it in the API project. With several API projects that share the same injected types, put it in a
  `Directory.Build.props` that covers them; the item is ignored by projects without the OpenAPI package.
- An exclude removes the ID from the **referenced** documentation only; the API's own copy keeps its description.

## Warnings

`POAX001` is an ordinary MSBuild warning:

```xml
<PropertyGroup>
	<!-- fail the build, for example in CI -->
	<MSBuildWarningsAsErrors>$(MSBuildWarningsAsErrors);POAX001</MSBuildWarningsAsErrors>
	<!-- or silence it, for an overlap with a type that is internal in the API -->
	<MSBuildWarningsAsMessages>$(MSBuildWarningsAsMessages);POAX001</MSBuildWarningsAsMessages>
</PropertyGroup>
```

## Turning it off

```xml
<PropertyGroup>
	<PurviewOpenAPIXmlCommentDeduplication>false</PurviewOpenAPIXmlCommentDeduplication>
</PropertyGroup>
```

Do this once the generator de-duplicates its own cache (dotnet/aspnetcore#67192), then remove the package.

## Checking it is active

```bash
dotnet msbuild path/to/Api/Api.csproj -getProperty:PurviewOpenAPIXmlCommentDeduplication -getProperty:PurviewOpenAPIXmlCommentsTaskAssembly
```

`true` and a path to `Purview.OpenAPI.XmlComments.dll` mean it is imported. A normal-verbosity build (`-v:n`) logs each
removal: `OpenAPI XML comments: removed 'T:...' from Lib.A, Lib.B (duplicate).`
