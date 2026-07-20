// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeRegex = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1509BacktrackingRegexWithoutTimeoutAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1509 (a backtracking-prone regex must not run with no match timeout).</summary>
public class BacktrackingRegexWithoutTimeoutAnalyzerUnitTest
{
    /// <summary>Verifies a nested-quantifier constant pattern to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedQuantifierToConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex({|SES1509:"(a+)+"|});
            }
            """);

    /// <summary>Verifies a top-level alternation inside a repeated group is reported on a static call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverlappingAlternationToStaticIsMatchReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input) => Regex.IsMatch(input, {|SES1509:"(a|aa)+"|});
            }
            """);

    /// <summary>Verifies a nested-quantifier constant pattern in a <c>[GeneratedRegex]</c> attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedQuantifierInGeneratedRegexReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task StarOnStarConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex({|SES1509:"(.*)*"|});
            }
            """);

    /// <summary>Verifies the pattern passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedPatternArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex(pattern: {|SES1509:@"(\d+)*"|});
            }
            """);

    /// <summary>Verifies a nested-quantifier constant pattern to static <c>Regex.Replace</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReplaceReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public string M(string input) => Regex.Replace(input, {|SES1509:"(a*)*"|}, "x");
            }
            """);

    /// <summary>Verifies an unbounded <c>{n,}</c> outer quantifier over a repeated group is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnboundedBraceOuterQuantifierReportedAsync()
        => await VerifyNet90Async(
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

        var test = new AnalyzeRegex.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a supplied match timeout keeps the constructor call clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchTimeoutSuppressesAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task NonBacktrackingOptionSuppressesAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+)+", RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies <c>NonBacktracking</c> combined with other flags is still recognized as safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBacktrackingCombinedWithFlagsSuppressesAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+)+", RegexOptions.Compiled | RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies a benign constant pattern with no nested quantifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BenignConstantPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("^[a-z]+$");
            }
            """);

    /// <summary>Verifies a repeated group whose body neither repeats nor alternates is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedGroupWithoutInnerRepetitionIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(abc)+");
            }
            """);

    /// <summary>Verifies a fixed-width bounded inner quantifier under an outer quantifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundedInnerQuantifierIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex(@"(\d{4})+");
            }
            """);

    /// <summary>Verifies a bounded outer quantifier over a repeated group is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundedOuterQuantifierIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("(a+){2,3}");
            }
            """);

    /// <summary>Verifies a non-constant pattern is not reported (that shape is a separate injection concern).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M(string userPattern) => new Regex(userPattern);
            }
            """);

    /// <summary>Verifies a benign constant pattern to a static call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticBenignPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input) => Regex.IsMatch(input, @"^\d+$");
            }
            """);

    /// <summary>Verifies a <c>[GeneratedRegex]</c> with a match-timeout in milliseconds is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedRegexWithTimeoutIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task SameNamedNonSystemRegexTypeIsCleanAsync()
        => await VerifyNet90Async(
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

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeRegex.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
