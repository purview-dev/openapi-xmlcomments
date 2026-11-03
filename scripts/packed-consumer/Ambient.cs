namespace Ambient;

/// <summary>An attribute a source generator would inject into every assembly.</summary>
/// <remarks>
/// Compiled into the API and both libraries, documented and public, which is exactly what makes the OpenAPI
/// XML-comment cache add <c>T:Ambient.InjectedAttribute</c> three times without the package.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InjectedAttribute : Attribute { }
