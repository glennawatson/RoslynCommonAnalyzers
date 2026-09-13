// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNamespace = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1417NamespaceFolderAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1417 (namespace should match the folder structure).</summary>
public class NamespaceFolderAnalyzerUnitTest
{
    /// <summary>The global configuration file shared by the path tests.</summary>
    private const string GlobalConfigFileName = "/.globalconfig";

    /// <summary>The source path for the model namespace tests.</summary>
    private const string ModelFilePath = "/src/MyApp/Models/Widget.cs";

    /// <summary>A global analyzer config supplying the project directory and root namespace.</summary>
    private const string GlobalConfig = """
        is_global = true
        build_property.ProjectDir = /src/MyApp/
        build_property.RootNamespace = MyApp

        """;

    /// <summary>Checks folder validation, project boundaries, and both directory separator conventions.</summary>
    /// <param name="projectDirectory">The configured project directory, or null to omit it.</param>
    /// <param name="filePath">The source file path.</param>
    /// <param name="rootNamespace">The build namespace, or null to omit it.</param>
    /// <param name="namespaceName">The declared namespace, with diagnostic markup where expected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, "/src/MyApp/Models/Widget.cs", "MyApp", "Wrong")]
    [Arguments("", "/src/MyApp/Models/Widget.cs", "MyApp", "Wrong")]
    [Arguments("/src/MyApp/", "/a.cs", "MyApp", "Wrong")]
    [Arguments("/src/MyApp/", "/src/Other/Widget.cs", "MyApp", "Wrong")]
    [Arguments("/src/MyApp", "/src/MyApplication/Widget.cs", "MyApp", "Wrong")]
    [Arguments("/src/MyApp", "/src/MyApp/Widget.cs", "MyApp", "MyApp")]
    [Arguments("/src/MyApp/", "/src/MyApp/Widget.cs", null, "Wrong")]
    [Arguments("/src/MyApp/", "/src/MyApp/Models/Widget.cs", null, "Models")]
    [Arguments("/src/MyApp/", "/src/MyApp/Models/Parts/Widget.cs", "MyApp", "MyApp.Models.Parts")]
    [Arguments("/src/MyApp/", "/src/MyApp/Models//Parts/Widget.cs", "MyApp", "MyApp.Models.Parts")]
    [Arguments("/src/MyApp/", "/src/MyApp/_Models/A_1/Widget.cs", "MyApp", "MyApp._Models.A_1")]
    [Arguments("/src/MyApp/", "/src/MyApp/1Models/Widget.cs", "MyApp", "Wrong")]
    [Arguments("/src/MyApp/", "/src/MyApp/Bad-Name/Widget.cs", "MyApp", "Wrong")]
    [Arguments("C:\\src\\MyApp\\", "C:\\src\\MyApp\\Models\\Widget.cs", "MyApp", "{|SST1417:Wrong|}")]
    [Arguments("C:\\src\\MyApp", "C:/src/MyApp/Models/Widget.cs", "MyApp", "MyApp.Models")]
    [Arguments("C:/src/MyApp/", "C:\\src\\MyApp\\Models\\Widget.cs", "MyApp", "MyApp.Models")]
    [Arguments("/src/MyApp/", "/src/MyApp/Models/Widget.cs", "MyApp", "{|SST1417:Wrong|}")]
    public async Task FolderAndProjectBoundariesAreRespectedAsync(string? projectDirectory, string filePath, string? rootNamespace, string namespaceName)
    {
        var test = new VerifyNamespace.Test();
        test.TestState.Sources.Add((filePath, $"namespace {namespaceName}; public class Widget {{ }}"));
        var config = "is_global = true\n";
        if (projectDirectory is not null)
        {
            config += $"build_property.ProjectDir = {projectDirectory}\n";
        }

        if (rootNamespace is not null)
        {
            config += $"build_property.RootNamespace = {rootNamespace}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks namespace overrides and fallback when either override is empty.</summary>
    /// <param name="specific">The rule-specific override.</param>
    /// <param name="general">The general override.</param>
    /// <param name="expected">The expected namespace root.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Specific", "General", "Specific")]
    [Arguments("", "General", "General")]
    [Arguments("", "", "MyApp")]
    public async Task NamespaceOverridePrecedenceIsRespectedAsync(string specific, string general, string expected)
    {
        var test = new VerifyNamespace.Test();
        test.TestState.Sources.Add((ModelFilePath, $"namespace {expected}.Models {{ public class Widget {{ }} }}"));
        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, GlobalConfig));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"root = true\n[*.cs]\nstylesharp.SST1417.namespace_root = {specific}\nstylesharp.namespace_root = {general}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks nested namespace parts are not independently compared with the folder.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedNamespaceIsIgnoredAsync()
    {
        var test = new VerifyNamespace.Test();
        test.TestState.Sources.Add(("/src/MyApp/Widget.cs", "namespace MyApp { namespace Nested { public class Widget { } } }"));
        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, GlobalConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks an in-memory document without a file path cannot infer a folder namespace.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFilePathIsIgnoredAsync()
    {
        var test = new VerifyNamespace.Test { TestCode = "namespace Wrong { public class Widget { } }" };
        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, GlobalConfig));
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithDocumentFilePath(solution.GetProject(projectId)!.DocumentIds[0], string.Empty));
        await test.RunAsync(CancellationToken.None);
    }

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
        test.TestState.Sources.Add((ModelFilePath, Source));
        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, GlobalConfig));

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
        test.TestState.Sources.Add((ModelFilePath, Source));
        test.TestState.AnalyzerConfigFiles.Add((GlobalConfigFileName, GlobalConfig));

        await test.RunAsync(CancellationToken.None);
    }
}
