// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyNameof = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1415UseNameofAnalyzer,
    StyleSharp.Analyzers.Sst1415UseNameofCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1415 (use nameof for parameter references) and its fix.</summary>
public class UseNameofAnalyzerUnitTest
{
    /// <summary>Verifies the shared syntax counter counts matching literals and rejects unrelated creations.</summary>
    /// <param name="creation">The object creation to inspect.</param>
    /// <param name="expected">The number of parameter-name literals.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new ArgumentException(\"first\", \"second\")", 2)]
    [Arguments("new System.ArgumentException(\"missing\", nameof(first))", 0)]
    [Arguments("new ArgumentException()", 0)]
    [Arguments("new ArgumentException { }", 0)]
    [Arguments("new Exception(\"first\")", 0)]
    [Arguments("new global::ArgumentException(\"first\")", 0)]
    public async Task ParameterLiteralCountMatchesSyntaxAsync(string creation, int expected)
    {
        var root = await CSharpSyntaxTree.ParseText($"class C {{ void M(string first, string second) {{ _ = {creation}; }} }}").GetRootAsync();
        var expression = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        await Assert.That(Sst1415UseNameofAnalyzer.CountParameterNameLiteralMatches(expression)).IsEqualTo(expected);
    }

    /// <summary>Verifies parameter lookup handles constructors, lambdas, local functions, and indexers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ParameterOwnersAreRecognizedAsync() =>
        VerifyNameof.VerifyAnalyzerAsync("""
            using System;
            class C
            {
                C(string first, string second) { throw new System.ArgumentException({|SST1415:"second"|}); }
                int this[int index] => throw new ArgumentOutOfRangeException({|SST1415:"index"|});
                void M(string outer)
                {
                    Action<string> one = value => throw new ArgumentNullException({|SST1415:"value"|});
                    Action<string> two = (value) => throw new ArgumentNullException({|SST1415:"value"|});
                    Action<string> capture = value => throw new ArgumentNullException({|SST1415:"outer"|});
                    void Local(string local) { throw new ArgumentNullException({|SST1415:"local"|}); }
                    void Boundary() { throw new ArgumentNullException("outer"); }
                }
            }
            """);

    /// <summary>Verifies nonmatching names and exception shapes remain untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonParameterExceptionArgumentsAreCleanAsync() =>
        VerifyNameof.VerifyAnalyzerAsync("""
            using System;
            class C
            {
                object field = new ArgumentNullException("missing");
                void M(string value)
                {
                    _ = new Exception("value");
                    _ = new ArgumentNullException();
                    _ = new ArgumentNullException { };
                    _ = new ArgumentException(null, value);
                    _ = new ArgumentNullException("missing");
                    Action<string> action = item => throw new ArgumentNullException("missing");
                }
            }
            """);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonParameterStringAndNameofAreCleanAsync() =>
        VerifyNameof.VerifyAnalyzerAsync(
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
