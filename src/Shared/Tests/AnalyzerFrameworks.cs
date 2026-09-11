// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

namespace RoslynCommon.Analyzers.Tests;

/// <summary>
/// The target frameworks an analyzed compilation can be checked against. These are the frameworks of
/// the code under analysis, not of the test host: an analyzer is netstandard2.0 and loads into the
/// compiler, so what varies here is which APIs the analyzed project can actually call.
/// </summary>
internal static class AnalyzerFrameworks
{
    /// <summary>Gets the .NET Framework 4.6.2 reference assemblies.</summary>
    public static ReferenceAssemblies Net462 { get; } = ReferenceAssemblies.NetFramework.Net462.Default;

    /// <summary>Gets the .NET Framework 4.7.2 reference assemblies.</summary>
    public static ReferenceAssemblies Net472 { get; } = ReferenceAssemblies.NetFramework.Net472.Default;

    /// <summary>Gets the .NET Framework 4.8 reference assemblies, whose surface 4.8.1 matches for analysis.</summary>
    public static ReferenceAssemblies Net48 { get; } = ReferenceAssemblies.NetFramework.Net48.Default;

    /// <summary>Gets the netstandard2.0 reference assemblies, the surface the analyzers are built against.</summary>
    public static ReferenceAssemblies NetStandard20 { get; } = ReferenceAssemblies.NetStandard.NetStandard20;

    /// <summary>Gets the .NET 8 reference assemblies.</summary>
    public static ReferenceAssemblies Net80 { get; } = ReferenceAssemblies.Net.Net80;

    /// <summary>Gets the .NET 9 reference assemblies.</summary>
    public static ReferenceAssemblies Net90 { get; } = ReferenceAssemblies.Net.Net90;

    /// <summary>Gets the .NET 10 reference assemblies.</summary>
    public static ReferenceAssemblies Net100 { get; } = ReferenceAssemblies.Net.Net100;

    /// <summary>Gets the .NET 11 reference assemblies.</summary>
    public static ReferenceAssemblies Net110 { get; } = DotNet11ReferenceAssemblies.Net110;

    /// <summary>Every framework, for a rule whose verdict must not depend on the target.</summary>
    /// <returns>The framework name and its reference assemblies.</returns>
    internal static IEnumerable<(string Name, ReferenceAssemblies Assemblies)> All()
    {
        yield return ("net462", Net462);
        yield return ("net472", Net472);
        yield return ("net48", Net48);
        yield return ("netstandard2.0", NetStandard20);
        yield return ("net8.0", Net80);
        yield return ("net9.0", Net90);
        yield return ("net10.0", Net100);
        yield return ("net11.0", Net110);
    }

    /// <summary>The frameworks that predate .NET Core, where much of the modern BCL is absent.</summary>
    /// <returns>The framework name and its reference assemblies.</returns>
    internal static IEnumerable<(string Name, ReferenceAssemblies Assemblies)> NetFrameworkOnly()
    {
        yield return ("net462", Net462);
        yield return ("net472", Net472);
        yield return ("net48", Net48);
    }

    /// <summary>The modern runtimes, where the newer BCL surface exists.</summary>
    /// <returns>The framework name and its reference assemblies.</returns>
    internal static IEnumerable<(string Name, ReferenceAssemblies Assemblies)> ModernOnly()
    {
        yield return ("net8.0", Net80);
        yield return ("net9.0", Net90);
        yield return ("net10.0", Net100);
        yield return ("net11.0", Net110);
    }
}
