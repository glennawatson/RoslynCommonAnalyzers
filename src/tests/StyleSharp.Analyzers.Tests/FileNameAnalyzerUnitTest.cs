// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1649FileNameAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1649 (file name should match the first type name).</summary>
public class FileNameAnalyzerUnitTest
{
    /// <summary>Verifies paths, generic arity markers, and names without extensions use the same stem.</summary>
    /// <param name="path">The syntax tree's file path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Widget")]
    [Arguments("/source/Widget.cs")]
    [Arguments("C:\\source\\Widget.cs")]
    [Arguments("Widget`1.cs")]
    [Arguments("Widget{T}.cs")]
    [Arguments("Widget.Logic.cs")]
    public async Task FileStemMatchesNamedTypeAsync(string path)
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add((path, "namespace N { public class Widget<T> { } }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies unnamed syntax trees do not report a filename mismatch.</summary>
    /// <param name="path">An empty path or a filename with an empty stem.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments(".cs")]
    public async Task EmptyStemIsIgnoredAsync(string path)
    {
        var tree = CSharpSyntaxTree.ParseText("public class Widget { }", path: path);
        var compilation = CSharpCompilation.Create("FileName", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst1649FileNameAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies declaration traversal handles delegates, empty namespaces, and partial declarations.</summary>
    /// <param name="source">The declarations to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("namespace N { public delegate void {|SST1649:Widget|}(); }")]
    [Arguments("namespace N { }")]
    [Arguments("namespace N { partial class First { } class Second { } }")]
    [Arguments("namespace N { public class {|SST1649:First|} { } public class Second { } }")]
    public async Task FirstDeclarationControlsTheFileNameAsync(string source)
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add(("Other.cs", source));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a type whose name matches its file produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add(("Widget.cs", "public class Widget { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a partial-style suffixed file name is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuffixedFileNameAsync()
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add(("Widget.Logic.cs", "public partial class Widget { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a type whose name does not match its file is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchAsync()
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add(("Other.cs", "public class {|SST1649:Widget|} { }"));
        await test.RunAsync(CancellationToken.None);
    }
}
