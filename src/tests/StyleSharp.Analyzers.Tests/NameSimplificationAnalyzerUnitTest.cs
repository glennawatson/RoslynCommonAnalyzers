// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNameSimplification = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.NameSimplificationAnalyzer,
    StyleSharp.Analyzers.NameSimplificationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for shortest-equivalent-name analysis (SST1116/SST1117).</summary>
public class NameSimplificationAnalyzerUnitTest
{
    /// <summary>Verifies a qualified type name is shortened only when the shorter name binds to the same symbol.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedNameCandidateIsFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public int M({|SST1116:System.Text.StringBuilder|} builder) => builder.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public sealed class C
                                   {
                                       public int M(StringBuilder builder) => builder.Length;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a redundant this-qualified member access is shortened.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisMemberAccessCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M() => {|SST1117:this._value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly int _value = 1;

                                       public int M() => _value;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies names are not shortened when the shorter spelling would bind elsewhere.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousShortNamesAreCleanAsync()
    {
        const string Source = """
                              namespace Other
                              {
                                  public sealed class Widget
                                  {
                                  }
                              }

                              public sealed class Widget
                              {
                              }

                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public Other.Widget Create() => new Other.Widget();

                                  public int M()
                                  {
                                      var _value = 2;
                                      return this._value + _value;
                                  }
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies parameters that shadow member names keep the explicit <c>this.</c> qualifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterShadowKeepsThisQualifierAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int value = 1;

                                  public int M(int value) => this.value + value;
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local function that shadows a method name keeps the explicit <c>this.</c> qualifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalFunctionShadowKeepsThisQualifierAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int GetValue() => 1;

                                  public int M()
                                  {
                                      int GetValue() => 2;
                                      return this.GetValue();
                                  }
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies generic qualified names keep the slower semantic fallback and still simplify correctly.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericQualifiedNameCandidateIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public int M({|SST1116:System.Collections.Generic.List<int>|} values) => values.Count;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public int M(List<int> values) => values.Count;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies generated files are not analyzed even when diagnostic reporting is optimized.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneratedFilesStayCleanAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M(System.Text.StringBuilder builder) => this._value + builder.Length;
                              }
                              """;
        var test = new VerifyNameSimplification.Test();
        test.TestState.Sources.Add(("NameSimplificationBench.g.cs", Source));

        await test.RunAsync(CancellationToken.None);
    }
}
