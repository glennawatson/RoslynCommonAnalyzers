// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1227PreferDedicatedCallAnalyzer,
    PerformanceSharp.Analyzers.Psh1227PreferDedicatedCallCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1227PreferDedicatedCallAnalyzer"/> (PSH1227 use the purpose-built call).</summary>
public class PreferDedicatedCallAnalyzerUnitTest
{
    /// <summary>Verifies an ordinal string.Compare is reported and rewritten to string.CompareOrdinal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinalCompareIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string a, string b) => {|PSH1227:string.Compare(a, b, StringComparison.Ordinal)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string a, string b) => string.CompareOrdinal(a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an always-false Debug.Assert with a message is reported and rewritten to Debug.Fail.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlwaysFalseAssertIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string message)
                                  {
                                      {|PSH1227:Debug.Assert(false, message)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics;

                                   public class C
                                   {
                                       public void M(string message)
                                       {
                                           Debug.Fail(message);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the detail-message Debug.Assert overload is rewritten to the matching Debug.Fail overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlwaysFalseAssertWithDetailIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string message, string detail)
                                  {
                                      {|PSH1227:Debug.Assert(false, message, detail)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics;

                                   public class C
                                   {
                                       public void M(string message, string detail)
                                       {
                                           Debug.Fail(message, detail);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a case-insensitive ordinal comparison is not reported: CompareOrdinal is case-sensitive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OrdinalIgnoreCaseCompareIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public int M(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
            """);

    /// <summary>Verifies an ordinal Compare tested against zero is left to the equality-versus-ordering rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OrdinalCompareAgainstZeroIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public bool M(string a, string b) => string.Compare(a, b, StringComparison.Ordinal) == 0;
            }
            """);

    /// <summary>Verifies a two-argument string.Compare without a StringComparison is not this rule's shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CultureCompareIsCleanAsync() =>
        VerifyCleanAsync(
            """
            public class C
            {
                public int M(string a, string b) => string.Compare(a, b);
            }
            """);

    /// <summary>Verifies an assertion of a real condition is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RealConditionAssertIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(bool condition, string message) => Debug.Assert(condition, message);
            }
            """);

    /// <summary>Verifies a message-less Debug.Assert(false) is not reported: Debug.Fail needs a message.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MessagelessAssertIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M() => Debug.Assert(false);
            }
            """);

    /// <summary>Verifies syntax and symbol near misses do not suggest a dedicated call.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Compare(a, b, comparison)")]
    [Arguments("string.Compare(a, b, false)")]
    [Arguments("string.Compare(a, b, Missing.Ordinal)")]
    [Arguments("other.Compare(a, b, 4)")]
    [Arguments("other->Compare(a, b, 4)")]
    [Arguments("C.Compare(a, b, 4)")]
    [Arguments("string.Compare<int>(a, b, 4)")]
    [Arguments("Compare(a, b, 4)")]
    [Arguments("Missing.Assert(false, a)")]
    [Arguments("other.Assert(false, a)")]
    [Arguments("C.Assert(false, a)")]
    [Arguments("0 != ((string.Compare(a, b, System.StringComparison.Ordinal)))")]
    [Arguments("((string.Compare(a, b, System.StringComparison.Ordinal))) != 0")]
    public async Task UnrelatedAndUnresolvedCallsAreCleanAsync(string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       class C
                       {
                           public static int Compare(string a, string b, int mode) => 0;
                           public static int Assert(bool condition, string message) => 0;
                           object M(Other other, string a, string b, System.StringComparison comparison) => {{expression}};
                       }
                       class Other
                       {
                           public int Compare(string a, string b, int mode) => 0;
                           public int Assert(bool condition, string message) => 0;
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies nonzero and ordering comparisons retain the dedicated ordinal suggestion.</summary>
    /// <param name="suffix">The surrounding comparison expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("== 1")]
    [Arguments("!= 1")]
    [Arguments("> 0")]
    [Arguments("== (0)")]
    [Arguments("== 0L")]
    [Arguments("== '\\0'")]
    public Task OrdinalCompareOutsideLiteralZeroEqualityIsReportedAsync(string suffix)
    {
        var source = $$"""
                       class C { bool M(string a, string b) => {|PSH1227:string.Compare(a, b, System.StringComparison.Ordinal)|} {{suffix}}; }
                       """;
        var fixedSource = $$"""
                            class C { bool M(string a, string b) => string.CompareOrdinal(a, b) {{suffix}}; }
                            """;
        return VerifyAsync(source, fixedSource);
    }

    /// <summary>Verifies the debug replacement must accept exactly the original message arguments.</summary>
    /// <param name="members">The alternate debug API surface.</param>
    /// <param name="arguments">The assertion arguments.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static void Assert(bool condition, string message) { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public void Fail(string message) { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public static object Fail;", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public static void Fail(object message) { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public static void Fail(string message, object detail) { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public static void Fail() { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message) { } public static void Fail(string message, string detail) { }", "false, message")]
    [Arguments("public static void Assert(bool condition, string message, string detail) { } public static void Fail(string message) { }", "false, message, message")]
    [Arguments("public static void Assert(bool condition, string message, object detail) { } public static void Fail(string message, string detail) { }", "false, message, message")]
    [Arguments("public static void Assert(object condition, string message) { } public static void Fail(string message) { }", "false, message")]
    [Arguments("public static void Assert(params object[] values) { } public static void Fail(string message) { }", "false, message")]
    public async Task IncompatibleDebugSurfaceIsCleanAsync(string members, string arguments, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Diagnostics { public class Debug { {{members}} } }
                       class C { void M(string message) => System.Diagnostics.Debug.Assert({{arguments}}); }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies missing and incompatible ordinal comparison APIs disable the rule.</summary>
    /// <param name="members">The members declared on the framework string type.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public static int CompareOrdinal;")]
    [Arguments("public int CompareOrdinal(string a, string b) => 0;")]
    [Arguments("public static int CompareOrdinal(string a) => 0;")]
    [Arguments("public static int CompareOrdinal(int a, string b) => 0;")]
    [Arguments("public static int CompareOrdinal(string a, int b) => 0;")]
    public async Task MissingOrdinalComparisonApiIsCleanAsync(string members, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System
                       {
                           public class Object { }
                           public class ValueType { }
                           public struct Int32 { }
                           public class String { {{members}} }
                       }
                       class C { int M(string a, string b) => string.Compare(a, b, 4); }
                       """;
        var diagnostics = await AnalyzeAsync(source, [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies similarly named comparison overloads retain their distinct parameter contracts.</summary>
    /// <param name="parameters">The alternate Compare parameter list.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("params string[] values", "a, b, a")]
    [Arguments("object a, string b, int mode", "a, b, 4")]
    [Arguments("string a, object b, int mode", "a, b, 4")]
    [Arguments("string a, string b, int mode", "a, b, 4")]
    public async Task AlternateStringCompareSignaturesAreCleanAsync(string parameters, string arguments, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System
                       {
                           public class Object { }
                           public class ValueType { }
                           public struct Int32 { }
                           public class Array { }
                           public class String
                           {
                               public static int CompareOrdinal(string a, string b) => 0;
                               public static int Compare({{parameters}}) => 0;
                           }
                       }
                       class C { int M(string a, string b) => string.Compare({{arguments}}); }
                       """;
        var diagnostics = await AnalyzeAsync(source, [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a compilation without Debug remains clean on repeated assertion candidates.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingDebugTypeIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = "class C { void M() { Debug.Assert(false, \"message\"); Debug.Assert(false, \"message\"); } }";
        var diagnostics = await AnalyzeAsync(Source, [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs the analyzer against incomplete or alternate framework sources.</summary>
    /// <param name="source">The compilation source.</param>
    /// <param name="references">Cached references, or none for a missing framework.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create("DedicatedCalls", [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)], references, new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1227PreferDedicatedCallAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyCleanAsync(string source) => VerifyAsync(source, source);
}
