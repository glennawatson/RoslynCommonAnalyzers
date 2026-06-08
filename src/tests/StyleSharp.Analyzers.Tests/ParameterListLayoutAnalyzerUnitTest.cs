// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyParameterLayout = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ParameterListLayoutAnalyzer>;
using VerifyParameterLayoutFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ParameterListLayoutAnalyzer,
    StyleSharp.Analyzers.OpeningParenOnDeclarationLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the parameter/argument layout rules (SST1110–SST1115, SST1118).</summary>
public class ParameterListLayoutAnalyzerUnitTest
{
    /// <summary>Verifies an opening parenthesis off the declaration line is reported (SST1110).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningParenOffDeclarationLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M
                    {|SST1110:(|}int x) => _ = x;
            }
            """);

    /// <summary>Verifies a closing parenthesis off the last parameter's line is reported (SST1111).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClosingParenOffLastParameterLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(
                    int x
                {|SST1111:)|} => _ = x;
            }
            """);

    /// <summary>Verifies an empty list's closing parenthesis on another line is reported (SST1112).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyListClosingParenOnOtherLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(
                {|SST1112:)|}
                {
                }
            }
            """);

    /// <summary>Verifies a comma off the previous parameter's line is reported (SST1113).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommaOffPreviousParameterLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x
                    {|SST1113:,|} int y) => _ = x + y;
            }
            """);

    /// <summary>Verifies a blank line before the first parameter is reported (SST1114).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeFirstParameterReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(

                    {|SST1114:int x|}) => _ = x;
            }
            """);

    /// <summary>Verifies a blank line before a later parameter is reported (SST1115).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeLaterParameterReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x,

                    {|SST1115:int y|}) => _ = x + y;
            }
            """);

    /// <summary>Verifies a multi-line argument is reported (SST1118, opt-in) and exempt callbacks are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineArgumentReportedButCallbacksExemptAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq;

            internal class C
            {
                private static int Add(int value) => value;

                private static void M()
                {
                    Add({|SST1118:1 +
                        2|});
                    Run(() =>
                    {
                        _ = 1;
                    });
                }

                private static void Run(Action action) => action();
            }
            """);

    /// <summary>Verifies a single-line parameter list is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineListIsCleanAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x, int y) => _ = x + y;
            }
            """);

    /// <summary>Verifies the code fix moves a method parameter list opening parenthesis onto the declaration line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningParenOffDeclarationLineFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static void M
                                      {|SST1110:(|}int x) => _ = x;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static void M(int x) => _ = x;
                                   }
                                   """;

        await VerifyParameterLayoutFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the code fix moves a bracketed argument list opening bracket onto the declaration line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningBracketOffDeclarationLineFixedAsync()
    {
        const string Source = """
                              internal static class C
                              {
                                  private static int M(int[] values)
                                      => values
                                          {|SST1110:[|}0];
                              }
                              """;
        const string FixedSource = """
                                   internal static class C
                                   {
                                       private static int M(int[] values)
                                           => values[0];
                                   }
                                   """;

        await VerifyParameterLayoutFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parenthesized callback lambda on the next argument line is not flagged as SST1110.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedCallbackArgumentOnNextLineIsCleanAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            using System;

            internal static class C
            {
                private static IDisposable Subscribe<T>(
                    this IObservable<T> source,
                    Action<T> onNext,
                    Action<Exception> onError,
                    Action onCompleted)
                    => throw new NotSupportedException();

                private static void M(IObservable<int> source)
                {
                    source.Subscribe(
                        value => { },
                        error => throw error,
                        () =>
                        {
                        });
                }
            }
            """);

    /// <summary>Verifies an expression-bodied callback lambda on the next argument line is not flagged as SST1110.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedCallbackArgumentOnNextLineIsCleanAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            internal static class C
            {
                private static IDisposable SubscribeAndDisposeOnFailureAsync(object subscription, Func<ValueTask> factory)
                    => throw new NotSupportedException();

                private static ValueTask SubscribeSourcesAsync(this object subscription, CancellationToken cancellationToken)
                    => default;

                private static IDisposable M(object subscription, CancellationToken cancellationToken)
                    => SubscribeAndDisposeOnFailureAsync(
                        subscription,
                        () => subscription.SubscribeSourcesAsync(cancellationToken));
            }
            """);

    /// <summary>Verifies the opening-bracket helper stays clean when the previous token is on the same line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningHelperRecognizesDeclarationLineAsync()
    {
        var open = ParseOpenParenToken("class C { void M(int x) { } }");
        var text = await open.SyntaxTree!.GetTextAsync();
        var openLine = LayoutHelpers.StartLine(text, open);

        await Assert.That(ParameterListLayoutAnalyzer.IsOpeningOnDeclarationLine(text, open, openLine)).IsTrue();
    }

    /// <summary>Verifies the opening-bracket helper rejects an opening token that moved to the next line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningHelperRejectsOffLineDeclarationAsync()
    {
        var open = ParseOpenParenToken(
            """
            class C { void M
            (int x) { } }
            """);
        var text = await open.SyntaxTree!.GetTextAsync();
        var openLine = LayoutHelpers.StartLine(text, open);

        await Assert.That(ParameterListLayoutAnalyzer.IsOpeningOnDeclarationLine(text, open, openLine)).IsFalse();
    }

    /// <summary>Parses the first parameter-list opening parenthesis token from the source.</summary>
    /// <param name="source">The source containing the parameter list.</param>
    /// <returns>The opening parenthesis token.</returns>
    private static SyntaxToken ParseOpenParenToken(string source)
        => ((MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[0]).ParameterList.OpenParenToken;
}
