// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDocumentationElementOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1666DocumentationElementOrderAnalyzer,
    StyleSharp.Analyzers.Sst1666DocumentationElementOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1666DocumentationElementOrderAnalyzer"/> and its code fix (SST1666).</summary>
public class DocumentationElementOrderAnalyzerUnitTest
{
    /// <summary>Verifies a returns element written before a param is reported and moved after it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnsBeforeParamIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does the thing.</summary>
                                  /// <returns>The result.</returns>
                                  /// {|SST1666:<param name="value">The value.</param>|}
                                  public int M(int value) => value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <param name="value">The value.</param>
                                       /// <returns>The result.</returns>
                                       public int M(int value) => value;
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a summary written after a param is moved to the front.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SummaryAfterParamIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <param name="value">The value.</param>
                                  /// {|SST1666:<summary>Does the thing.</summary>|}
                                  public void M(int value)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <param name="value">The value.</param>
                                       public void M(int value)
                                       {
                                       }
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type parameter written after a parameter is moved ahead of it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParamAfterParamIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does the thing.</summary>
                                  /// <param name="value">The value.</param>
                                  /// {|SST1666:<typeparam name="T">The type.</typeparam>|}
                                  /// <returns>The result.</returns>
                                  public T M<T>(int value) => default!;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <typeparam name="T">The type.</typeparam>
                                       /// <param name="value">The value.</param>
                                       /// <returns>The result.</returns>
                                       public T M<T>(int value) => default!;
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies remarks written before returns are moved after it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RemarksBeforeReturnsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does the thing.</summary>
                                  /// <remarks>Some background.</remarks>
                                  /// {|SST1666:<returns>The result.</returns>|}
                                  public int M() => 1;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <returns>The result.</returns>
                                       /// <remarks>Some background.</remarks>
                                       public int M() => 1;
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an exception element written before returns is moved after it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionBeforeReturnsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does the thing.</summary>
                                  /// <exception cref="InvalidOperationException">Always.</exception>
                                  /// {|SST1666:<returns>The result.</returns>|}
                                  public int M() => throw new InvalidOperationException();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <returns>The result.</returns>
                                       /// <exception cref="InvalidOperationException">Always.</exception>
                                       public int M() => throw new InvalidOperationException();
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies parameters of the same kind keep their relative order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameKindElementsKeepTheirOrderAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <returns>The result.</returns>
                                  /// {|SST1666:<param name="second">The second.</param>|}
                                  /// <param name="first">The first.</param>
                                  /// <summary>Does the thing.</summary>
                                  public int M(int first, int second) => first + second;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the thing.</summary>
                                       /// <param name="second">The second.</param>
                                       /// <param name="first">The first.</param>
                                       /// <returns>The result.</returns>
                                       public int M(int first, int second) => first + second;
                                   }
                                   """;
        await VerifyDocumentationElementOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment already in the conventional order is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionalOrderIsCleanAsync()
        => await VerifyDocumentationElementOrder.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                /// <summary>Does the thing.</summary>
                /// <typeparam name="T">The type.</typeparam>
                /// <param name="value">The value.</param>
                /// <returns>The result.</returns>
                /// <exception cref="InvalidOperationException">Never.</exception>
                /// <remarks>Some background.</remarks>
                public T M<T>(int value) => default!;
            }
            """);

    /// <summary>Verifies an unranked element is left wherever it was written.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrankedElementIsCleanAsync()
        => await VerifyDocumentationElementOrder.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <inheritdoc/>
                /// <summary>Does the thing.</summary>
                /// <returns>The result.</returns>
                public int M() => 1;
            }
            """);

    /// <summary>Verifies a comment with one element is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleElementIsCleanAsync()
        => await VerifyDocumentationElementOrder.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does the thing.</summary>
                public int M() => 1;
            }
            """);
}
