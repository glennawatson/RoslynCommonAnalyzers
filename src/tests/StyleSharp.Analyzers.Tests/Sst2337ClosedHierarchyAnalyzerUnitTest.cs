// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;
using VerifyClosed = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2337ClosedHierarchyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the closed-hierarchy rule (SST2337, opt-in).</summary>
public class Sst2337ClosedHierarchyAnalyzerUnitTest
{
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
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyClosed.Test
        {
            // The 'closed' modifier binds against IsClosedTypeAttribute, which only the .NET 11
            // reference assemblies carry.
            ReferenceAssemblies = DotNet11ReferenceAssemblies.Net110,
            TestCode = source,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
