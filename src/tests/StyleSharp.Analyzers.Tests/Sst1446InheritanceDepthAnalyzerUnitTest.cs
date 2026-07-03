// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1446InheritanceDepthAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1446InheritanceDepthAnalyzer"/> (SST1446 inheritance depth).</summary>
public class Sst1446InheritanceDepthAnalyzerUnitTest
{
    /// <summary>Verifies a chain deeper than the default maximum flags the deepest class.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ChainOverDefaultMaximumIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C0
            {
            }

            public class C1 : C0
            {
            }

            public class C2 : C1
            {
            }

            public class C3 : C2
            {
            }

            public class C4 : C3
            {
            }

            public class C5 : C4
            {
            }

            public class {|SST1446:C6|} : C5
            {
            }
            """);

    /// <summary>Verifies a chain at the default maximum is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ChainAtDefaultMaximumIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C0
            {
            }

            public class C1 : C0
            {
            }

            public class C2 : C1
            {
            }

            public class C3 : C2
            {
            }

            public class C4 : C3
            {
            }

            public class C5 : C4
            {
            }
            """);

    /// <summary>Verifies framework ancestors are free by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FrameworkAncestorsAreFreeByDefaultAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class MyException : System.ArgumentException
            {
            }

            public class MySpecificException : MyException
            {
            }
            """);

    /// <summary>Verifies the maximum is configurable per tree.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MaximumIsConfigurableAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       public class C0
                       {
                       }

                       public class C1 : C0
                       {
                       }

                       public class {|SST1446:C2|} : C1
                       {
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1446.max_inheritance_depth = 1

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies external ancestors count when configured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExternalAncestorsCountWhenConfiguredAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       public class {|SST1446:MyException|} : System.ArgumentException
                       {
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1446.max_inheritance_depth = 2
            stylesharp.SST1446.count_external_types = true

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
