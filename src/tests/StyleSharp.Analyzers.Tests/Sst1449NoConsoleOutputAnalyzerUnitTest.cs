// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1449NoConsoleOutputAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1449NoConsoleOutputAnalyzer"/> (SST1449 direct console output).</summary>
public class Sst1449NoConsoleOutputAnalyzerUnitTest
{
    /// <summary>Verifies Console.WriteLine is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConsoleWriteLineIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M() => {|SST1449:Console.WriteLine("text")|};
            }
            """);

    /// <summary>Verifies Console.Write and the fully qualified spelling are flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedConsoleWriteIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M() => {|SST1449:System.Console.Write("text")|};
            }
            """);

    /// <summary>Verifies write methods on other types are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OtherWritersAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.IO;

            public class C
            {
                public void M(TextWriter writer) => writer.WriteLine("text");
            }
            """);

    /// <summary>Verifies non-write console members are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConsoleReadIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public string M() => Console.ReadLine();
            }
            """);

    /// <summary>Verifies a user type named Console is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UserConsoleTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class Console
            {
                public static void WriteLine(string text)
                {
                }
            }

            public class C
            {
                public void M() => Console.WriteLine("text");
            }
            """);
}
