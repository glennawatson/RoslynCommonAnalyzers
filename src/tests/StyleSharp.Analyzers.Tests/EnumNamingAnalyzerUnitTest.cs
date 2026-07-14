// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1319EnumNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1319 (enumeration names should be PascalCase).</summary>
public class EnumNamingAnalyzerUnitTest
{
    /// <summary>Verifies a PascalCase enum produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PascalCaseAsync() => await Verify.VerifyAnalyzerAsync("public enum LogLevel { Debug, Info }");

    /// <summary>Verifies a two-letter acronym stays upper-case, as the .NET guidelines have it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoLetterAcronymAsync() => await Verify.VerifyAnalyzerAsync("public enum IOMode { Read, Write }");

    /// <summary>Verifies a two-letter acronym is allowed at the end of the name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingTwoLetterAcronymAsync() => await Verify.VerifyAnalyzerAsync("public enum XY { A, B }");

    /// <summary>Verifies a digit inside the name is not mistaken for a violation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DigitAsync() => await Verify.VerifyAnalyzerAsync("public enum Http2Client { A, B }");

    /// <summary>Verifies a single-letter name is left alone; it is already PascalCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLetterAsync() => await Verify.VerifyAnalyzerAsync("public enum A { X, Y }");

    /// <summary>Verifies a lower-case enum name is left to SST1300, which owns the first character.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowerCaseIsLeftToElementNamingAsync()
        => await Verify.VerifyAnalyzerAsync("public enum lowercase { A, B }");

    /// <summary>Verifies a lower-case snake_case name is also SST1300's, so the two rules never double up.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowerCaseWithUnderscoreIsLeftToElementNamingAsync()
        => await Verify.VerifyAnalyzerAsync("public enum log_level { A, B }");

    /// <summary>Verifies an underscore is reported once the name starts upper-case, and the fix removes it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnderscoreAfterUpperCaseAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:Snake_Case|} { A, B }",
            "public enum SnakeCase { A, B }");

    /// <summary>Verifies a trailing underscore is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingUnderscoreAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:Log_Level_|} { A, B }",
            "public enum LogLevel { A, B }");

    /// <summary>Verifies an acronym longer than two letters is reported and rewritten as a word.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingAcronymAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:HTTPStatus|} { A, B }",
            "public enum HttpStatus { A, B }");

    /// <summary>Verifies a long acronym is rewritten without disturbing the words that follow it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingAcronymFollowedByWordsAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:HTTPStatusCode|} { A, B }",
            "public enum HttpStatusCode { A, B }");

    /// <summary>Verifies a trailing acronym longer than two letters is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingAcronymAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:MyENUM|} { A, B }",
            "public enum MyEnum { A, B }");

    /// <summary>Verifies a name that is nothing but a long acronym is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AcronymOnlyAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:XYZ|} { A, B }",
            "public enum Xyz { A, B }");

    /// <summary>Verifies a four-letter run followed by a word is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongRunBeforeWordAsync()
        => await Verify.VerifyCodeFixAsync(
            "public enum {|SST1319:ABCDef|} { A, B }",
            "public enum AbcDef { A, B }");

    /// <summary>Verifies the members of a badly named enum are not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MembersAreNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync("public enum Good { lower_case, ALSOBAD }");
}
