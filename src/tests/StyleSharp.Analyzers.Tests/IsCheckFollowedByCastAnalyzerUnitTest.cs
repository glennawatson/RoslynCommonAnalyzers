// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyPatternMatching = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PatternMatchingAnalyzer,
    StyleSharp.Analyzers.DeclarationPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2007 (is check followed by a cast local).</summary>
public class IsCheckFollowedByCastAnalyzerUnitTest
{
    /// <summary>Verifies an <c>is</c> check followed by a matching local cast is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsCheckFollowedByCastLocalIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object value)
                                  {
                                      if ({|SST2007:value is string|})
                                      {
                                          var text = (string)value;
                                          return text.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object value)
                                       {
                                           if (value is string text)
                                           {
                                               return text.Length;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyPatternMatching.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies property receivers and mismatched casts are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsafeOrMismatchedShapesAreCleanAsync()
        => await VerifyPatternMatching.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly object _value = "";

                public object Value { get; } = "";

                public int PropertyReceiver()
                {
                    if (Value is string)
                    {
                        var text = (string)Value;
                        return text.Length;
                    }

                    return 0;
                }

                public int MismatchedCast(object value)
                {
                    if (value is string)
                    {
                        var text = (object)value;
                        return text.GetHashCode();
                    }

                    return 0;
                }

                public int FieldReceiver()
                {
                    if (_value is string)
                    {
                        var text = (string)_value;
                        return text.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 7, where the declaration pattern the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object value)
                                  {
                                      if (value is string)
                                      {
                                          var text = (string)value;
                                          return text.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        var test = new VerifyPatternMatching.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
