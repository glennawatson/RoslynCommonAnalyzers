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

    /// <summary>Verifies an object initializer copying a member of this instance onto the new one is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// In <c>new() { Name = Name }</c> the left side names a member of the object being built and the right
    /// side reads this instance's, so the two identical-looking names are different members of different
    /// objects.
    /// </remarks>
    [Test]
    public async Task ObjectInitializerCopyingThisInstanceIsCleanAsync()
        => await VerifySelfAssign.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name { get; set; }

                public int Count { get; set; }

                public C Clone() => new() { Name = Name };

                public C Bump() => new() { Count = Count + 1 };
            }
            """);

    /// <summary>Verifies a <c>with</c> initializer copying a member of this instance is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithInitializerCopyingThisInstanceIsCleanAsync()
        => await VerifySelfAssign.VerifyAnalyzerAsync(
            """
            public record R(string Name)
            {
                public R Copy(R source) => source with { Name = Name };
            }
            """);

    /// <summary>Verifies a nested object initializer is not reported either.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedObjectInitializerIsCleanAsync()
        => await VerifySelfAssign.VerifyAnalyzerAsync(
            """
            public class Inner
            {
                public string Name { get; set; }
            }

            public class C
            {
                public string Name { get; set; }

                public Inner Child { get; set; } = new();

                public C Clone() => new() { Child = { Name = Name } };
            }
            """);
}
