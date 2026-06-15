// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using VerifySpacing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.SpacingAnalyzer,
    StyleSharp.Analyzers.SpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the trivia spacing rules (SST1005/SST1025/SST1027/SST1028).</summary>
public class SpacingAnalyzerUnitTest
{
    /// <summary>Verifies a single-line comment without a leading space is reported (SST1005) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentWithoutSpaceFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1005://comment|}
                                  private int x;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       // comment
                                       private int x;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All inserts the missing space after every <c>//</c> in one pass (SST1005).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1005://first|}
                                  private int x;

                                  {|SST1005://second|}
                                  private int y;

                                  {|SST1005://third|}
                                  private int z;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       // first
                                       private int x;

                                       // second
                                       private int y;

                                       // third
                                       private int z;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies trailing whitespace is reported (SST1028) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingWhitespaceRemovedAsync()
    {
        const string Source = """
                              internal class C 
                              {
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                   }
                                   """;

        await VerifySpacing.VerifyCodeFixAsync(
            Source,
            VerifySpacing.Diagnostic("SST1028").WithSpan(1, 17, 1, 18),
            FixedSource);
    }

    /// <summary>Verifies a tab in indentation is reported (SST1027) and replaced with spaces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TabReplacedWithSpacesAsync()
    {
        var source = $$"""
                      internal class C
                      {
                      {{'\t'}}private int x;
                      }
                      """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int x;
                                   }
                                   """;

        await VerifySpacing.VerifyCodeFixAsync(
            source,
            VerifySpacing.Diagnostic("SST1027").WithSpan(3, 1, 3, 2),
            FixedSource);
    }

    /// <summary>Verifies multiple whitespace between tokens is reported (SST1025) and collapsed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleWhitespaceCollapsedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private  int x;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int x;
                                   }
                                   """;

        await VerifySpacing.VerifyCodeFixAsync(
            Source,
            VerifySpacing.Diagnostic("SST1025").WithSpan(3, 12, 3, 14),
            FixedSource);
    }

    /// <summary>Verifies a space before a comma is reported (SST1001) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeCommaRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(int a, int b)
                                  {
                                      Combine(a {|SST1001:,|} b);
                                  }

                                  private void Combine(int x, int y)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(int a, int b)
                                       {
                                           Combine(a, b);
                                       }

                                       private void Combine(int x, int y)
                                       {
                                       }
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comma not followed by a space is reported (SST1001) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingSpaceAfterCommaAddedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(int a, int b)
                                  {
                                      Combine(a{|SST1001:,|}b);
                                  }

                                  private void Combine(int x, int y)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(int a, int b)
                                       {
                                           Combine(a, b);
                                       }

                                       private void Combine(int x, int y)
                                       {
                                       }
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a semicolon is reported (SST1002) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeSemicolonRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M()
                                  {
                                      System.Console.WriteLine() {|SST1002:;|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M()
                                       {
                                           System.Console.WriteLine();
                                       }
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space around a member-access dot is reported (SST1019) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAroundMemberDotRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private string M(string x) => x {|SST1019:.|}ToString();
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private string M(string x) => x.ToString();
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a nullable question mark is reported (SST1018) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeNullableRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int {|SST1018:?|} value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int? value;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before an opening generic bracket is reported (SST1014) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeGenericBracketRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private System.Collections.Generic.List {|SST1014:<|}int> M() => null;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private System.Collections.Generic.List<int> M() => null;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a closing generic bracket is reported (SST1015) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeClosingGenericBracketRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private System.Collections.Generic.List<int {|SST1015:>|} M() => null;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private System.Collections.Generic.List<int> M() => null;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space inside an opening attribute bracket is reported (SST1016) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterAttributeBracketRemovedAsync()
    {
        const string Source = """
                              [ {|SST1016:System|}.Obsolete]
                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   [System.Obsolete]
                                   internal class C
                                   {
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a closing attribute bracket is reported (SST1017) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeClosingAttributeBracketRemovedAsync()
    {
        const string Source = """
                              [System.Obsolete {|SST1017:]|}
                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   [System.Obsolete]
                                   internal class C
                                   {
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before the bracket of an implicit array is reported (SST1026) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeImplicitArrayBracketRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int[] M() => new {|SST1026:[|}] { 1 };
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int[] M() => new[] { 1 };
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an 'operator' keyword not followed by a space is reported (SST1007) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorKeywordSpacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public static C {|SST1007:operator|}+(C a, C b) => a;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public static C operator +(C a, C b) => a;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a closing square bracket is reported (SST1011) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeClosingBracketRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(int[] arr, int i) => arr[i {|SST1011:]|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(int[] arr, int i) => arr[i];
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a postfix increment is reported (SST1020) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeIncrementRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(int i) => i {|SST1020:++|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(int i) => i++;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space after a unary negative sign is reported (SST1021) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterNegativeSignRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(int x) => - {|SST1021:x|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(int x) => -x;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space after a unary positive sign is reported (SST1022) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterPositiveSignRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(int x) => + {|SST1022:x|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(int x) => +x;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a control keyword not followed by a space is reported (SST1000) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeywordBeforeParenSpacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(bool x)
                                  {
                                      {|SST1000:if|}(x)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(bool x)
                                       {
                                           if (x)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a binary operator missing a space is reported (SST1003) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinaryOperatorSpacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(int a, int b) => a{|SST1003:+|} b;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(int a, int b) => a + b;
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space after an opening parenthesis is reported (SST1008) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterOpeningParenRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static int N(int x) => x;

                                  private int M(int x) => N( {|SST1008:x|});
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static int N(int x) => x;

                                       private int M(int x) => N(x);
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a space before a closing parenthesis is reported (SST1009) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeClosingParenRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static int N(int x) => x;

                                  private int M(int x) => N(x {|SST1009:)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static int N(int x) => x;

                                       private int M(int x) => N(x);
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a missing space after an opening brace is reported (SST1012) and added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterOpeningBraceAddedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int[] M() => new[] {|SST1012:{|}1 };
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int[] M() => new[] { 1 };
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a missing space before a closing brace is reported (SST1013) and added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeClosingBraceAddedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int[] M() => new[] { 1{|SST1013:}|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int[] M() => new[] { 1 };
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies interpolation braces are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolationBracesAreCleanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string M(int x) => $"{x}";
            }
            """);

    /// <summary>Verifies a base-list colon missing a leading space is reported (SST1024) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseListColonSpacedAsync()
    {
        const string Source = """
                              internal class B
                              {
                              }

                              internal class C{|SST1024::|} B
                              {
                              }
                              """;
        const string FixedSource = """
                                   internal class B
                                   {
                                   }

                                   internal class C : B
                                   {
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a named-argument colon preceded by a space is reported (SST1024) and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentColonSpacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static int N(int x) => x;

                                  private int M() => N(x {|SST1024::|} 1);
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static int N(int x) => x;

                                       private int M() => N(x: 1);
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a correctly spaced base-list colon is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectBaseListColonIsCleanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            internal class B
            {
            }

            internal class C : B
            {
            }
            """);

    /// <summary>Verifies a space before an opening square bracket is reported (SST1010, opt-in) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforeOpeningBracketRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(int[] arr) => arr {|SST1010:[|}0];
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(int[] arr) => arr[0];
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies SST1010 (enabled) leaves the outer space before a collection-expression '[' alone (the C# 12 'x = [1, 2]' style).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionOuterSpaceAllowedAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int[] Field = [1, 2, 3];
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies SST1010 (enabled) leaves the outer space before a list-pattern '[' alone ('x is [1, 2]').</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListPatternOuterSpaceAllowedAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static bool M(int[] a) => a is [1, 2];
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies that by default (tight) inner padding in a collection expression is reported and removed, while the outer space is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionInnerSpaceRemovedByDefaultAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int[] Field = [ {|SST1010:1|}, 2 {|SST1011:]|};
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static readonly int[] Field = [1, 2];
                        }
                        """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies that with collection_expression_spacing = space, a padded collection expression produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionPaddedAllowedAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int[] Field = [ 1, 2, 3 ];
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning
            stylesharp.collection_expression_spacing = space

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies that with collection_expression_spacing = space, a tight collection expression is reported and inner padding is added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionPaddedRequiresInnerSpaceAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int[] Field = {|SST1010:[|}1, 2, 3{|SST1011:]|};
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static readonly int[] Field = [ 1, 2, 3 ];
                        }
                        """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning
            stylesharp.collection_expression_spacing = space

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an empty collection expression stays tight ('[]') even when collection_expression_spacing = space.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyCollectionExpressionStaysTightWhenPaddedAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int[] Field = [];
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning
            stylesharp.collection_expression_spacing = space

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the collection_expression_spacing option does not affect indexer brackets, which stay tight.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexerUnaffectedByPaddedOptionAsync()
    {
        var test = new VerifySpacing.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int[] arr) => arr[ {|SST1010:0|} {|SST1011:]|};
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M(int[] arr) => arr[0];
                        }
                        """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1010.severity = warning
            stylesharp.collection_expression_spacing = space

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a documentation line abutting the '///' is reported (SST1004) and a space added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterDocExteriorAddedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1004:///|}<summary>Does something.</summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does something.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a correctly spaced documentation line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectDocExteriorIsCleanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does something.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a preprocessor keyword preceded by a space is reported (SST1006) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceBeforePreprocessorKeywordRemovedAsync()
    {
        const string Source = """
                              # {|SST1006:if|} true
                              internal class C
                              {
                              }
                              #endif
                              """;
        const string FixedSource = """
                                   #if true
                                   internal class C
                                   {
                                   }
                                   #endif
                                   """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a correctly written preprocessor directive is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectPreprocessorKeywordIsCleanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            #if true
            internal class C
            {
            }
            #endif
            """);

    /// <summary>Verifies a dereference symbol followed by a space is reported (SST1023, opt-in) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterDereferenceRemovedAsync()
    {
        const string Source = """
                              unsafe class C
                              {
                                  private static int M(int* p) => {|SST1023:*|} p;
                              }
                              """;
        const string FixedSource = """
                                   unsafe class C
                                   {
                                       private static int M(int* p) => *p;
                                   }
                                   """;
        await RunUnsafeAsync(Source, FixedSource);
    }

    /// <summary>Verifies an address-of symbol followed by a space is reported (SST1023, opt-in) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAfterAddressOfRemovedAsync()
    {
        const string Source = """
                              unsafe class C
                              {
                                  private static void M(int x)
                                  {
                                      int* p = {|SST1023:&|} x;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   unsafe class C
                                   {
                                       private static void M(int x)
                                       {
                                           int* p = &x;
                                       }
                                   }
                                   """;
        await RunUnsafeAsync(Source, FixedSource);
    }

    /// <summary>Verifies dereference and address-of symbols that touch their operand are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectPointerSymbolsAreCleanAsync()
        => await RunUnsafeAsync(
            """
            unsafe class C
            {
                private static int M(int* p)
                {
                    int* q = &*p;
                    return *q;
                }
            }
            """,
            null);

    /// <summary>Runs the spacing code-fix verifier with unsafe compilation enabled.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="fixedSource">The expected source after the code fix, or <see langword="null"/> to only verify the analyzer.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunUnsafeAsync(string source, string? fixedSource)
    {
        var test = new VerifySpacing.Test
        {
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
