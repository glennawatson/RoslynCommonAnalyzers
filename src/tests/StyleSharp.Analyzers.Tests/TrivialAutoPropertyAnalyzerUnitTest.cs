// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAutoProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TrivialAutoPropertyAnalyzer,
    StyleSharp.Analyzers.TrivialAutoPropertyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1420 (use an auto-property for trivial accessors).</summary>
public class TrivialAutoPropertyAnalyzerUnitTest
{
    /// <summary>Verifies a trivial get/set property is converted to an auto-property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TrivialPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public int {|SST1420:Value|} { get => _value; set => _value = value; }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value { get; set; }
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies accessor logic and external field use prevent the diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonTrivialOrSharedFieldIsCleanAsync()
        => await VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value { get => _value; set => _value = value < 0 ? 0 : value; }

                public int Read() => _value;
            }
            """);
}
