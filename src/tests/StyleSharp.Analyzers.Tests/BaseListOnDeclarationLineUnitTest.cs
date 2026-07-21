// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBaseList = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1530BaseListOnDeclarationLineAnalyzer,
    StyleSharp.Analyzers.Sst1530BaseListOnDeclarationLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the base-list-on-its-own-line rule (SST1530).</summary>
public class BaseListOnDeclarationLineUnitTest
{
    /// <summary>Verifies a base list on its own line is reported and joined to the declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseListJoinedToDeclarationAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1530.severity = warning

            """;
        var test = new VerifyBaseList.Test
        {
            TestCode = """
                       internal interface IMarker
                       {
                       }

                       internal class Foo
                           {|SST1530:: IMarker|}
                       {
                       }
                       """,
            FixedCode = """
                        internal interface IMarker
                        {
                        }

                        internal class Foo : IMarker
                        {
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a base list already on the declaration line is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseListOnDeclarationLineIsCleanAsync()
    {
        var test = new VerifyBaseList.Test
        {
            TestCode = """
                       internal interface IMarker
                       {
                       }

                       internal class Foo : IMarker
                       {
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1530.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a base list is not reported when joining it would exceed the line limit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseListThatWouldNotFitIsCleanAsync()
    {
        var test = new VerifyBaseList.Test
        {
            TestCode = """
                       internal interface IMarker
                       {
                       }

                       internal class Foo
                           : IMarker
                       {
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1530.severity = warning
            stylesharp.max_line_length = 20

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
