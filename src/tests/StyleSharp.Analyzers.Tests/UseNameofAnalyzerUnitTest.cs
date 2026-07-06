// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

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

    /// <summary>Verifies Fix All replaces every parameter-naming literal with nameof in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string first, string second, string third)
                                  {
                                      throw new ArgumentNullException({|SST1415:"first"|});
                                  }

                                  public void N(string first, string second, string third)
                                  {
                                      throw new ArgumentNullException({|SST1415:"second"|});
                                  }

                                  public void O(string first, string second, string third)
                                  {
                                      throw new ArgumentNullException({|SST1415:"third"|});
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string first, string second, string third)
                                       {
                                           throw new ArgumentNullException(nameof(first));
                                       }

                                       public void N(string first, string second, string third)
                                       {
                                           throw new ArgumentNullException(nameof(second));
                                       }

                                       public void O(string first, string second, string third)
                                       {
                                           throw new ArgumentNullException(nameof(third));
                                       }
                                   }
                                   """;
        await VerifyNameof.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rule stays silent below C# 6, where nameof does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp6Async()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      throw new ArgumentNullException("value");
                                  }
                              }
                              """;
        var test = new VerifyNameof.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp5));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
