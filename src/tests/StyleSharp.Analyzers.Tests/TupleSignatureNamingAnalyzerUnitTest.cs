// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTupleSignature = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.TupleSignatureNamingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1414 (tuple types in signatures should have element names).</summary>
public class TupleSignatureNamingAnalyzerUnitTest
{
    /// <summary>Verifies an unnamed signature tuple is reported, while a named one and a local are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnnamedSignatureTupleReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public {|SST1414:(int, string)|} M() => default;

                                  public (int Count, string Name) Named() => default;

                                  public void Local()
                                  {
                                      (int, string) value = default;
                                      _ = value;
                                  }
                              }
                              """;
        var test = new VerifyTupleSignature.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }
}
