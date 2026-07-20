// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeMaxDepth = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1403JsonMaxDepthAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1403 (a JSON deserialization depth limit must stay within a safe ceiling).</summary>
public class JsonMaxDepthAnalyzerUnitTest
{
    /// <summary>Verifies an object-initializer <c>MaxDepth</c> above the ceiling on JsonSerializerOptions is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerMaxDepthOnSerializerOptionsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonSerializerOptions M()
                    => new JsonSerializerOptions { MaxDepth = {|SES1403:5000|} };
            }
            """);

    /// <summary>Verifies a plain assignment of <c>MaxDepth</c> above the ceiling is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentMaxDepthOnSerializerOptionsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public void M(JsonSerializerOptions options)
                    => options.MaxDepth = {|SES1403:5000|};
            }
            """);

    /// <summary>Verifies a raised <c>MaxDepth</c> on the JsonReaderOptions struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerMaxDepthOnReaderOptionsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonReaderOptions M()
                    => new JsonReaderOptions { MaxDepth = {|SES1403:1024|} };
            }
            """);

    /// <summary>Verifies a raised <c>MaxDepth</c> on the JsonDocumentOptions struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerMaxDepthOnDocumentOptionsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonDocumentOptions M()
                    => new JsonDocumentOptions { MaxDepth = {|SES1403:500|} };
            }
            """);

    /// <summary>Verifies a <c>MaxDepth</c> from a <c>const</c> field (a compile-time constant) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstFieldMaxDepthReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                private const int Depth = 200;

                public JsonSerializerOptions M()
                    => new JsonSerializerOptions { MaxDepth = {|SES1403:Depth|} };
            }
            """);

    /// <summary>Verifies a <c>MaxDepth</c> at the default ceiling is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaxDepthAtCeilingCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonSerializerOptions M()
                    => new JsonSerializerOptions { MaxDepth = 64 };
            }
            """);

    /// <summary>Verifies a <c>MaxDepth</c> below the default ceiling is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaxDepthBelowCeilingCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonSerializerOptions M()
                    => new JsonSerializerOptions { MaxDepth = 32 };
            }
            """);

    /// <summary>Verifies a <c>MaxDepth</c> of 0 (the framework-default sentinel) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaxDepthZeroCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonSerializerOptions M()
                    => new JsonSerializerOptions { MaxDepth = 0 };
            }
            """);

    /// <summary>Verifies a non-constant <c>MaxDepth</c> (a method parameter) stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantMaxDepthCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public JsonSerializerOptions M(int depth)
                    => new JsonSerializerOptions { MaxDepth = depth };
            }
            """);

    /// <summary>Verifies an assignment to a non-<c>MaxDepth</c> property on the option type stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedPropertyAssignmentCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Text.Json;

            public class C
            {
                public void M(JsonSerializerOptions options)
                    => options.WriteIndented = true;
            }
            """);

    /// <summary>Verifies a raised <c>MaxDepth</c> on a type other than the JSON option types stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaxDepthOnUnrelatedTypeCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Parser
            {
                public int MaxDepth { get; set; }
            }

            public class C
            {
                public void M(Parser parser)
                    => parser.MaxDepth = 5000;
            }
            """);

    /// <summary>Verifies an assignment to a local named <c>MaxDepth</c> (not a property) stays silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalNamedMaxDepthCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public int M()
                {
                    int MaxDepth = 0;
                    MaxDepth = 5000;
                    return MaxDepth;
                }
            }
            """);

    /// <summary>Verifies a lowered rule-specific ceiling reports a depth the default would have allowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoweredRuleCeilingReportsMidDepthAsync()
    {
        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Text.Json;

                       public class C
                       {
                           public JsonSerializerOptions M()
                               => new JsonSerializerOptions { MaxDepth = {|SES1403:32|} };
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1403.maxdepth = 16

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a raised project-wide ceiling allows a depth the default would flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RaisedProjectWideCeilingAllowsDepthAsync()
    {
        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Text.Json;

                       public class C
                       {
                           public JsonSerializerOptions M()
                               => new JsonSerializerOptions { MaxDepth = 128 };
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.maxdepth = 256

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable ceiling value falls back to the default and still reports a raised depth.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparsableCeilingFallsBackToDefaultAsync()
    {
        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Text.Json;

                       public class C
                       {
                           public JsonSerializerOptions M()
                               => new JsonSerializerOptions { MaxDepth = {|SES1403:128|} };
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1403.maxdepth = not-a-number

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a nonsensical ceiling (below 1) falls back to the default and still reports a raised depth.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonsensicalCeilingFallsBackToDefaultAsync()
    {
        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Text.Json;

                       public class C
                       {
                           public JsonSerializerOptions M()
                               => new JsonSerializerOptions { MaxDepth = {|SES1403:128|} };
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1403.maxdepth = 0

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent on a framework without <c>System.Text.Json</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenJsonUnavailableAsync()
    {
        const string Source = """
                              public sealed class JsonSerializerOptions
                              {
                                  public int MaxDepth { get; set; }
                              }

                              public class C
                              {
                                  public JsonSerializerOptions M()
                                      => new JsonSerializerOptions { MaxDepth = 5000 };
                              }
                              """;

        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the JSON option types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeMaxDepth.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
