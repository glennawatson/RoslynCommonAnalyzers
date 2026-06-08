// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1316TupleElementNamingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1316 (tuple element names should match the configured casing).</summary>
public class TupleElementNamingAnalyzerUnitTest
{
    /// <summary>Verifies PascalCase tuple elements produce no diagnostics by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public (int Count, string Name) M() => default; }");

    /// <summary>Verifies a camelCase element in a tuple type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleTypeAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public (int {|SST1316:count|}, string Name) M() => default; }");

    /// <summary>Verifies a camelCase element in a tuple expression is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleExpressionAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M() { var t = ({|SST1316:count|}: 1, Name: 2); } }");

    /// <summary>Verifies inferred tuple element names (taken from variables) are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InferredNamesIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M() { int count = 1; var t = (count, count); } }");

    /// <summary>Verifies an editorconfig override to camel_case flags a PascalCase element.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CamelCaseConfiguredAsync()
    {
        var test = new Verify.Test
        {
            TestCode = "public class C { public (int {|SST1316:Count|}, string name) M() => default; }"
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.tuple_element_naming = camel_case

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
