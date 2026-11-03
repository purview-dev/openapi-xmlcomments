# Getting Started

`Purview.OpenAPI.XmlComments` keeps the `Microsoft.AspNetCore.OpenApi` XML-comment source generator working when the
same documentation ID comes from more than one assembly. Without it, `/openapi/v1.json` answers `500` with:

```text
System.ArgumentException: An item with the same key has already been added. Key: T:Some.Namespace.SomeType
   at ...XmlCommentCache.GenerateCacheEntries()
```

## Do you need it?

You do if all of these are true:

- the API project references `Microsoft.AspNetCore.OpenApi` and its XML-comment generator is on (the default);
- the API references projects that generate XML documentation (`GenerateDocumentationFile`);
- a documented type with the same full name is compiled into more than one of them - typically an attribute or polyfill
  a source generator injects into every assembly, such as `Purview.Results.GenerateResultAttribute`.

The key in the exception names the type - but only the first collision. To list every colliding ID and where each
copy comes from, run the diagnostic in [Troubleshooting](Troubleshooting.md); AI agents get the same procedure from
the [`purview-openapi-xmlcomments-duplicates`](Agent-Skills.md) skill.

## Install

```xml
<!-- the project that calls AddOpenApi() -->
<PackageReference Include="Purview.OpenAPI.XmlComments" Version="1.0.0" PrivateAssets="all" />
```

or once for every project in the repository (it does nothing where there is no OpenAPI package):

```xml
<!-- Directory.Packages.props -->
<GlobalPackageReference Include="Purview.OpenAPI.XmlComments" Version="1.0.0" />
```

Then remove any workaround that turns the generator off, such as

```xml
<Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'Microsoft.AspNetCore.OpenApi.SourceGenerators'" />
```

## Build

Build as usual. If the injected type is carried by only **one** referenced project, the build warns
[`POAX001`](Diagnostics.md) and names the line to add:

```xml
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Purview.Results.GenerateResultAttribute" />
</ItemGroup>
```

Next: [How It Works](How-It-Works.md), [Configuration](Configuration.md) and, if anything still fails,
[Troubleshooting](Troubleshooting.md).
