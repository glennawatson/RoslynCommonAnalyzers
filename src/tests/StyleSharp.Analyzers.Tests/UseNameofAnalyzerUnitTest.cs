// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNameof = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1415UseNameofAnalyzer,
    StyleSharp.Analyzers.Sst1415UseNameofCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1415 (use nameof for parameter references) and its fix.</summary>
public class UseNameofAnalyzerUnitTest
{
    /// <summary>Verifies a parameter-naming string literal in an argument exception is replaced with nameof.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterNameLiteralReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      throw new ArgumentNullException({|SST1415:"value"|});
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value)
                                       {
                                           throw new ArgumentNullException(nameof(value));
                                       }
                                   }
                                   """;
        await VerifyNameof.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a message string and an existing nameof are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonParameterStringAndNameofAreCleanAsync()
        => await VerifyNameof.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M(string value)
                {
                    throw new ArgumentException("value cannot be blank", nameof(value));
                }
            }
            """);
}
