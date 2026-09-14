// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyBlockReceiver = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1711UnusedBlockReceiverAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the unused extension-block receiver rule (SST1711).</summary>
public class ExtensionBlockReceiverUsageUnitTest
{
    /// <summary>Verifies an empty receiver list supplied by a syntax transformation is safely ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyReceiverSyntaxListIsIgnoredAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("static class E { extension(string text) { public int M() => 1; } }");
        var block = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Single(ExtensionBlockHelper.IsExtensionBlock);
        var changed = root.ReplaceNode(block, block.WithParameterList(SyntaxFactory.ParameterList()));
        var tree = CSharpSyntaxTree.Create(changed, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create("EmptyReceiver", [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst1711UnusedBlockReceiverAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies blocks without a usable receiver name are ignored, including incomplete syntax.</summary>
    /// <param name="receiver">The receiver declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("string")]
    [Arguments("string _")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnnamedReceiverIsIgnoredAsync(string receiver) =>
        RunAsync($"static class E {{ extension({receiver}) {{ public int M() => 1; }} }}", CompilerDiagnostics.None);

    /// <summary>Verifies supported bodies report only when every accessor ignores the receiver.</summary>
    /// <param name="member">The extension member and its expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public int {|SST1711:M|}() { return 1; }")]
    [Arguments("public int M() { return text.Length; }")]
    [Arguments("public int {|SST1711:Value|} { get { return 1; } }")]
    [Arguments("public int Value { get { return 1; } set { _ = text.Length; } }")]
    [Arguments("public int {|SST1711:this|}[int index] => 1;")]
    [Arguments("public int this[int index] => text.Length;")]
    [Arguments("public int {|SST1711:this|}[int index] { get { return 1; } }")]
    [Arguments("public int this[int index] { get { return text.Length; } }")]
    [Arguments("public int M();")]
    [Arguments("public int Field;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MemberBodiesRespectReceiverReadsAsync(string member) =>
        RunAsync($"static class E {{ extension(string text) {{ {member} }} }}", CompilerDiagnostics.None);

    /// <summary>Verifies a property that never reads the receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyIgnoringTheReceiverIsReportedAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int {|SST1711:Always|} => 42;
                }
            }
            """);

    /// <summary>Verifies a method that never reads the receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodIgnoringTheReceiverIsReportedAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int {|SST1711:Seven|}() => 7;
                }
            }
            """);

    /// <summary>Verifies a member that reads the receiver in any accessor is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The setter alone reading the receiver is enough; the pair is one member.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberReadingTheReceiverIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public int Length => text.Length;

                    public int Doubled() => text.Length * 2;
                }
            }
            """);

    /// <summary>Verifies a static block member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The receiver is not in scope for a static extension member, so it could not read it either way.
    /// Reporting one would make every static extension member a violation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticBlockMemberIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                extension(string text)
                {
                    public static string Blank => "   ";
                }
            }
            """);

    /// <summary>Verifies a member outside an extension block is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlainStaticHelperIsCleanAsync() =>
        RunAsync(
            """
            public static class StringExtensions
            {
                public static int Always() => 42;

                public class Nested
                {
                    public int Value => 42;
                }
            }
            """);

    /// <summary>Runs the verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="compilerDiagnostics">Whether incomplete member syntax is checked by the compiler.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, CompilerDiagnostics compilerDiagnostics = CompilerDiagnostics.Errors)
    {
        var test = new VerifyBlockReceiver.Test { TestCode = source, CompilerDiagnostics = compilerDiagnostics };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
