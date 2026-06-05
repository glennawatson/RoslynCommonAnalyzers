// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FileNameAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1649 (file name should match the first type name).</summary>
public class FileNameAnalyzerUnitTest
{
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
