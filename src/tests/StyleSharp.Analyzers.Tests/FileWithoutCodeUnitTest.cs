// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFileWithoutCode = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1533FileWithoutCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the file-without-code rule (SST1533).</summary>
public class FileWithoutCodeUnitTest
{
    /// <summary>The editorconfig content enabling the opt-in rule.</summary>
    private const string EnableConfig = """
        root = true
        [*.cs]
        dotnet_diagnostic.SST1533.severity = warning

        """;

    /// <summary>Verifies a file of only usings is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingsOnlyFileReportedAsync()
    {
        var test = new VerifyFileWithoutCode.Test { TestCode = "{|SST1533:using System;|}\n" };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file of only comments is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentOnlyFileReportedAsync()
    {
        var test = new VerifyFileWithoutCode.Test { TestCode = "{|SST1533:// nothing here anymore|}\n" };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a polyfill whose type is compiled out for this framework is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The file declares its type on the frameworks that lack the API and looks empty on the ones that
    /// already have it. Reporting it here asks for a deletion that breaks every other framework built.
    /// </remarks>
    [Test]
    public async Task PolyfillCompiledOutForThisFrameworkIsCleanAsync()
    {
        var test = new VerifyFileWithoutCode.Test
        {
            TestCode = """
                       using System;

                       #if NETSTANDARD2_0
                       namespace System.Runtime.CompilerServices
                       {
                           internal static class IsExternalInit
                           {
                           }
                       }
                       #endif

                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file whose only content is an empty directive region is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The exemption is for a region that holds source, not for the presence of a directive.</remarks>
    [Test]
    public async Task EmptyDirectiveRegionIsStillReportedAsync()
    {
        var test = new VerifyFileWithoutCode.Test
        {
            TestCode = """
                       {|SST1533:using System;|}

                       #if NETSTANDARD2_0
                       #endif

                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a file that declares a type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileWithTypeIsCleanAsync()
    {
        var test = new VerifyFileWithoutCode.Test
        {
            TestCode = """
                       internal class C
                       {
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a whitespace-only file is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhitespaceOnlyFileIsCleanAsync()
    {
        var test = new VerifyFileWithoutCode.Test { TestCode = "   \n" };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EnableConfig));
        await test.RunAsync(CancellationToken.None);
    }
}
