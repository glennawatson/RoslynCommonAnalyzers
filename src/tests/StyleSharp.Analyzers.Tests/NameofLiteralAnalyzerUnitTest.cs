// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNameofLiteral = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1463NameofLiteralAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1463NameofLiteralAnalyzer"/>.</summary>
public class NameofLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a name-shaped argument matching a property is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SymbolNameLiteralIsReportedAsync()
        => await VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Count { get; set; }

                public void M() => Notify({|SST1463:"Count"|});

                private static void Notify(string propertyName)
                {
                }
            }
            """);

    /// <summary>Verifies ordinary message strings are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonNameParameterIsCleanAsync()
        => await VerifyNameofLiteral.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Count { get; set; }

                public void M() => Log("Count");

                private static void Log(string message)
                {
                }
            }
            """);
}
