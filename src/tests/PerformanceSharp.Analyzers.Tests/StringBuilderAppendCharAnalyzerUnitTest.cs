// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using RoslynCommon.Analyzers.Tests;

using AnalyzerVerifyAppendChar = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1202StringBuilderAppendCharAnalyzer>;

using VerifyAppendChar = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1202StringBuilderAppendCharAnalyzer,
    PerformanceSharp.Analyzers.Psh1202StringBuilderAppendCharCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1202 (StringBuilder should append single characters as char) and its code fix.</summary>
public class StringBuilderAppendCharAnalyzerUnitTest
{
    /// <summary>The cached core library reference for compilations with minimal framework stubs.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies Append with a single-character literal is reported (PSH1202) and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Insert with a single-character literal is reported and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InsertSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Insert(0, {|PSH1202:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Insert(0, 'x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a multi-character literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendMultiCharacterLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append("xy");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-literal string argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendVariableIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string value)
                                      => builder.Append(value);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies AppendLine is not reported — there is no char AppendLine overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendLineIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.AppendLine("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies chained Append calls are each reported and all fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedAppendsAllReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"a"|}).Append({|PSH1202:"b"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('a').Append('b');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a double-quote literal is fixed with correct char escaping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendDoubleQuoteLiteralEscapedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"\""|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('"');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies only simple member calls with the expected argument and literal shapes are candidates.</summary>
    /// <param name="expression">The invocation that fails a syntax requirement.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Append(\"x\")")]
    [Arguments("builder?.Append(\"x\")")]
    [Arguments("builder->Append(\"x\")")]
    [Arguments("builder.Append<string>(\"x\")")]
    [Arguments("builder.Append()")]
    [Arguments("builder.Append(\"x\", 0, 1)")]
    [Arguments("builder.Insert(0)")]
    [Arguments("builder.Insert(0, \"x\", 1)")]
    [Arguments("builder.AppendLine(\"x\")")]
    [Arguments("builder.Append(\"\")")]
    [Arguments("builder.Append(\"xy\")")]
    [Arguments("builder.Append(value)")]
    [Arguments("builder.Append('x')")]
    [Arguments("builder.Append(@\"x\")")]
    [Arguments("builder.Append(\"\"\"x\"\"\")")]
    [Arguments("builder.Append($\"x\")")]
    [Arguments("builder.Append(\"x\"u8)")]
    [Arguments("builder.Append((\"x\"))")]
    [Arguments("builder.Insert(0, value)")]
    public async Task NonCandidateInvocationIsCleanAsync(string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       class C
                       {
                           void Append(string value) { }
                           void M(System.Text.StringBuilder builder, string value)
                           {
                               {{expression}};
                           }
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies unresolved calls and similarly named methods on other types remain unchanged.</summary>
    /// <param name="expression">The invocation that fails semantic binding.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("missing.Append(\"x\")")]
    [Arguments("dynamicBuilder.Append(\"x\")")]
    [Arguments("other.Append(\"x\")")]
    [Arguments("other.Insert(0, \"x\")")]
    [Arguments("C.Append(\"x\")")]
    [Arguments("C.Insert(0, \"x\")")]
    public async Task NonBuilderInvocationIsCleanAsync(string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       class Other
                       {
                           public void Append(string value) { }
                           public void Insert(int index, string value) { }
                       }
                       class C
                       {
                           public static void Append(string value) { }
                           public static void Insert(int index, string value) { }
                           void M(Other other, dynamic dynamicBuilder)
                           {
                               {{expression}};
                           }
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a resolved builder method must have the exact string overload contract.</summary>
    /// <param name="member">The nonstandard builder overload.</param>
    /// <param name="expression">The invocation that binds to the overload.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void Append(string value, int count = 1) { }", "builder.Append(\"x\")")]
    [Arguments("public void Insert(int index, string value, int count = 1) { }", "builder.Insert(0, \"x\")")]
    [Arguments("public void Append(object value) { }", "builder.Append(\"x\")")]
    [Arguments("public void Insert(int index, object value) { }", "builder.Insert(0, \"x\")")]
    [Arguments("public void Insert(long index, string value) { }", "builder.Insert(0, \"x\")")]
    public async Task NonStringOverloadContractIsCleanAsync(string member, string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Text
                       {
                           public class StringBuilder
                           {
                               public void Append(char value) { }
                               public void Insert(int index, char value) { }
                               {{member}}
                           }
                       }
                       class C
                       {
                           void M(System.Text.StringBuilder builder) => {{expression}};
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, CoreReferences, cancellationToken);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a missing char overload disables only the corresponding suggestion.</summary>
    /// <param name="members">The char overloads available on the builder.</param>
    /// <param name="expression">The call whose char overload is unavailable.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "builder.Append(\"x\")")]
    [Arguments("", "builder.Insert(0, \"x\")")]
    [Arguments("public void Append(char value) { }", "builder.Insert(0, \"x\")")]
    [Arguments("public void Insert(int index, char value) { }", "builder.Append(\"x\")")]
    public async Task MissingCharOverloadIsCleanAsync(string members, string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Text
                       {
                           public class StringBuilder
                           {
                               public void Append(string value) { }
                               public void Insert(int index, string value) { }
                               {{members}}
                           }
                       }
                       class C
                       {
                           void M(System.Text.StringBuilder builder) => {{expression}};
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, CoreReferences, cancellationToken);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies candidate syntax remains safe when no framework provides StringBuilder.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingBuilderTypeIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = "class C { void M() { this.Append(\"x\"); this.Insert(0, \"x\"); } }";
        var diagnostics = await AnalyzeAsync(Source, [], cancellationToken);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies overload probing requires instance methods with exact char parameter shapes.</summary>
    /// <param name="members">The members exposed by the builder type.</param>
    /// <param name="expectedAppend">Whether an instance Append(char) overload should resolve.</param>
    /// <param name="expectedInsert">Whether an instance Insert(int, char) overload should resolve.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false, false)]
    [Arguments("public int Append; public int Insert;", false, false)]
    [Arguments("public static void Append(char value) { } public static void Insert(int index, char value) { }", false, false)]
    [Arguments("public void Append() { } public void Insert(int index) { }", false, false)]
    [Arguments("public void Append(char value, int count) { } public void Insert(int index, char value, int count) { }", false, false)]
    [Arguments("public void Append(string value) { } public void Insert(int index, string value) { }", false, false)]
    [Arguments("public void Insert(long index, char value) { }", false, false)]
    [Arguments("public void Append(char value) { }", true, false)]
    [Arguments("public void Insert(int index, char value) { }", false, true)]
    [Arguments("public void Append(char value) { } public void Insert(int index, char value) { }", true, true)]
    public async Task CharOverloadAvailabilityMatchesFrameworkAsync(string members, bool expectedAppend, bool expectedInsert, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Text
                       {
                           public class StringBuilder
                           {
                               {{members}}
                           }
                       }
                       """;
        var compilation = CSharpCompilation.Create(
            "BuilderOverloads",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            CoreReferences,
            new(OutputKind.DynamicallyLinkedLibrary));
        var resolved = Psh1202StringBuilderAppendCharAnalyzer.StringBuilderOverloads.TryResolve(compilation, out var overloads);

        await Assert.That(resolved).IsEqualTo(expectedAppend || expectedInsert);
        await Assert.That(overloads.HasAppendChar).IsEqualTo(expectedAppend);
        await Assert.That(overloads.HasInsertChar).IsEqualTo(expectedInsert);
    }

    /// <summary>Verifies Append remains reportable when the framework lacks Insert(char).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AppendOnlyCharFrameworkReportsAppendAsync() =>
        VerifyCoreLibraryAsync(
            """
            namespace System.Text
            {
                public class StringBuilder
                {
                    public void Append(string value) { }
                    public void Append(char value) { }
                }
            }
            class C
            {
                void M(System.Text.StringBuilder builder) => builder.Append({|PSH1202:"x"|});
            }
            """);

    /// <summary>Verifies Insert remains reportable when the framework lacks Append(char).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InsertOnlyCharFrameworkReportsInsertAsync() =>
        VerifyCoreLibraryAsync(
            """
            namespace System.Text
            {
                public class StringBuilder
                {
                    public void Insert(int index, string value) { }
                    public void Insert(int index, char value) { }
                }
            }
            class C
            {
                void M(System.Text.StringBuilder builder) => builder.Insert(0, {|PSH1202:"x"|});
            }
            """);

    /// <summary>Runs analyzer verification with cached references and a minimal builder definition.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static Task VerifyCoreLibraryAsync(string source)
    {
        var test = new AnalyzerVerifyAppendChar.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, CoreReferences));
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against incomplete code or a minimal framework definition.</summary>
    /// <param name="source">The compilation source.</param>
    /// <param name="references">Cached references, or none for a missing framework.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create("BuilderCalls", [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)], references, new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1202StringBuilderAppendCharAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAppendChar.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
