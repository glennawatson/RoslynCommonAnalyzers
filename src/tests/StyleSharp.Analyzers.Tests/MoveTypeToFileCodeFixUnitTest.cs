// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMove = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FileTypeNamespaceAnalyzer,
    StyleSharp.Analyzers.Sst1402MoveTypeToFileCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1402 move-type-to-file code fix.</summary>
public class MoveTypeToFileCodeFixUnitTest
{
    /// <summary>The global analyzer config selecting the backtick-arity (metadata) generic convention.</summary>
    private const string MetadataConfig = """
        is_global = true
        stylesharp.file_naming_convention = metadata

        """;

    /// <summary>Verifies a second top-level type is moved to its own file.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondTypeMovedToOwnFileAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add(("First.cs", """
            public class First
            {
            }

            public class {|SST1402:Second|}
            {
            }
            """));
        test.FixedState.Sources.Add(("First.cs", """
            public class First
            {
            }
            """));
        test.FixedState.Sources.Add(("Second.cs", """
            public class Second
            {
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the moved type keeps its enclosing namespace and the new file is named for the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeInNamespaceKeepsNamespaceAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add(("First.cs", """
            namespace N
            {
                public class First
                {
                }

                public class {|SST1402:Second|}
                {
                }
            }
            """));
        test.FixedState.Sources.Add(("First.cs", """
            namespace N
            {
                public class First
                {
                }
            }
            """));
        test.FixedState.Sources.Add(("Second.cs", """
            namespace N
            {
                public class Second
                {
                }
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a moved generic type uses the brace convention for its file name by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTypeUsesBraceConventionAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add(("First.cs", """
            public class First
            {
            }

            public class {|SST1402:Widget|}<T>
            {
            }
            """));
        test.FixedState.Sources.Add(("First.cs", """
            public class First
            {
            }
            """));
        test.FixedState.Sources.Add(("Widget{T}.cs", """
            public class Widget<T>
            {
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the metadata convention names a moved generic type's file with backtick arity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTypeUsesMetadataConventionAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add(("First.cs", """
            public class First
            {
            }

            public class {|SST1402:Widget|}<TKey, TValue>
            {
            }
            """));
        test.FixedState.Sources.Add(("First.cs", """
            public class First
            {
            }
            """));
        test.FixedState.Sources.Add(("Widget`2.cs", """
            public class Widget<TKey, TValue>
            {
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        await test.RunAsync(CancellationToken.None);
    }
}
