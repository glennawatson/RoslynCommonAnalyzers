// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRename = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1649FileNameAnalyzer,
    StyleSharp.Analyzers.Sst1649FileNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1649 rename-file code fix.</summary>
public class FileNameCodeFixUnitTest
{
    /// <summary>The global analyzer config selecting the backtick-arity (metadata) generic convention.</summary>
    private const string MetadataConfig = """
        is_global = true
        stylesharp.file_naming_convention = metadata

        """;

    /// <summary>Verifies the file is renamed to match its first type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileRenamedToMatchTypeAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add(("Other.cs", "public class {|SST1649:Widget|} { }"));
        test.FixedState.Sources.Add(("Widget.cs", "public class Widget { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a generic type renames the file using the brace convention by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFileRenamedWithBraceConventionAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add(("Other.cs", "public class {|SST1649:Widget|}<T> { }"));
        test.FixedState.Sources.Add(("Widget{T}.cs", "public class Widget<T> { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the metadata convention renames a generic type's file with backtick arity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFileRenamedWithMetadataConventionAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add(("Other.cs", "public class {|SST1649:Widget|}<TKey, TValue> { }"));
        test.FixedState.Sources.Add(("Widget`2.cs", "public class Widget<TKey, TValue> { }"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All renames every misnamed file in the scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRenamesEveryFileAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add(("WrongA.cs", "public class {|SST1649:Apple|} { }"));
        test.TestState.Sources.Add(("WrongB.cs", "public class {|SST1649:Banana|} { }"));
        test.FixedState.Sources.Add(("Apple.cs", "public class Apple { }"));
        test.FixedState.Sources.Add(("Banana.cs", "public class Banana { }"));
        await test.RunAsync(CancellationToken.None);
    }
}
