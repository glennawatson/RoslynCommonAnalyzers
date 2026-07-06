// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyModernSyntaxPreference = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxPreferenceAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxPreferenceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for compact modern syntax preference rules (SST2218-SST2219).</summary>
public class ModernSyntaxPreferenceAnalyzerUnitTest
{
    /// <summary>Verifies explicit lambda parameter types are removed when the delegate target supplies them.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypesAreFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private Func<int, int, int> _sum = {|SST2218:(int left, int right)|} => left + right;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       private Func<int, int, int> _sum = (left, right) => left + right;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are removed for single non-generic invocation targets.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeForSingleInvocationTargetIsFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M() => Select({|SST2218:(int value)|} => value + 1, 1);

                                  private static int Select(Func<int, int> selector, int value) => selector(value);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public int M() => Select((value) => value + 1, 1);

                                       private static int Select(Func<int, int> selector, int value) => selector(value);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are kept when they select an overload.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeNeededForOverloadResolutionIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      Invoke((int _) => { });
                                  }

                                  private static void Invoke(Action<int> action)
                                  {
                                  }

                                  private static void Invoke(Action<string> action)
                                  {
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are kept when they infer generic arguments.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeNeededForGenericInferenceIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      Create((string input) => input.ToUpperInvariant());
                                  }

                                  private static void Create<T>(Func<T, string> action)
                                  {
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies simple get and set accessor bodies are expression-bodied.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SimplePropertyAccessorsAreFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public int Value
                                  {
                                      {|SST2219:get|} { return _value; }
                                      {|SST2219:set|} { _value = value; }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public int Value
                                       {
                                           get => _value;
                                           set => _value = value;
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ambiguous syntax shapes stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousSyntaxShapesAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private delegate int RefFunc(ref int value);

                                  private RefFunc _converted = (ref int value) => value;

                                  public int Value
                                  {
                                      get
                                      {
                                          var value = 1;
                                          return value;
                                      }
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies expression-bodied accessor suggestions stay silent below C# 7, where they cannot be written.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SimplePropertyAccessorsAreSilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public int Value
                                  {
                                      get { return _value; }
                                      set { _value = value; }
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxPreference.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyModernSyntaxPreference.Test CreateNet80Test(string source, string fixedSource)
        => new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource
        };
}
