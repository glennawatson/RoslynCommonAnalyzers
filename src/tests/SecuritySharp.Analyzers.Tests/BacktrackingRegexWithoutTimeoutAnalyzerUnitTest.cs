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

using AnalyzeRegex = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1509BacktrackingRegexWithoutTimeoutAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1509 (a backtracking-prone regex must not run with no match timeout).</summary>
public class BacktrackingRegexWithoutTimeoutAnalyzerUnitTest
{
    /// <summary>Cached primitive references for framework-surface tests with minimal regex stubs.</summary>
    private static readonly ImmutableArray<MetadataReference> PrimitiveReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies a nested-quantifier constant pattern to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedQuantifierToConstructorReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex({|SES1509:"(a+)+"|});
            }
            """);

    /// <summary>Verifies a top-level alternation inside a repeated group is reported on a static call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverlappingAlternationToStaticIsMatchReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input) => Regex.IsMatch(input, {|SES1509:"(a|aa)+"|});
            }
            """);

    /// <summary>Verifies a nested-quantifier constant pattern in a <c>[GeneratedRegex]</c> attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedQuantifierInGeneratedRegexReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                [GeneratedRegex({|SES1509:"([a-z]+)*"|})]
                public Regex M() => null!;
            }
            """);

    /// <summary>Verifies a <c>(.*)*</c> star-on-star constant pattern is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StarOnStarConstructorReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex({|SES1509:"(.*)*"|});
            }
            """);

    /// <summary>Verifies the pattern passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedPatternArgumentReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex(pattern: {|SES1509:@"(\d+)*"|});
            }
            """);

    /// <summary>Verifies a nested-quantifier constant pattern to static <c>Regex.Replace</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticReplaceReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public string M(string input) => Regex.Replace(input, {|SES1509:"(a*)*"|}, "x");
            }
            """);

    /// <summary>Verifies an unbounded <c>{n,}</c> outer quantifier over a repeated group is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnboundedBraceOuterQuantifierReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex({|SES1509:"(a+){2,}"|});
            }
            """);

    /// <summary>Verifies the rule works on the .NET Framework, where a match timeout matters just as much.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedQuantifierOnNetFrameworkReportedAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              public class C
                              {
                                  public Regex M() => new Regex({|SES1509:"(a+)+"|});
                              }
                              """;

        var test = new AnalyzeRegex.Test { ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a supplied match timeout keeps the constructor call clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MatchTimeoutSuppressesAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+)+", RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            """);

    /// <summary>Verifies the non-backtracking engine keeps the constructor call clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonBacktrackingOptionSuppressesAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+)+", RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies <c>NonBacktracking</c> combined with other flags is still recognized as safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonBacktrackingCombinedWithFlagsSuppressesAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+)+", RegexOptions.Compiled | RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies a benign constant pattern with no nested quantifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BenignConstantPatternIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("^[a-z]+$");
            }
            """);

    /// <summary>Verifies a repeated group whose body neither repeats nor alternates is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RepeatedGroupWithoutInnerRepetitionIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(abc)+");
            }
            """);

    /// <summary>Verifies a fixed-width bounded inner quantifier under an outer quantifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundedInnerQuantifierIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex(@"(\d{4})+");
            }
            """);

    /// <summary>Verifies a bounded outer quantifier over a repeated group is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundedOuterQuantifierIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+){2,3}");
            }
            """);

    /// <summary>Verifies a non-constant pattern is not reported (that shape is a separate injection concern).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantPatternIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M(string userPattern) => new Regex(userPattern);
            }
            """);

    /// <summary>Verifies a benign constant pattern to a static call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticBenignPatternIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input) => Regex.IsMatch(input, @"^\d+$");
            }
            """);

    /// <summary>Verifies a <c>[GeneratedRegex]</c> with a match-timeout in milliseconds is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GeneratedRegexWithTimeoutIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                [GeneratedRegex("(a+)+", RegexOptions.None, 1000)]
                public Regex M() => null!;
            }
            """);

    /// <summary>Verifies a same-named regex type from another namespace is not reported (only the real type binds).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedNonSystemRegexTypeIsCleanAsync() =>
        VerifyNet90Async(
            """
            public sealed class Regex
            {
                public Regex(string pattern)
                {
                }
            }

            public class C
            {
                public Regex M() => new Regex("(a+)+");
            }
            """);

    /// <summary>Verifies escapes, character classes, grouping, and incomplete quantifiers retain their current interpretation.</summary>
    /// <param name="pattern">The compile-time regex pattern.</param>
    /// <param name="reported">Whether the pattern contains an overlapping unbounded repetition.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(@"\(a+\)\+", false)]
    [Arguments("(a+", false)]
    [Arguments("(a+)", false)]
    [Arguments("[^]+]", false)]
    [Arguments(@"[\]()+]", false)]
    [Arguments("[", false)]
    [Arguments("[^", false)]
    [Arguments("[]", false)]
    [Arguments("[abc", false)]
    [Arguments("([abc", false)]
    [Arguments("((ab)c)+", false)]
    [Arguments("((a|b)c)+", false)]
    [Arguments("((a+))", false)]
    [Arguments("((a+)+)", true)]
    [Arguments(@"(\+)+", false)]
    [Arguments("([+])+", false)]
    [Arguments("([a]+)+", true)]
    [Arguments("(a{2,})+", true)]
    [Arguments("(a{2,3})+", false)]
    [Arguments("(a+){", false)]
    [Arguments("(a+){x", false)]
    [Arguments("(a+){12", false)]
    [Arguments("(a+){12,", false)]
    [Arguments("(a+){12,x}", false)]
    [Arguments("(a+){12,}", true)]
    [Arguments("(a+){12}", false)]
    [Arguments("(a+)?", false)]
    [Arguments("", false)]
    public Task PatternStructureDeterminesReportAsync(string pattern, bool reported)
    {
        var expression = reported ? $"{{|SES1509:@\"{pattern}\"|}}" : $"@\"{pattern}\"";
        return VerifyNet90Async($"class C {{ object M() => new System.Text.RegularExpressions.Regex({expression}); }}");
    }

    /// <summary>Verifies every guarded static method reports the pattern argument.</summary>
    /// <param name="method">The static method name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Match")]
    [Arguments("Matches")]
    [Arguments("Split")]
    [Arguments("Count")]
    public Task StaticRegexMethodsReportNamedPatternAsync(string method) =>
        VerifyNet90Async($"class C {{ object M() => System.Text.RegularExpressions.Regex.{method}(pattern: {{|SES1509:\"(a+)+\"|}}, input: \"text\"); }}");

    /// <summary>Verifies named options and timeout arguments bound the regex cost when supplied.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedAndUnknownOptionsRespectBacktrackingGuardAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Text.RegularExpressions;
            class C
            {
                Regex M(RegexOptions options) => new Regex("(a+)+", options);
                Regex N() => new Regex(options: RegexOptions.IgnoreCase, pattern: {|SES1509:"(a+)+"|});
                Regex Safe() => new Regex(options: RegexOptions.NonBacktracking, pattern: "(a+)+");
                bool Timed() => Regex.IsMatch("text", "(a+)+", RegexOptions.None, TimeSpan.FromSeconds(1));
                bool Unknown(RegexOptions options) => Regex.IsMatch("text", "(a+)+", options);
                Regex NullPattern() => new Regex(null);
            }
            """);

    /// <summary>Verifies attribute qualification and named constructor arguments resolve to the same guarded pattern.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QualifiedAttributesAndNamedArgumentsAreAnalyzedAsync() =>
        VerifyNet90Async(
            """
            using Rx = System.Text.RegularExpressions;
            class C
            {
                [System.Text.RegularExpressions.GeneratedRegexAttribute(pattern: {|SES1509:"(a+)+"|}, options: Rx.RegexOptions.None)]
                object M() => null;
                [Rx::GeneratedRegex(pattern: {|SES1509:"(a+)+"|})]
                object N() => null;
                [Rx.GeneratedRegex(options: Rx.RegexOptions.NonBacktracking, pattern: "(a+)+")]
                object Safe() => null;
                [Rx.GeneratedRegex(matchTimeoutMilliseconds: 1, options: Rx.RegexOptions.None, pattern: "(a+)+")]
                object Timed() => null;
            }
            """);

    /// <summary>Verifies unrelated syntax and unresolved overloads cannot report a regex vulnerability.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnboundAndUnrelatedCandidatesAreCleanAsync()
    {
        var test = new AnalyzeRegex.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = """
                       using System;
                       using System.Text.RegularExpressions;
                       [Obsolete("reason")]
                       class C
                       {
                           [GeneratedRegex]
                           object A() => new object();
                           [GeneratedRegex(123)]
                           object B() => new Regex(123);
                           object M() => new Regex();
                           object N() => new C("(a+)+");
                           bool P() => Regex.IsMatch(1, 2);
                           bool Q(Regex regex) => regex.IsMatch("(a+)+", 1);
                           bool R() => IsMatch("text", "(a+)+");
                           bool IsMatch(string text, string pattern) => false;
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies guarded names from unrelated types and attributes are ignored after binding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedStaticMethodAndAttributeAreCleanAsync() =>
        VerifyNet90Async(
            """
            class GeneratedRegexAttribute : System.Attribute
            {
                public GeneratedRegexAttribute(string pattern) { }
            }
            class C
            {
                [GeneratedRegex("(a+)+")]
                public bool M() => C.IsMatch("text", "(a+)+");
                public static bool IsMatch(string input, string pattern) => false;
            }
            """);

    /// <summary>Verifies missing framework types and missing pattern parameters stop analysis safely.</summary>
    /// <param name="declarations">The available regex framework surface.</param>
    /// <param name="use">The guarded candidate expression or attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "class C { object M() => new Regex(\"(a+)+\"); }")]
    [Arguments("public class Regex { public Regex(string pattern) { } }", "class C { object M() => new Regex(\"(a+)+\"); }")]
    [Arguments("", "class C { bool M() => Regex.IsMatch(\"text\", \"(a+)+\"); }")]
    [Arguments("", "[GeneratedRegex(\"(a+)+\")] class C { }")]
    [Arguments("public enum RegexOptions { None } public class Regex { }", "[GeneratedRegex(\"(a+)+\")] class C { }")]
    [Arguments("public enum RegexOptions { None } public class Regex { public Regex(string input) { } }", "class C { object M() => new Regex(\"(a+)+\"); }")]
    [Arguments(
        "public enum RegexOptions : long { None } public class Regex { public Regex(string pattern, RegexOptions options) { } }",
        "class C { object M() => new Regex(\"(a+)+\", RegexOptions.None); }")]
    [Arguments(
        "public enum RegexOptions { None } public class Regex { public Regex(string pattern = null, RegexOptions options = RegexOptions.None) { } }",
        "class C { object M() => new Regex(options: RegexOptions.None); }")]
    [Arguments(
        "public enum RegexOptions { None } public class Regex { } public class GeneratedRegexAttribute : System.Attribute { "
        + "public GeneratedRegexAttribute(string pattern = null, RegexOptions options = RegexOptions.None) { } public string Label { get; set; } }",
        "[GeneratedRegex(Label = \"label\")] class C { }")]
    [Arguments(
        "public enum RegexOptions { None } public class Regex { } public class GeneratedRegexAttribute : System.Attribute { "
        + "public GeneratedRegexAttribute(string pattern = null, RegexOptions options = RegexOptions.None) { } }",
        "[GeneratedRegex(options: RegexOptions.None)] class C { }")]
    public async Task IncompleteFrameworkSurfaceIsCleanAsync(string declarations, string use)
    {
        var tree = CSharpSyntaxTree.ParseText($"using System.Text.RegularExpressions; namespace System.Text.RegularExpressions {{ {declarations} }} {use}");
        var compilation = CSharpCompilation.Create("RegexSurface", [tree], PrimitiveReferences, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Ses1509BacktrackingRegexWithoutTimeoutAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await TUnit.Assertions.Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeRegex.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
