// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NSubstitute;
using RoslynCommon.Analyzers.Tests;
using VerifyHungarian = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1305HungarianNotationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the Hungarian-notation rule (SST1305).</summary>
public class HungarianNotationAnalyzerUnitTest
{
    /// <summary>The configuration path read by the analyzer test harness.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Verifies semicolons separate raw option tokens when a host supplies the value directly.</summary>
    /// <param name="list">The raw list before editorconfig comment parsing.</param>
    /// <param name="allowed">Whether the list permits the candidate prefix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("xx;VM", true)]
    [Arguments(";vm;", true)]
    [Arguments("xx;vx", false)]
    public async Task HostSuppliedSemicolonListsMatchWholeTokensAsync(string list, bool allowed)
    {
        var options = Substitute.For<AnalyzerConfigOptions>();
        _ = options.TryGetValue("stylesharp.SST1305.allowed_hungarian_prefixes", out Arg.Any<string?>()).Returns(call =>
        {
            call[1] = list;
            return true;
        });
        var provider = Substitute.For<AnalyzerConfigOptionsProvider>();
        _ = provider.GlobalOptions.Returns(options);
        _ = provider.GetOptions(Arg.Any<SyntaxTree>()).Returns(options);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("SST1305", ReportDiagnostic.Warn));
        var tree = CSharpSyntaxTree.ParseText("class C { void M(int vmCount) { } }");
        var compilation = CSharpCompilation.Create("RawPrefixes", [tree], [RuntimeMetadataReferences.CoreLibrary], compilationOptions);
        var diagnostics = await compilation.WithAnalyzers([new Sst1305HungarianNotationAnalyzer()], options: new([], provider)).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(allowed ? 0 : 1);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "SST1305")).IsTrue();
    }

    /// <summary>Verifies every field and local declarator is checked after leading underscores.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FieldAndLocalDeclaratorsAreInspectedAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            class C
            {
                private int {|SST1305:_iCount|}, {|SST1305:__szName|}, count;
                void M()
                {
                    int {|SST1305:iValue|} = 0, {|SST1305:szText|} = 0, value = 0;
                }
            }
            """);

    /// <summary>Verifies names need a short lowercase prefix immediately followed by an uppercase letter.</summary>
    /// <param name="name">The parameter name that does not meet the heuristic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("_")]
    [Arguments("___")]
    [Arguments("Count")]
    [Arguments("i")]
    [Arguments("sz")]
    [Arguments("strName")]
    [Arguments("i1")]
    [Arguments("i_name")]
    public Task NamesWithoutHungarianPrefixAreCleanAsync(string name) =>
        VerifyHungarian.VerifyAnalyzerAsync($"class C {{ void M(int {name}) {{ }} }}");

    /// <summary>Verifies configured lists use exact case-insensitive tokens and either key can allow a prefix.</summary>
    /// <param name="specific">The rule-specific list.</param>
    /// <param name="general">The fallback list.</param>
    /// <param name="allowed">Whether either list permits the candidate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>Editorconfig semicolons begin comments before the rule receives the value.</remarks>
    [Test]
    [Arguments("", "", false)]
    [Arguments("vx", "xx", false)]
    [Arguments("vmm", "longer", false)]
    [Arguments(" , ;\t ", "xx", false)]
    [Arguments("xx,VM", "", true)]
    [Arguments("xx;VM", "", false)]
    [Arguments("xx VM", "", true)]
    [Arguments("xx\tVM", "", true)]
    [Arguments("vx,,\tVM,tail", "", true)]
    [Arguments("xx", "VM", true)]
    [Arguments("vm", "xx", true)]
    public async Task ConfiguredPrefixListsMatchWholeTokensAsync(string specific, string general, bool allowed)
    {
        var name = allowed ? "vmCount" : "{|SST1305:vmCount|}";
        var test = new VerifyHungarian.Test { TestCode = $"class C {{ void M(int {name}) {{ }} }}" };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, $"""
            root = true
            [*.cs]
            stylesharp.SST1305.allowed_hungarian_prefixes = {specific}
            stylesharp.allowed_hungarian_prefixes = {general}

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Hungarian-notation parameter is reported (SST1305).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HungarianParameterReportedAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int {|SST1305:iCount|})
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary camelCase parameter is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CamelCaseParameterIsCleanAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int isEnabled)
                {
                }
            }
            """);

    /// <summary>Verifies an abbreviated technology name is not read as a type prefix.</summary>
    /// <param name="name">The parameter name under test.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks><c>jsRuntime</c> says which runtime it is, the way <c>dbContext</c> says which context.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("jsRuntime")]
    [Arguments("efQueryable")]
    [Arguments("gcType")]
    [Arguments("msTest")]
    public Task AbbreviatedTechnologyNameIsCleanAsync(string name) =>
        VerifyHungarian.VerifyAnalyzerAsync(
            $$"""
            internal class C
            {
                private void M(int {{name}})
                {
                }
            }
            """);

    /// <summary>Verifies a prefix outside the built-in allow-list is reported when not configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnconfiguredPrefixReportedAsync() =>
        VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int {|SST1305:vmCount|};
            }
            """);

    /// <summary>Verifies the rule-specific editorconfig allow-list suppresses a configured prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificAllowedPrefixIsCleanAsync()
    {
        var test = new VerifyHungarian.Test
        {
            TestCode = """
                       internal class C
                       {
                           private int vmCount;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1305.allowed_hungarian_prefixes = vm, wpf

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the general editorconfig allow-list suppresses a configured prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralAllowedPrefixIsCleanAsync()
    {
        var test = new VerifyHungarian.Test
        {
            TestCode = """
                       internal class C
                       {
                           private int vmCount;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.allowed_hungarian_prefixes = vm

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
