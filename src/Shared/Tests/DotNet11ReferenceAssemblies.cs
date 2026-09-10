// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

namespace RoslynCommon.Analyzers.Tests;

/// <summary>
/// The .NET 11 reference assemblies, for rules that only fire when an API introduced in that
/// version resolves in the analyzed compilation.
/// </summary>
/// <remarks><c>ReferenceAssemblies.Net</c> stops at .NET 10; see issue #62.</remarks>
internal static class DotNet11ReferenceAssemblies
{
    /// <summary>The reference-assembly package that carries the .NET 11 surface.</summary>
    private const string ReferencePackageId = "Microsoft.NETCore.App.Ref";

    /// <summary>The pinned .NET 11 reference-assembly version.</summary>
    private const string ReferencePackageVersion = "11.0.0-rc.1.26425.128";

    /// <summary>Gets the .NET 11 reference assemblies.</summary>
    public static ReferenceAssemblies Net110 { get; } = new(
        "net11.0",
        new PackageIdentity(ReferencePackageId, ReferencePackageVersion),
        Path.Combine("ref", "net11.0"));
}
