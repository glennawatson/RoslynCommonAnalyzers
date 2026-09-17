// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyClosed = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2337ClosedHierarchyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the closed-hierarchy rule (SST2337, opt-in).</summary>
public class Sst2337ClosedHierarchyAnalyzerUnitTest
{
    /// <summary>Verifies C# 15 does not require a modifier unsupported by the .NET 10 references.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task Net10WithoutClosedTypeAttributeIsCleanAsync() =>
        RunAsync(
            """
            internal abstract class Base;
            internal sealed class First : Base;
            internal sealed class Second : Base;
            """,
            referenceAssemblies: AnalyzerFrameworks.Net100);

    /// <summary>Verifies a source-provided attribute enables the rule without .NET 11 references.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task Net10WithClosedTypeAttributeIsReportedAsync() =>
        RunAsync(
            """
            internal abstract class {|SST2337:Base|};
            internal sealed class First : Base;
            internal sealed class Second : Base;

            namespace System.Runtime.CompilerServices
            {
                internal sealed class IsClosedTypeAttribute : System.Attribute;
            }
            """,
            referenceAssemblies: AnalyzerFrameworks.Net100);

    /// <summary>Verifies the source-provided attribute makes the closed modifier compile on .NET 10.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task Net10WithClosedTypeAttributeAlreadyClosedIsCleanAsync() =>
        RunAsync(
            """
            internal closed class Base;
            internal sealed class First : Base;
            internal sealed class Second : Base;

            namespace System.Runtime.CompilerServices
            {
                internal sealed class IsClosedTypeAttribute : System.Attribute;
            }
            """,
            referenceAssemblies: AnalyzerFrameworks.Net100);

    /// <summary>Verifies an assembly-internal abstract base with two descendants is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalBaseWithTwoDescendantsReportedAsync() =>
        RunAsync(
            """
            internal abstract class {|SST2337:GateState|}
            {
            }

            internal sealed class Shut : GateState
            {
            }

            internal sealed class Open : GateState
            {
            }
            """);

    /// <summary>Verifies an externally visible base is not reported, because closing it would break callers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicBaseIsCleanAsync() =>
        RunAsync(
            """
            public abstract class GateState
            {
            }

            public sealed class Shut : GateState
            {
            }

            public sealed class Open : GateState
            {
            }
            """);

    /// <summary>Verifies a base with a single descendant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleDescendantIsCleanAsync() =>
        RunAsync(
            """
            internal abstract class GateState
            {
            }

            internal sealed class Shut : GateState
            {
            }
            """);

    /// <summary>Verifies a concrete base is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConcreteBaseIsCleanAsync() =>
        RunAsync(
            """
            internal class GateState
            {
            }

            internal sealed class Shut : GateState
            {
            }

            internal sealed class Open : GateState
            {
            }
            """);

    /// <summary>Verifies a hierarchy that already carries the modifier is not reported again.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AlreadyClosedIsCleanAsync() =>
        RunAsync(
            """
            internal closed class GateState
            {
            }

            internal sealed class Shut : GateState
            {
            }

            internal sealed class Open : GateState
            {
            }
            """);

    /// <summary>Verifies nothing is reported below C# 15, where the modifier does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BelowCSharp15IsCleanAsync() =>
        RunAsync(
            """
            internal abstract class GateState
            {
            }

            internal sealed class Shut : GateState
            {
            }

            internal sealed class Open : GateState
            {
            }
            """,
            LanguageVersion.CSharp13);

    /// <summary>Runs the analyzer verifier at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <param name="referenceAssemblies">The framework references; defaults to .NET 11 for closed-type support.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        ReferenceAssemblies? referenceAssemblies = null)
    {
        var test = new VerifyClosed.Test { ReferenceAssemblies = referenceAssemblies ?? DotNet11ReferenceAssemblies.Net110, TestCode = source };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
