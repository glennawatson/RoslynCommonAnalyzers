// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNamespace = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.NamespaceFolderAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1417 (namespace should match the folder structure).</summary>
public class NamespaceFolderAnalyzerUnitTest
{
    /// <summary>A global analyzer config supplying the project directory and root namespace.</summary>
    private const string GlobalConfig = """
        is_global = true
        build_property.ProjectDir = /src/MyApp/
        build_property.RootNamespace = MyApp

        """;

    /// <summary>Verifies a namespace that does not match the folder structure is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedNamespaceReportedAsync()
    {
        const string Source = """
                              namespace {|SST1417:MyApp.Wrong|}
                              {
                                  public class Widget { }
                              }
                              """;

        var test = new VerifyNamespace.Test();
        test.TestState.Sources.Add(("/src/MyApp/Models/Widget.cs", Source));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a namespace that matches the folder structure is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingNamespaceIsCleanAsync()
    {
        const string Source = """
                              namespace MyApp.Models
                              {
                                  public class Widget { }
                              }
                              """;

        var test = new VerifyNamespace.Test();
        test.TestState.Sources.Add(("/src/MyApp/Models/Widget.cs", Source));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));

        await test.RunAsync(CancellationToken.None);
    }
}
