// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions;

using VerifyRecord = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.RecordAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the record rules (SST1800 sealing and SST1801 positional-parameter casing).</summary>
public class RecordAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = "\nnamespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }";

    /// <summary>Verifies a record class that is neither sealed nor abstract is reported (SST1800, force-enabled here).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsealedRecordClassReportedAsync()
        => await VerifyRecord.VerifyAnalyzerAsync("public record {|SST1800:Animal|};");

    /// <summary>Verifies sealed and abstract record classes are not reported by SST1800.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedAndAbstractRecordClassesAreCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync("public sealed record Cat;\npublic abstract record Shape;");

    /// <summary>Verifies camelCase positional record parameters are reported (SST1801, default PascalCase).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowercasePositionalParametersReportedAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            "public sealed record Point(int {|SST1801:x|}, int {|SST1801:y|});" + IsExternalInit);

    /// <summary>Verifies PascalCase positional record parameters are not reported by SST1801.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PascalCasePositionalParametersAreCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync("public sealed record Point(int X, int Y);" + IsExternalInit);

    /// <summary>Verifies an editorconfig override to camel_case flags a PascalCase positional parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CamelCaseConfiguredAsync()
    {
        var test = new VerifyRecord.Test
        {
            TestCode = "public sealed record Point(int {|SST1801:X|}, int y);" + IsExternalInit,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "root = true\n[*.cs]\nstylesharp.record_parameter_naming = camel_case\n"));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the specialized PascalCase fast path accepts compliant positional parameter names.</summary>
    /// <param name="name">The candidate positional parameter name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Value")]
    [Arguments("X")]
    public async Task PascalCaseFastPathAcceptsCompliantNames(string name)
        => await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsTrue();

    /// <summary>Verifies the specialized PascalCase fast path rejects non-compliant positional parameter names.</summary>
    /// <param name="name">The candidate positional parameter name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("")]
    [Arguments("value")]
    [Arguments("1Value")]
    public async Task PascalCaseFastPathRejectsNonCompliantNames(string name)
        => await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsFalse();
}
