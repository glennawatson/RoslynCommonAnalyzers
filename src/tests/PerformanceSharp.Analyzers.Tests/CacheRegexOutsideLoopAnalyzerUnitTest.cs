// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyCacheRegexOutsideLoop = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1421CacheRegexOutsideLoopAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Psh1421CacheRegexOutsideLoopAnalyzer"/> (PSH1421).</summary>
public class CacheRegexOutsideLoopAnalyzerUnitTest
{
    /// <summary>Cached references for a framework without regular expressions.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies fully qualified receivers and loops without refreshed names are reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedRegexInUnchangingLoopIsReportedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            class C
            {
                void M(string input, string pattern)
                {
                    while (true)
                    {
                        {|PSH1421:System.Text.RegularExpressions.Regex.IsMatch(input, pattern)|};
                    }
                }
            }
            """);

    /// <summary>Verifies ref writes, deconstruction, and nested function parameters prevent hoisting.</summary>
    /// <param name="body">The loop body that refreshes a pattern name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Update(ref pattern); _ = Regex.IsMatch(input, pattern);")]
    [Arguments("var (pattern, other) = (input, input); _ = Regex.IsMatch(input, pattern);")]
    [Arguments("System.Func<string, string> copy = pattern => pattern; _ = Regex.IsMatch(input, pattern);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RefreshedPatternNamesAreCleanAsync(string body) =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            $$"""
            using System.Text.RegularExpressions;
            class C
            {
                static string pattern;
                void M(string input)
                {
                    while (true) { {{body}} }
                }
                static void Update(ref string value) { value = "x"; }
            }
            """);

    /// <summary>Verifies instance receivers and unbound methods fail semantic matching.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InvalidAndInstanceRegexCallsAreCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;
            class C
            {
                bool M(Regex Regex, string input) => Regex.IsMatch(input, 0);
                bool N(string input) => Regex.{|CS0117:Missing|}(input, "pattern");
                bool P(Regex value, string input) => (value).IsMatch(input, 0);
            }
            """);

    /// <summary>Verifies a same-named user type cannot pass framework identity matching.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UserDefinedRegexIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            class Regex { public static bool IsMatch(string input, string pattern) => true; }
            class C { bool M() => Regex.IsMatch("input", "pattern"); }
            """);

    /// <summary>Verifies local functions do not inherit a surrounding loop's runtime pattern.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LocalFunctionRuntimePatternIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;
            class C
            {
                void M(string pattern)
                {
                    while (true) { bool Match(string value) => Regex.IsMatch(value, pattern); }
                }
            }
            """);

    /// <summary>Verifies missing regex metadata and methods without supplied pattern arguments remain silent.</summary>
    /// <param name="declaration">The available regex type and methods.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class Regex { public static bool Check(string input, string value) => true; }")]
    [Arguments("namespace System.Text.RegularExpressions { class Regex { public static bool Check(string input, string value) => true; } }")]
    [Arguments("namespace System.Text.RegularExpressions { class Regex { public static bool Check(string input, string value, string pattern = null) => true; } }")]
    public async Task MissingPatternContractIsCleanAsync(string declaration)
    {
        var source = $$"""{{declaration}} class C { bool M() => Regex.Check("a", "b"); }""";
        if (declaration.StartsWith("namespace", StringComparison.Ordinal))
        {
            source = $"using System.Text.RegularExpressions; {source}";
        }

        var compilation = CSharpCompilation.Create("RegexContract", [CSharpSyntaxTree.ParseText(source)], CoreReferences);
        var diagnostics = await compilation.WithAnalyzers([new Psh1421CacheRegexOutsideLoopAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a top-level call has no enclosing member or loop and still reports a constant pattern.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelConstantPatternIsReportedAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("_ = System.Text.RegularExpressions.Regex.IsMatch(\"a\", \"b\");");
        var compilation = CSharpCompilation.Create("TopLevelRegex", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1421CacheRegexOutsideLoopAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["PSH1421"]);
    }

    /// <summary>Verifies a bound regex call in an incomplete assembly attribute has no enclosing member or loop.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AssemblyAttributeConstantPatternIsReportedAsync()
    {
        const string Source = """
            using System;
            using System.Text.RegularExpressions;
            [assembly: Flag(Regex.IsMatch("input", "pattern"))]
            class FlagAttribute : Attribute { public FlagAttribute(bool value) { } }
            """;
        var compilation = CSharpCompilation.Create("AttributeRegex", [CSharpSyntaxTree.ParseText(Source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1421CacheRegexOutsideLoopAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["PSH1421"]);
    }

    /// <summary>Verifies a static match call inside a foreach body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallInForeachIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public int M(string[] values)
                {
                    var count = 0;
                    foreach (var value in values)
                    {
                        if ({|PSH1421:Regex.IsMatch(value, "[a-z]+")|})
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a static replace call inside a for body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallInForIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = {|PSH1421:Regex.Replace(values[i], "[0-9]", "")|};
                    }
                }
            }
            """);

    /// <summary>Verifies a static call inside a while body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallInWhileIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string value, int count)
                {
                    while (count > 0)
                    {
                        _ = {|PSH1421:Regex.Split(value, ",")|};
                        count--;
                    }
                }
            }
            """);

    /// <summary>Verifies a static call inside a do body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallInDoIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string value, int count)
                {
                    do
                    {
                        _ = {|PSH1421:Regex.Match(value, "[a-z]")|};
                        count--;
                    }
                    while (count > 0);
                }
            }
            """);

    /// <summary>Verifies an instance call inside a loop is left alone; it already holds its pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceCallInLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                private static readonly Regex Pattern = new Regex("[a-z]+");

                public int M(string[] values)
                {
                    var count = 0;
                    foreach (var value in values)
                    {
                        if (Pattern.IsMatch(value))
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a constant pattern outside any loop is reported; it can always be hoisted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstantPatternOutsideLoopIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public bool M(string value) => {|PSH1421:Regex.IsMatch(value, "[a-z]+")|};
            }
            """);

    /// <summary>Verifies a constant declared elsewhere still counts as a hoistable pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedConstantPatternIsFlaggedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                private const string Pattern = "[a-z]+";

                public bool M(string value) => {|PSH1421:Regex.IsMatch(value, Pattern)|};
            }
            """);

    /// <summary>Verifies a run-time pattern outside a loop is left alone; there is nothing to hoist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RuntimePatternOutsideLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public bool M(string value, string pattern) => Regex.IsMatch(value, pattern);
            }
            """);

    /// <summary>Verifies a call inside a lambda declared in a loop is left alone; it runs when the delegate does.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The pattern is built at run time so the constant-pattern arm cannot fire, leaving the loop question as
    /// the only thing under test.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallInsideLambdaIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System;
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string[] values, string pattern)
                {
                    foreach (var value in values)
                    {
                        Func<bool> check = () => Regex.IsMatch(value, pattern);
                        _ = check;
                    }
                }
            }
            """);

    /// <summary>Verifies Regex.Escape inside a loop is left alone, because it compiles no pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RegexEscapeInLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string[] values)
                {
                    foreach (var value in values)
                    {
                        _ = Regex.Escape(value);
                    }
                }
            }
            """);

    /// <summary>Verifies Regex.Unescape inside a loop is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RegexUnescapeInLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string[] values)
                {
                    foreach (var value in values)
                    {
                        _ = Regex.Unescape(value);
                    }
                }
            }
            """);

    /// <summary>Verifies a pattern read from the loop's iteration variable is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IterationVariablePatternIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string input, string[] patterns)
                {
                    foreach (var pattern in patterns)
                    {
                        _ = Regex.IsMatch(input, pattern);
                    }
                }
            }
            """);

    /// <summary>Verifies a pattern built from the loop's iteration variable is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PatternBuiltFromIterationVariableIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string input, string[] names)
                {
                    foreach (var name in names)
                    {
                        _ = Regex.IsMatch(input, "^" + name + "$");
                    }
                }
            }
            """);

    /// <summary>Verifies a pattern the loop reassigns is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PatternWrittenInsideLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string input, string[] names)
                {
                    var pattern = "^a$";
                    foreach (var name in names)
                    {
                        pattern = name;
                        _ = Regex.IsMatch(input, pattern);
                    }
                }
            }
            """);

    /// <summary>Verifies a pattern fixed before the loop is still reported, because it can be hoisted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PatternFixedBeforeLoopIsReportedAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public void M(string[] values, string suffix)
                {
                    var pattern = "^" + suffix + "$";
                    foreach (var value in values)
                    {
                        _ = {|PSH1421:Regex.IsMatch(value, pattern)|};
                    }
                }
            }
            """);

    /// <summary>Verifies an unrelated static call inside a loop is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedStaticCallInLoopIsCleanAsync() =>
        VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(string[] values)
                {
                    foreach (var value in values)
                    {
                        _ = string.Concat(value, value);
                    }
                }
            }
            """);
}
