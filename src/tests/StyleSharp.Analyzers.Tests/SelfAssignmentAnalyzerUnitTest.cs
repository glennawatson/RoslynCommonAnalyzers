// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySelfAssign = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.SelfAssignmentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1189 (self-assignment) and its fix.</summary>
public class SelfAssignmentAnalyzerUnitTest
{
    /// <summary>Verifies a field self-assignment is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfAssignmentRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public void M()
                                  {
                                      _value = 5;
                                      {|SST1189:_value = _value|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _value;

                                       public void M()
                                       {
                                           _value = 5;
                                       }
                                   }
                                   """;
        await VerifySelfAssign.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every self-assignment in a document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _first;
                                  private int _second;
                                  private int _third;

                                  public void M()
                                  {
                                      {|SST1189:_first = _first|};
                                      {|SST1189:_second = _second|};
                                      {|SST1189:_third = _third|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _first;
                                       private int _second;
                                       private int _third;

                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifySelfAssign.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a genuine assignment and a constructor field assignment are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenuineAssignmentsAreCleanAsync()
        => await VerifySelfAssign.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public C(int value) => _value = value;

                public void M(int other) => _value = other;
            }
            """);
}
