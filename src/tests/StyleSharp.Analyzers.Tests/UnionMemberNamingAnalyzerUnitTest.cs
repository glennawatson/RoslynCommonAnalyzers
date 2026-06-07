// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.UnionMemberNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1315 (union member names should match the configured casing).</summary>
public class UnionMemberNamingAnalyzerUnitTest
{
    /// <summary>A stand-in <c>IUnion</c> marker so the rule activates (real C# 15 unions are not yet in Roslyn).</summary>
    private const string Marker = """
                                  namespace System.Runtime.CompilerServices { public interface IUnion { } }
                                  """;

    /// <summary>A union base type implementing the marker.</summary>
    private const string UnionBase = """
                                     public abstract class Shape : System.Runtime.CompilerServices.IUnion { }
                                     """;

    /// <summary>Verifies a PascalCase union case produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
        => await Verify.VerifyAnalyzerAsync($$"""{{Marker}}{{UnionBase}}public sealed class Circle : Shape { }""");

    /// <summary>Verifies a lower-case union case is reported and renamed to PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnionCaseAsync()
        => await Verify.VerifyCodeFixAsync(
            $$"""{{Marker}}{{UnionBase}}public sealed class {|SST1315:circle|} : Shape { }""",
            $$"""{{Marker}}{{UnionBase}}public sealed class Circle : Shape { }""");

    /// <summary>Verifies the rule does not fire when no IUnion marker is present in the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoMarkerNoDiagnosticAsync()
        => await Verify.VerifyAnalyzerAsync("public sealed class circle { }");
}
