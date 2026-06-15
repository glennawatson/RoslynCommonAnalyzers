// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBaseCall = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.RedundantBaseConstructorCallCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1178 (redundant parameterless base constructor call) and its fix.</summary>
public class RedundantBaseConstructorCallAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless <c>: base()</c> is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessBaseCallRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public C()
                                      {|SST1178:: base()|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public C()
                                       {
                                       }
                                   }
                                   """;
        await VerifyBaseCall.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every redundant parameterless base call in a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public C()
                                      {|SST1178:: base()|}
                                  {
                                  }

                                  public C(int x)
                                      {|SST1178:: base()|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public C()
                                       {
                                       }

                                       public C(int x)
                                       {
                                       }
                                   }
                                   """;
        await VerifyBaseCall.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>: base(arg)</c> with arguments and a <c>: this()</c> chain are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseCallWithArgumentsIsCleanAsync()
        => await VerifyBaseCall.VerifyAnalyzerAsync(
            """
            public class B
            {
                public B(int x)
                {
                }
            }

            public class C : B
            {
                public C()
                    : base(1)
                {
                }

                public C(int x)
                    : this()
                {
                }
            }
            """);
}
