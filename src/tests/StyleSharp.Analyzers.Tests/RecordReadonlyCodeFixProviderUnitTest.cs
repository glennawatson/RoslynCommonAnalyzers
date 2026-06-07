// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonly = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RecordAnalyzer,
    StyleSharp.Analyzers.RecordReadonlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1803 (record structs should be readonly) and its add-readonly code fix.</summary>
public class RecordReadonlyCodeFixProviderUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a non-readonly record struct is reported (SST1803) and the readonly modifier is added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyModifierAddedAsync()
    {
        const string Source = """
                              public record struct {|SST1803:Point|}(int X, int Y);
                              namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
                              """;
        const string FixedSource = """
                                   public readonly record struct Point(int X, int Y);
                                   namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
                                   """;
        await VerifyReadonly.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a readonly record struct and a record class are not reported by SST1803.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyStructAndRecordClassAreCleanAsync()
        => await VerifyReadonly.VerifyAnalyzerAsync(
            $$"""
            public readonly record struct Point(int X, int Y);
            public sealed record Person(string Name);{{IsExternalInit}}
            """);
}
