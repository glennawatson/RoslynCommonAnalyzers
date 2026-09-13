// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2444InvalidRegexPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2444 (an invalid regular-expression pattern).</summary>
public class InvalidRegexPatternAnalyzerUnitTest
{
    /// <summary>Core types without the regular-expression assembly, shared by missing-framework tests.</summary>
    private static readonly MetadataReference[] CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies an unterminated character class in a construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnterminatedSetInConstructionIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:"[a-z"|});
            }
            """);

    /// <summary>Verifies an unclosed group in a static query is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnclosedGroupInStaticQueryIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool Match(string s) => Regex.IsMatch(s, {|SST2444:"(unclosed"|});
            }
            """);

    /// <summary>Verifies a reversed quantifier is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReversedQuantifierIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:"a{2,1}"|});
            }
            """);

    /// <summary>Verifies a backreference to a group that is never defined is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndefinedBackreferenceIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public string Clean(string s) => Regex.Replace(s, {|SST2444:@"\1x"|}, "y");
            }
            """);

    /// <summary>Verifies the non-backtracking option is kept, so a backreference under it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BackreferenceUnderNonBacktrackingIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:@"(a)\1"|}, RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies a valid pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("[a-z]+");
            }
            """);

    /// <summary>Verifies a valid backreference in the default engine is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidBackreferenceIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex(@"(a)\1");
            }
            """);

    /// <summary>Verifies the compile option is stripped, so a valid compiled pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompiledOptionIsStrippedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("[a-z]+", RegexOptions.Compiled);
            }
            """);

    /// <summary>Verifies whitespace ignored under the pattern-whitespace option keeps a spaced pattern valid.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IgnorePatternWhitespaceKeepsSpacedPatternValidAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("a b c", RegexOptions.IgnorePatternWhitespace);
            }
            """);

    /// <summary>Verifies a non-constant pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build(string pattern) => new Regex(pattern);
            }
            """);

    /// <summary>Verifies a same-named method on another type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMethodOnOtherTypeIsCleanAsync() =>
        VerifyAsync(
            """
            public static class Helper
            {
                public static bool IsMatch(string s, string pattern) => false;
            }

            public class C
            {
                public bool Match(string s) => Helper.IsMatch(s, "(unclosed");
            }
            """);

    /// <summary>Verifies an instance query with no pattern argument is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceQueryWithoutPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool Match(Regex regex, string s) => regex.IsMatch(s);
            }
            """);

    /// <summary>Verifies failed engine resolution remains silent for repeated syntactic candidates.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingRegexTypeIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public class Regex
            {
                public Regex(string pattern) { }
                public static bool IsMatch(string input, string pattern) => false;
            }
            public class C
            {
                public void M()
                {
                    _ = new Regex("[");
                    _ = Regex.IsMatch("input", "[");
                }
            }
            """);
        var compilation = CSharpCompilation.Create(
            nameof(MissingRegexTypeIsCleanAsync),
            [tree],
            CoreReferences,
            new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new Sst2444InvalidRegexPatternAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a real engine stub can bind without an options enum.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RegexWithoutOptionsTypeStillValidatesAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            namespace System.Text.RegularExpressions
            {
                public class Regex
                {
                    public Regex(string pattern, int ignored) { }
                }
            }
            public class C
            {
                public object M() => new System.Text.RegularExpressions.Regex("[", 0);
            }
            """);
        var compilation = CSharpCompilation.Create(
            nameof(RegexWithoutOptionsTypeStillValidatesAsync),
            [tree],
            CoreReferences,
            new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new Sst2444InvalidRegexPatternAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2444");
        var text = await tree.GetTextAsync(CancellationToken.None);
        await Assert.That(text.ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("\"[\"");
    }

    /// <summary>Verifies cached valid and invalid patterns retain their verdict and effective options.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RepeatedPatternsKeepTheirVerdictsAsync() =>
        VerifyAsync("""
            using System.Text.RegularExpressions;
            public class C
            {
                public void M()
                {
                    _ = new Regex({|SST2444:"["|});
                    _ = new Regex({|SST2444:"["|}, RegexOptions.Compiled);
                    _ = new Regex("(a)\\1");
                    _ = new Regex("(a)\\1", RegexOptions.Compiled);
                    _ = new Regex({|SST2444:"(a)\\1"|}, RegexOptions.NonBacktracking);
                }
            }
            """);

    /// <summary>Verifies every supported static query name validates its pattern.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticQueryNamesValidatePatternsAsync() =>
        VerifyAsync("""
            using System.Text.RegularExpressions;
            using static System.Text.RegularExpressions.Regex;
            public class C
            {
                public void M()
                {
                    _ = Match("input", {|SST2444:"["|});
                    _ = Regex.Matches("input", {|SST2444:"["|});
                    _ = Regex.Split("input", {|SST2444:"["|});
                    _ = Regex.Count("input", {|SST2444:"["|});
                    _ = Regex.EnumerateMatches("input", {|SST2444:"["|});
                    _ = Regex.EnumerateSplits("input", {|SST2444:"["|});
                }
            }
            """);

    /// <summary>Verifies input literals do not turn variable or null patterns into constant strings.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantAndNullPatternsWithLiteralInputAreCleanAsync() =>
        VerifyAsync("""
            using System.Text.RegularExpressions;
            public class C
            {
                public void M(string pattern)
                {
                    _ = Regex.IsMatch("input", pattern);
                    _ = Regex.IsMatch("input", null);
                    _ = new Regex($"{pattern}");
                    const string Invalid = "[";
                    _ = new Regex(Invalid);
                    _ = Regex.IsMatch("input", {|SST2444:Invalid|});
                }
            }
            """);

    /// <summary>Verifies variable options prevent validation while unsupported constant options are not pattern defects.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantAndInvalidOptionsAreCleanAsync() =>
        VerifyAsync("""
            using System.Text.RegularExpressions;
            public class C
            {
                public void M(RegexOptions options)
                {
                    _ = new Regex("[", options);
                    _ = new Regex("[", (RegexOptions)(-1));
                    _ = Regex.IsMatch(options: RegexOptions.None, pattern: {|SST2444:"["|}, input: "input");
                }
            }
            """);

    /// <summary>Verifies qualified construction names bind while unrelated calls and unsupported invocation shapes stay silent.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QualifiedNamesAndUnrelatedCallsAreDistinguishedAsync() =>
        VerifyAsync("""
            using R = System.Text.RegularExpressions;
            public class C
            {
                public void M(System.Text.RegularExpressions.Regex regex, System.Func<string, bool> IsMatch)
                {
                    _ = new System.Text.RegularExpressions.Regex({|SST2444:"["|});
                    _ = new R::Regex({|SST2444:"["|});
                    _ = new object();
                    _ = System.Text.RegularExpressions.Regex.Escape("[");
                    _ = regex?.IsMatch("[");
                    _ = (IsMatch ?? IsMatch)("[");
                    _ = regex.IsMatch("[");
                }
            }
            """);

    /// <summary>Verifies a parameter named pattern must also have string type and belong to the regex engine.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LookalikeConstructorsAndParametersAreCleanAsync() =>
        VerifyAsync("""
            public class Regex
            {
                public Regex(string pattern) { }
                public static bool IsMatch(string input, int pattern) => false;
            }
            public class C
            {
                public void M()
                {
                    _ = new Regex("[");
                    _ = Regex.IsMatch("[", 0);
                }
            }
            """);

    /// <summary>Verifies incomplete constructions and calls with unbound arguments remain silent during editing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MalformedCallsAreCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = """
                using System.Text.RegularExpressions;
                public class C
                {
                    public void M()
                    {
                        _ = new Regex { };
                        _ = new int(0);
                        _ = Regex.IsMatch("[", missing: 1);
                        _ = new Regex();
                    }
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies nodes outside the registered call kinds cannot name a regex API.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonCallSyntaxDoesNotNameRegexApiAsync()
    {
        var namesApi = Sst2444InvalidRegexPatternAnalyzer.NamesRegexApi(SyntaxFactory.IdentifierName("Regex"));
        await Assert.That(namesApi).IsFalse();
    }

    /// <summary>Verifies names sharing query prefixes and lengths do not pass the syntactic API filter.</summary>
    /// <param name="name">A near miss for a supported query method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("IsMatcX")]
    [Arguments("IsMatchAsync")]
    [Arguments("MatcX")]
    [Arguments("MatcheX")]
    [Arguments("ReplacX")]
    [Arguments("SpliX")]
    [Arguments("CounX")]
    [Arguments("EnumerateMatcheX")]
    [Arguments("EnumerateSplitX")]
    [Arguments("Other")]
    [Arguments("Unknown")]
    [Arguments("Find")]
    [Arguments("UnrelatedMethod")]
    [Arguments("UnrelatedMethods")]
    public async Task NearMissQueryNamesAreRejectedAsync(string name)
    {
        var invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name));
        var namesApi = Sst2444InvalidRegexPatternAnalyzer.NamesRegexApi(invocation);
        await Assert.That(namesApi).IsFalse();
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
