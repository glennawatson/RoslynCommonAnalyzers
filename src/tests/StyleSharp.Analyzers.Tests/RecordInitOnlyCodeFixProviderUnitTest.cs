// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInitOnly = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RecordAnalyzer,
    StyleSharp.Analyzers.RecordInitOnlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1802 (record properties should be init-only) and its set→init code fix.</summary>
public class RecordInitOnlyCodeFixProviderUnitTest
{
    /// <summary>Verifies a settable record property is reported (SST1802) and the set accessor is replaced with init.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetAccessorReplacedWithInitAsync()
    {
        const string Source = """
                              public sealed record Person
                              {
                                  public string Name { get; {|SST1802:set|}; }
                              }
                              namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
                              """;
        const string FixedSource = """
                                   public sealed record Person
                                   {
                                       public string Name { get; init; }
                                   }
                                   namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
                                   """;
        await VerifyInitOnly.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies init-only and get-only record properties, and a static settable property, are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitGetOnlyAndStaticPropertiesAreCleanAsync()
        => await VerifyInitOnly.VerifyAnalyzerAsync(
            """
            public sealed record Person
            {
                public string Name { get; init; }

                public int Age { get; }

                public static int Count { get; set; }
            }
            namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
            """);
}
