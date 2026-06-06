// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPrimaryCtor = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PrimaryConstructorParameterMutationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1425 (do not reassign captured primary-constructor parameters).</summary>
public class PrimaryConstructorParameterMutationAnalyzerUnitTest
{
    /// <summary>Verifies assignment and increment on a class primary-constructor parameter are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassPrimaryConstructorMutationReportedAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public class Counter(int count)
            {
                public void Reset()
                {
                    {|SST1425:count|} = 0;
                    {|SST1425:count|}++;
                }
            }
            """);

    /// <summary>Verifies <c>ref</c>/<c>out</c> passing of a struct primary-constructor parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructPrimaryConstructorRefOutReportedAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public struct Counter(int count)
            {
                public void Reset()
                {
                    Bump(ref {|SST1425:count|});
                    Set(out {|SST1425:count|});
                }

                private static void Bump(ref int value) => value++;

                private static void Set(out int value) => value = 0;
            }
            """);

    /// <summary>Verifies record primary-constructor parameters are not reported because they are properties, not captured mutable parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordPrimaryConstructorMutationIsCleanAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public record Counter(int Count)
            {
                public Counter Reset() => this with { Count = 0 };
            }

            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);

    /// <summary>Verifies ordinary method parameters are not reported by SST1425.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinaryMethodParameterIsCleanAsync()
        => await VerifyPrimaryCtor.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public void Reset(int count)
                {
                    count = 0;
                    count++;
                }
            }
            """);
}
