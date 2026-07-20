// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeRegex = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1303RegexInjectionAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1303 (a regular-expression pattern must not be built from non-constant data).</summary>
public class RegexInjectionAnalyzerUnitTest
{
    /// <summary>Verifies a non-constant pattern passed to the Regex constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantConstructorPatternReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M(string userPattern) => new Regex({|SES1303:userPattern|});
            }
            """);

    /// <summary>Verifies a concatenated data-derived constructor pattern is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenatedConstructorPatternReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M(string data) => new Regex({|SES1303:"^" + data + "$"|}, RegexOptions.IgnoreCase);
            }
            """);

    /// <summary>Verifies an interpolated data-derived constructor pattern is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedConstructorPatternReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M(string data) => new Regex({|SES1303:$"^{data}$"|});
            }
            """);

    /// <summary>Verifies the fully-qualified constructor form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedConstructorPatternReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public System.Text.RegularExpressions.Regex M(string userPattern)
                    => new System.Text.RegularExpressions.Regex({|SES1303:userPattern|});
            }
            """);

    /// <summary>Verifies a non-constant pattern passed to static Regex.IsMatch is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantIsMatchPatternReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input, string userPattern) => Regex.IsMatch(input, {|SES1303:userPattern|});
            }
            """);

    /// <summary>Verifies a non-constant pattern passed to static Regex.Replace is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantReplacePatternReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public string M(string input, string userPattern) => Regex.Replace(input, {|SES1303:userPattern|}, "X");
            }
            """);

    /// <summary>Verifies non-constant patterns to static Match, Matches, and Split are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantMatchMatchesSplitPatternsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public void M(string input, string userPattern)
                {
                    _ = Regex.Match(input, {|SES1303:userPattern|});
                    _ = Regex.Matches(input, {|SES1303:userPattern|});
                    _ = Regex.Split(input, {|SES1303:userPattern|});
                }
            }
            """);

    /// <summary>Verifies the pattern is reported when supplied by name in a reordered call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedPatternArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input, string userPattern) => Regex.IsMatch(pattern: {|SES1303:userPattern|}, input: input);
            }
            """);

    /// <summary>Verifies a constant literal constructor pattern is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantConstructorPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex M() => new Regex("^[a-z]+$", RegexOptions.IgnoreCase);
            }
            """);

    /// <summary>Verifies a const-composed constructor pattern is not reported (its value is constant).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstComposedConstructorPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                private const string Digits = "[0-9]+";

                public Regex M() => new Regex("^" + Digits + "$");
            }
            """);

    /// <summary>Verifies a constant literal static pattern is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantStaticPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string input) => Regex.IsMatch(input, "^[a-z]+$");
            }
            """);

    /// <summary>Verifies a non-constant input with a constant pattern is not reported (input is out of scope).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantInputWithConstantPatternIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(string userInput) => Regex.IsMatch(userInput, "^[a-z]+$");
            }
            """);

    /// <summary>Verifies an instance Regex.IsMatch (which carries no pattern argument) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceIsMatchWithNonConstantInputIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool M(Regex regex, string userInput) => regex.IsMatch(userInput);
            }
            """);

    /// <summary>Verifies a same-named method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedTypeWithMatchingSignatureIsCleanAsync()
        => await VerifyNet90Async(
            """
            public static class Regex
            {
                public static bool IsMatch(string input, string pattern) => false;
            }

            public class C
            {
                public bool M(string input, string userPattern) => Regex.IsMatch(input, userPattern);
            }
            """);

    /// <summary>Verifies a same-named constructor on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedConstructorNamedRegexIsCleanAsync()
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
                public Regex M(string userPattern) => new Regex(userPattern);
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
