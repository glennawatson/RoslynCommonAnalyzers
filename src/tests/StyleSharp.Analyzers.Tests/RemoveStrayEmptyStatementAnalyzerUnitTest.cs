// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyStraySemicolon = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2259RemoveStrayEmptyStatementAnalyzer,
    StyleSharp.Analyzers.Sst2259RemoveStrayEmptyStatementCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2259RemoveStrayEmptyStatementAnalyzer"/> and its code fix (SST2259).</summary>
public class RemoveStrayEmptyStatementAnalyzerUnitTest
{
    /// <summary>Verifies a stray semicolon after a type body is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StraySemicolonAfterTypeIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                              }{|SST2259:;|}
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyStraySemicolon.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a stray semicolon after a struct body is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StraySemicolonAfterStructIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal struct S
                              {
                              }{|SST2259:;|}
                              """;
        const string FixedSource = """
                                   internal struct S
                                   {
                                   }
                                   """;
        await VerifyStraySemicolon.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a stray semicolon after an enum body is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StraySemicolonAfterEnumIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum E
                              {
                                  A,
                              }{|SST2259:;|}
                              """;
        const string FixedSource = """
                                   internal enum E
                                   {
                                       A,
                                   }
                                   """;
        await VerifyStraySemicolon.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type with no stray semicolon is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NormalTypeIsCleanAsync()
        => await VerifyStraySemicolon.VerifyAnalyzerAsync(
            """
            internal class C
            {
            }
            """);

    /// <summary>Verifies a positional record's required semicolon is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalRecordSemicolonIsCleanAsync()
        => await VerifyStraySemicolon.VerifyAnalyzerAsync(
            """
            internal record R(int X);
            """);

    /// <summary>Verifies a method body with a trailing semicolon is left to the compiler (CS1597) and not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodBodySemicolonIsNotReportedAsync()
        => await VerifyStraySemicolon.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                }{|CS1597:;|}
            }
            """);
}
