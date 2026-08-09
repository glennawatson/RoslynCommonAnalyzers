// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCacheRegexOutsideLoop = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1421CacheRegexOutsideLoopAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Psh1421CacheRegexOutsideLoopAnalyzer"/> (PSH1421).</summary>
public class CacheRegexOutsideLoopAnalyzerUnitTest
{
    /// <summary>Verifies a static match call inside a foreach body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticCallInForeachIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticCallInForIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticCallInWhileIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticCallInDoIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task InstanceCallInLoopIsCleanAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task ConstantPatternOutsideLoopIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
            """
            using System.Text.RegularExpressions;

            internal class C
            {
                public bool M(string value) => {|PSH1421:Regex.IsMatch(value, "[a-z]+")|};
            }
            """);

    /// <summary>Verifies a constant declared elsewhere still counts as a hoistable pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedConstantPatternIsFlaggedAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task RuntimePatternOutsideLoopIsCleanAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticCallInsideLambdaIsCleanAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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

    /// <summary>Verifies an unrelated static call inside a loop is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedStaticCallInLoopIsCleanAsync()
        => await VerifyCacheRegexOutsideLoop.VerifyAnalyzerAsync(
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
