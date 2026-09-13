// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using RoslynCommon.Analyzers.Tests;

using VerifyAccessor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1504AccessorConsistencyAnalyzer,
    StyleSharp.Analyzers.Sst1504AccessorConsistencyCodeFixProvider>;
using VerifyElement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1502SingleLineElementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;
using VerifyStatement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1501SingleLineStatementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the single-line layout rules (SST1501/SST1502/SST1504).</summary>
public class LayoutSingleLineUnitTest
{
    /// <summary>Source with two properties whose accessor lists each mix a single-line and a block accessor.</summary>
    private const string MultipleInconsistentAccessorsSource = """
        internal class C
        {
            private int x;
            private int y;

            public int X
            {|SST1504:{|}
                get { return x; }
                set
                {
                    x = value;
                }
            }

            public int Y
            {|SST1504:{|}
                get { return y; }
                set
                {
                    y = value;
                }
            }
        }
        """;

    /// <summary>The same two properties once every accessor has been expanded to a block.</summary>
    private const string MultipleInconsistentAccessorsFixedSource = """
        internal class C
        {
            private int x;
            private int y;

            public int X
            {
                get
                {
                    return x;
                }
                set
                {
                    x = value;
                }
            }

            public int Y
            {
                get
                {
                    return y;
                }
                set
                {
                    y = value;
                }
            }
        }
        """;

    /// <summary>Checks named bodies and operator bodies are all recognized as single-line elements.</summary>
    /// <param name="member">The member containing the collapsed body.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("C() {|SST1502:{|} M(); }")]
    [Arguments("~C() {|SST1502:{|} M(); }")]
    [Arguments("public static C operator +(C a, C b) {|SST1502:{|} return a; }")]
    [Arguments("public static implicit operator int(C value) {|SST1502:{|} return 1; }")]
    [Arguments("void N()\n{\nvoid Local() {|SST1502:{|} M(); }\nLocal();\n}")]
    public Task SingleLineMemberKindsAreReportedAsync(string member) =>
        VerifyElement.VerifyAnalyzerAsync($"class C\n{{\nstatic void M() {{ }}\n{member}\n}}");

    /// <summary>Checks type and enum bodies are reported only when they contain members on one line.</summary>
    /// <param name="source">The declaration source with its expected diagnostic.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C {|SST1502:{|} int value; }")]
    [Arguments("struct C {|SST1502:{|} int value; }")]
    [Arguments("interface C {|SST1502:{|} void M(); }")]
    [Arguments("record C {|SST1502:{|} int value; }")]
    [Arguments("record struct C {|SST1502:{|} int value; }")]
    [Arguments("enum C {|SST1502:{|} A }")]
    [Arguments("enum C {}")]
    [Arguments("class C {}")]
    [Arguments("record C;")]
    [Arguments("class C\n{\nint M() => 1;\nvoid N()\n{\nint Local() => 1;\n}\n}")]
    public Task TypeBodyContentControlsReportingAsync(string source) => VerifyElement.VerifyAnalyzerAsync(source);

    /// <summary>Checks incomplete type and enum declarations do not report missing braces.</summary>
    /// <param name="source">An unfinished declaration.</param>
    /// <returns>The verification task.</returns>
    [Test]
    [Arguments("class C")]
    [Arguments("enum C A }")]
    public async Task MissingOpeningBraceIsIgnoredAsync(string source)
    {
        var test = new VerifyElement.Test { TestCode = source, CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks an enum created without an opening brace is ignored by the layout rule.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SynthesizedEnumWithoutBraceIsIgnoredAsync()
    {
        var declaration = SyntaxFactory.EnumDeclaration("E")
            .AddMembers(SyntaxFactory.EnumMemberDeclaration("A"))
            .WithOpenBraceToken(default);
        var tree = CSharpSyntaxTree.Create(SyntaxFactory.CompilationUnit().AddMembers(declaration));
        var compilation = CSharpCompilation.Create("IncompleteEnum", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst1502SingleLineElementAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a single-line embedded block is reported (SST1501) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineEmbeddedBlockExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b) {|SST1501:{|} b = false; }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """;
        await VerifyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All expands every single-line embedded block (SST1501) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b) {|SST1501:{|} b = false; }

                    if (b) {|SST1501:{|} b = true; }

                    if (b) {|SST1501:{|} b = false; }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }

                    if (b)
                    {
                        b = true;
                    }

                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """;
        await VerifyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line embedded block is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultiLineEmbeddedBlockIsCleanAsync() =>
        VerifyStatement.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """);

    /// <summary>Verifies a single-line method body is reported (SST1502) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineMethodBodyExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M() {|SST1502:{|} System.Console.WriteLine(); }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    System.Console.WriteLine();
                }
            }
            """;
        await VerifyElement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty single-line body is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptySingleLineBodyIsCleanAsync() =>
        VerifyElement.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M() { }
            }
            """);

    /// <summary>Verifies mixed single-line and multi-line accessors are reported (SST1504) and made consistent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedAccessorsExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private int x;

                public int X
                {|SST1504:{|}
                    get { return x; }
                    set
                    {
                        x = value;
                    }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int x;

                public int X
                {
                    get
                    {
                        return x;
                    }
                    set
                    {
                        x = value;
                    }
                }
            }
            """;
        await VerifyAccessor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All makes every inconsistent accessor list in the document consistent in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixAllRewritesEveryAccessorOccurrenceAsync() =>
        VerifyAccessor.VerifyCodeFixAsync(MultipleInconsistentAccessorsSource, MultipleInconsistentAccessorsFixedSource);

    /// <summary>Verifies consistently single-line accessors are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsistentAccessorsAreCleanAsync() =>
        VerifyAccessor.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int x;

                public int X
                {
                    get { return x; }
                    set { x = value; }
                }
            }
            """);
}
