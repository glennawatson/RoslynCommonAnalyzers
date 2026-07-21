// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInitializer = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1531InitializerOnSingleLineAnalyzer,
    StyleSharp.Analyzers.Sst1531InitializerOnSingleLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the collapse-short-initializer rule (SST1531).</summary>
public class InitializerOnSingleLineUnitTest
{
    /// <summary>Verifies a short multi-line object initializer is reported and collapsed onto one line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShortObjectInitializerCollapsedAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1531.severity = warning

            """;
        var test = new VerifyInitializer.Test
        {
            TestCode = """
                       internal class Point
                       {
                           public int X { get; set; }
                           public int Y { get; set; }
                       }

                       internal class C
                       {
                           private static Point Make() => new Point
                           {|SST1531:{|}
                               X = 1,
                               Y = 2,
                           };
                       }
                       """,
            FixedCode = """
                        internal class Point
                        {
                            public int X { get; set; }
                            public int Y { get; set; }
                        }

                        internal class C
                        {
                            private static Point Make() => new Point { X = 1, Y = 2, };
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an initializer carrying a comment between elements is not collapsed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerWithCommentIsCleanAsync()
    {
        var test = new VerifyInitializer.Test
        {
            TestCode = """
                       internal class Point
                       {
                           public int X { get; set; }
                           public int Y { get; set; }
                       }

                       internal class C
                       {
                           private static Point Make() => new Point
                           {
                               X = 1,

                               // keep this one out
                               Y = 2,
                           };
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1531.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an initializer that would exceed the line limit collapsed is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerThatWouldNotFitIsCleanAsync()
    {
        var test = new VerifyInitializer.Test
        {
            TestCode = """
                       internal class Point
                       {
                           public int X { get; set; }
                           public int Y { get; set; }
                       }

                       internal class C
                       {
                           private static Point Make() => new Point
                           {
                               X = 1,
                               Y = 2,
                           };
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1531.severity = warning
            stylesharp.max_line_length = 20

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
