// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyNestedOnly = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1498PrivateMemberUsedOnlyByNestedTypeAnalyzer,
    StyleSharp.Analyzers.Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1498 (a private member only a nested type uses) and its fix.</summary>
public class PrivateMemberUsedOnlyByNestedTypeAnalyzerUnitTest
{
    /// <summary>Verifies a private static method only a nested type calls is reported and moved into it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateStaticMethodIsMovedIntoTheNestedTypeAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:Double|}(int value) => value * 2;

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => Double(value);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Outer
                                   {
                                       internal sealed class Inner
                                       {
                                           public int Run(int value) => Double(value);

                                           private static int Double(int value) => value * 2;
                                       }
                                   }
                                   """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the moved method takes its documentation comment with it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MovedMethodKeepsItsDocumentationAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  /// <summary>Doubles a number.</summary>
                                  /// <param name="value">The number.</param>
                                  /// <returns>Twice the number.</returns>
                                  private static int {|SST1498:Double|}(int value) => value * 2;

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => Double(value);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Outer
                                   {
                                       internal sealed class Inner
                                       {
                                           public int Run(int value) => Double(value);

                                           /// <summary>Doubles a number.</summary>
                                           /// <param name="value">The number.</param>
                                           /// <returns>Twice the number.</returns>
                                           private static int Double(int value) => value * 2;
                                       }
                                   }
                                   """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method that calls itself is still one only the nested type uses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecursiveMethodIsStillReportedAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:Sum|}(int value) => value <= 0 ? 0 : value + Sum(value - 1);

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => Sum(value);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Outer
                                   {
                                       internal sealed class Inner
                                       {
                                           public int Run(int value) => Sum(value);

                                           private static int Sum(int value) => value <= 0 ? 0 : value + Sum(value - 1);
                                       }
                                   }
                                   """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a private field a nested type has taken over is reported, and left for a human to move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Moving a static field changes when its initializer runs, so the fix does not offer to.</remarks>
    [Test]
    public async Task PrivateFieldIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:_factor|} = 2;

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => value * _factor;
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a private property a nested type has taken over is reported, and left for a human to move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivatePropertyIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:Factor|} => 2;

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => value * Factor;
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies an instance member the nested type reaches through the outer instance is reported, not moved.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Whose state it becomes after the move is a design question, so the fix declines it.</remarks>
    [Test]
    public async Task InstanceFieldReachedThroughTheOwnerIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private int {|SST1498:_factor|} = 2;

                                  internal sealed class Inner
                                  {
                                      private readonly Outer _owner;

                                      public Inner(Outer owner) => _owner = owner;

                                      public int Run(int value) => value * _owner._factor;
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a use written against the outer type's name is reported, but never rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The call names the type the method would leave, so moving it would leave the call behind.</remarks>
    [Test]
    public async Task QualifiedUseIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:Double|}(int value) => value * 2;

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => Outer.Double(value);
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a method whose name the outer type reuses is reported, but never moved.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Moving one overload would hide the rest of the family from every unqualified call inside the nested type.</remarks>
    [Test]
    public async Task OverloadFamilyIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Outer
                              {
                                  private static int {|SST1498:Double|}(int value) => value * 2;

                                  private static string Double(string value) => value + value;

                                  public string Twice(string value) => Double(value);

                                  internal sealed class Inner
                                  {
                                      public int Run(int value) => Double(value);
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a nested type with a base class is reported, but never moved into.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A declared member hides the inherited family of that name from every unqualified call in the type.</remarks>
    [Test]
    public async Task NestedTypeWithABaseClassIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal class Base
                              {
                                  protected int Triple(int value) => value * 3;
                              }

                              internal class Outer
                              {
                                  private static int {|SST1498:Double|}(int value) => value * 2;

                                  internal sealed class Inner : Base
                                  {
                                      public int Run(int value) => Double(value) + Triple(value);
                                  }
                              }
                              """;
        await VerifyNestedOnly.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a member the outer type also uses stays where both can see it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberTheOuterTypeAlsoUsesIsCleanAsync()
        => await VerifyNestedOnly.VerifyAnalyzerAsync(
            """
            internal class Outer
            {
                private static int Double(int value) => value * 2;

                public int Quadruple(int value) => Double(Double(value));

                internal sealed class Inner
                {
                    public int Run(int value) => Double(value);
                }
            }
            """);

    /// <summary>Verifies a member two nested types share has no single place to move to.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberSharedByTwoNestedTypesIsCleanAsync()
        => await VerifyNestedOnly.VerifyAnalyzerAsync(
            """
            internal class Outer
            {
                private static int Double(int value) => value * 2;

                internal sealed class First
                {
                    public int Run(int value) => Double(value);
                }

                internal sealed class Second
                {
                    public int Run(int value) => Double(value) + 1;
                }
            }
            """);

    /// <summary>Verifies a member carrying an attribute is left alone: something we cannot see may be using it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberWithAnAttributeIsCleanAsync()
        => await VerifyNestedOnly.VerifyAnalyzerAsync(
            """
            internal sealed class MarkAttribute : System.Attribute
            {
            }

            internal class Outer
            {
                [Mark]
                private static int Double(int value) => value * 2;

                internal sealed class Inner
                {
                    public int Run(int value) => Double(value);
                }
            }
            """);

    /// <summary>Verifies a partial type is not analyzed: a use of the member could sit in another file.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeIsCleanAsync()
        => await VerifyNestedOnly.VerifyAnalyzerAsync(
            """
            internal partial class Outer
            {
                private static int Double(int value) => value * 2;

                internal sealed class Inner
                {
                    public int Run(int value) => Double(value);
                }
            }
            """);

    /// <summary>Verifies a member nothing uses at all is not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnusedMemberIsCleanAsync()
        => await VerifyNestedOnly.VerifyAnalyzerAsync(
            """
            internal class Outer
            {
                private static int Double(int value) => value * 2;

                public int Run(int value) => Double(value);

                internal sealed class Inner
                {
                    public int Echo(int value) => value;
                }
            }
            """);

    /// <summary>Verifies a member an extension block uses is not reported, because an extension block is not a nested type.</summary>
    /// <remarks>
    /// An extension block parses as a type declaration but declares no type — its identifier is empty, and
    /// nothing can move into it. Code written there is the enclosing static class's own code, so a private
    /// member it uses is used by the type itself.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberUsedByAnExtensionBlockIsNotReportedAsync()
    {
        var test = new VerifyNestedOnly.Test
        {
            TestCode = """
                       #nullable enable
                       using System;

                       public static class SampleExtensions
                       {
                           private static readonly Action<Exception> Rethrow = static e => throw e;

                           extension(Exception? exception)
                           {
                               public void Raise() => Rethrow(exception!);
                           }
                       }
                       """,
        };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
