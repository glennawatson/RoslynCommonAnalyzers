// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOverloads = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1218OverloadsGroupedAnalyzer,
    StyleSharp.Analyzers.Sst1218OverloadsGroupedCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1218 (method overloads should be grouped together).</summary>
public class OverloadsGroupedAnalyzerUnitTest
{
    /// <summary>Verifies a separated overload is reported and moved back beside its family.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedOverloadIsMovedBackAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Write(int value)
                                  {
                                  }

                                  public void Flush()
                                  {
                                  }

                                  public void {|SST1218:Write|}(string value)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Write(int value)
                                       {
                                       }

                                       public void Write(string value)
                                       {
                                       }

                                       public void Flush()
                                       {
                                       }
                                   }
                                   """;
        await VerifyOverloads.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the moved overload keeps its documentation comment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MovedOverloadKeepsItsDocumentationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  /// <summary>Writes a number.</summary>
                                  /// <param name="value">The number.</param>
                                  public void Write(int value)
                                  {
                                  }

                                  /// <summary>Flushes.</summary>
                                  public void Flush()
                                  {
                                  }

                                  /// <summary>Writes text.</summary>
                                  /// <param name="value">The text.</param>
                                  public void {|SST1218:Write|}(string value)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       /// <summary>Writes a number.</summary>
                                       /// <param name="value">The number.</param>
                                       public void Write(int value)
                                       {
                                       }

                                       /// <summary>Writes text.</summary>
                                       /// <param name="value">The text.</param>
                                       public void Write(string value)
                                       {
                                       }

                                       /// <summary>Flushes.</summary>
                                       public void Flush()
                                       {
                                       }
                                   }
                                   """;
        await VerifyOverloads.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies overloads that already sit together, with only comments between them, are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentOverloadsAreCleanAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Write(int value)
                {
                }

                // Text goes through the same path.
                public void Write(string value)
                {
                }

                public void Flush()
                {
                }
            }
            """);

    /// <summary>Verifies overloads the ordering rules place apart — a different accessibility, a different static-ness — are not compared.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Reporting these would demand a move that the member-ordering rules would then demand back.</remarks>
    [Test]
    public async Task OverloadsInDifferentOrderingGroupsAreCleanAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public class C
            {
                public static void Write(int value)
                {
                }

                public void Write(string value)
                {
                }

                public void Flush()
                {
                }

                private void Write(bool value)
                {
                }
            }
            """);

    /// <summary>Verifies an explicit interface implementation follows its interface, not the name it shares.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsCleanAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public interface IWriter
            {
                void Write(int value);
            }

            public class C : IWriter
            {
                public void Write(string value)
                {
                }

                public void Flush()
                {
                }

                void IWriter.Write(int value)
                {
                }
            }
            """);

    /// <summary>Verifies constructors are not measured: they already carry the type's name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorsAreNotMeasuredAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public class C
            {
                public C(int value)
                {
                }

                public void Flush()
                {
                }

                public C(string value)
                {
                }
            }
            """);

    /// <summary>Verifies each part of a partial type is judged on its own members.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypePartsAreJudgedSeparatelyAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                public void Write(int value)
                {
                }

                public void Flush()
                {
                }
            }

            public partial class C
            {
                public void Write(string value)
                {
                }
            }
            """);

    /// <summary>Verifies a family scattered into three places is gathered back at its first member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ScatteredFamilyIsGatheredAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Write(int value)
                                  {
                                  }

                                  public void Flush()
                                  {
                                  }

                                  public void {|SST1218:Write|}(string value)
                                  {
                                  }

                                  public void Close()
                                  {
                                  }

                                  public void {|SST1218:Write|}(bool value)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Write(int value)
                                       {
                                       }

                                       public void Write(string value)
                                       {
                                       }

                                       public void Write(bool value)
                                       {
                                       }

                                       public void Flush()
                                       {
                                       }

                                       public void Close()
                                       {
                                       }
                                   }
                                   """;
        await VerifyOverloads.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member of any kind between two overloads separates them.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyBetweenOverloadsSeparatesThemAsync()
        => await VerifyOverloads.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Write(int value)
                {
                }

                public int Count { get; set; }

                public void {|SST1218:Write|}(string value)
                {
                }
            }
            """);

    /// <summary>Verifies a type whose members carry a directive is reported but not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A member carries its trivia when it moves, and moving one past a directive would unbalance it.</remarks>
    [Test]
    public async Task DirectiveInTypeSuppressesTheFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Write(int value)
                                  {
                                  }

                                  #region Plumbing
                                  public void Flush()
                                  {
                                  }
                                  #endregion

                                  public void {|SST1218:Write|}(string value)
                                  {
                                  }
                              }
                              """;
        await VerifyOverloads.VerifyCodeFixAsync(Source, Source);
    }
}
