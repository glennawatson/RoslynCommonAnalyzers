// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1005ValueTypeEqualityBoxesAnalyzer,
    PerformanceSharp.Analyzers.Psh1005ValueTypeEqualityCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="Psh1005ValueTypeEqualityCodeFixProvider"/> — the multi-suggestion
/// fixes for PSH1005: record struct conversion, IEquatable implementation, and the readonly
/// combination.
/// </summary>
public class ValueTypeEqualityCodeFixUnitTest
{
    /// <summary>The expected comparison of three instance values.</summary>
    private const string ThreeValueEquality =
        "global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(A, other.A) && "
        + "global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(B, other.B) && "
        + "global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(Value, other.Value)";

    /// <summary>The equivalence key for record conversion.</summary>
    private const string RecordActionKey = "Psh1005ValueTypeEqualityCodeFixProvider.Record";

    /// <summary>Verifies record conversion does not duplicate an existing readonly modifier.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadonlyRecordConversionKeepsOneModifierAsync()
    {
        const string Source = "public readonly struct {|PSH1005:C|} { public int Value { get; } }";
        const string FixedSource = "public readonly record struct C { public int Value { get; } }";
        await VerifyAsync(Source, FixedSource, RecordActionKey);
    }

    /// <summary>Verifies a framework without HashCode offers only language-supported record conversion.</summary>
    /// <param name="version">The language version used by the consumer.</param>
    /// <param name="supportsRecord">Whether a record action can replace the struct.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LanguageVersion.CSharp9, false)]
    [Arguments(LanguageVersion.CSharp10, true)]
    public async Task FrameworkWithoutHashCodeLimitsActionsAsync(LanguageVersion version, bool supportsRecord)
    {
        const string Source = "public struct {|PSH1005:C|} { }";
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.NetStandard20, TestCode = Source, FixedCode = supportsRecord ? "public readonly record struct C { }" : Source };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(version)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies unrepresentable state, conflicting equality members, and stale locations offer no fix.</summary>
    /// <param name="source">The declaration receiving the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("struct C { public bool Equals(C other) => true; }")]
    [Arguments("struct C { public override int GetHashCode() => 0; }")]
    [Arguments("struct C { public static bool operator ==(C a, C b) => true; }")]
    [Arguments("struct C { public static bool operator !=(C a, C b) => false; }")]
    [Arguments("unsafe struct C { public int* Value; }")]
    [Arguments("unsafe struct C { public delegate*<void> Value; }")]
    [Arguments("unsafe struct C { public fixed int Value[2]; }")]
    [Arguments("unsafe struct C { public int* Value { get; set; } }")]
    [Arguments("unsafe struct C { public delegate*<void> Value { get; set; } }")]
    [Arguments("struct C {\n#region State\npublic int Value;\n#endregion\n}")]
    public async Task UnsupportedDeclarationOffersNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, source, LanguageVersion.Preview, includeFramework: true);
        var actions = await GetActionsAsync(document);
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies language versions, existing readonly state, framework APIs, and member limits select actions.</summary>
    /// <param name="version">The language version.</param>
    /// <param name="source">The target struct.</param>
    /// <param name="includeFramework">Whether framework metadata supplies HashCode.</param>
    /// <param name="expected">The ordered action suffixes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LanguageVersion.CSharp6, "struct C { }", true, "")]
    [Arguments(LanguageVersion.CSharp7, "struct C { }", true, "Equatable")]
    [Arguments(LanguageVersion.CSharp7_2, "struct C { }", true, "EquatableReadonly,Equatable")]
    [Arguments(LanguageVersion.CSharp9, "struct C { }", true, "EquatableReadonly,Equatable")]
    [Arguments(LanguageVersion.CSharp10, "struct C { }", true, "Record,EquatableReadonly,Equatable")]
    [Arguments(LanguageVersion.CSharp10, "readonly struct C { }", true, "Record,Equatable")]
    [Arguments(LanguageVersion.CSharp10, "struct C { public int Value; }", true, "Record,Equatable")]
    [Arguments(LanguageVersion.CSharp10, "struct C { public int A, B, C1, D, E, F, G, H; }", true, "Record,Equatable")]
    [Arguments(LanguageVersion.CSharp10, "struct C { public int A, B, C1, D, E, F, G, H, I; }", true, "Record")]
    [Arguments(LanguageVersion.CSharp10, "struct C { }", false, "Record")]
    [Arguments(LanguageVersion.CSharp9, "struct C { }", false, "")]
    public async Task AvailableActionsRespectCapabilitiesAsync(LanguageVersion version, string source, bool includeFramework, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, source, version, includeFramework);
        var actions = await GetActionsAsync(document);
        var actual = string.Join(",", actions.Select(static a => a.EquivalenceKey!["Psh1005ValueTypeEqualityCodeFixProvider.".Length..]));
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Verifies generated equality includes only instance fields and auto-properties, and compiles.</summary>
    /// <param name="members">The original members.</param>
    /// <param name="equals">The expected typed equality expression.</param>
    /// <param name="hash">The expected hash expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "true", "0")]
    [Arguments("public static int Shared; public const int Constant = 1; public static int Property { get; set; }", "true", "0")]
    [Arguments("public int A, B; public int Value { get; set; }", ThreeValueEquality, "global::System.HashCode.Combine(A, B, Value)")]
    [Arguments("public int Value { get { return 1; } } public int Other { get => 2; } public int Computed => 3;", "true", "0")]
    [Arguments("public static C operator +(C a, C b) => a;", "true", "0")]
    public async Task EquatableUsesOnlyInstanceDataAsync(string members, string equals, string hash)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, $"public struct C {{ {members} }}", LanguageVersion.Preview, includeFramework: true);
        var changed = await ApplyEquatableAsync(document);
        var root = (await changed.GetSyntaxRootAsync())!;
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var typedEquals = methods.Single(static m => m.Identifier.ValueText == "Equals" && m.ParameterList.Parameters[0].Type!.ToString() == "C");
        await Assert.That(typedEquals.ExpressionBody!.Expression.ToString()).IsEqualTo(equals);
        await Assert.That(methods.Single(static m => m.Identifier.ValueText == "GetHashCode").ExpressionBody!.Expression.ToString()).IsEqualTo(hash);
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    /// <summary>Verifies generic, nullable, nested structs retain their existing interface and indentation.</summary>
    /// <param name="baseList">The existing interface list.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments(" : global::System.IDisposable")]
    public async Task GenericNullableStructRetainsShapeAsync(string baseList)
    {
        var source = $$"""
            #nullable enable
            class Outer
            {
                public struct Item<T>{{baseList}} where T : class
                {
                    public T? Value { get; set; }
                    public void Dispose() { }
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, source, LanguageVersion.Preview, includeFramework: true);
        var changed = await ApplyEquatableAsync(document);
        var root = (await changed.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<StructDeclarationSyntax>().Single();
        await Assert.That(declaration.BaseList!.Types.Last().Type.ToString()).IsEqualTo("global::System.IEquatable<Item<T>>");
        const int EquatableAndDisposable = 2;
        await Assert.That(declaration.BaseList.Types.Count).IsEqualTo(baseList.Length == 0 ? 1 : EquatableAndDisposable);
        var text = (await changed.GetTextAsync()).ToString();
        await Assert.That(text).Contains("public override bool Equals(object? obj)");
        await Assert.That(text).Contains("\n        public bool Equals(Item<T> other)");
        await Assert.That(text).Contains("EqualityComparer<T?>.Default.Equals(Value, other.Value)");
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    /// <summary>Verifies colliding local names force global qualification in the generated members.</summary>
    /// <param name="comparer">The conflicting comparer declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class EqualityComparer { }")]
    [Arguments("class EqualityComparer<T> { }")]
    [Arguments("namespace EqualityComparer { }")]
    public async Task ShadowedFrameworkNamesAreQualifiedAsync(string comparer)
    {
        var source = $"class IEquatable<T> {{ }} class HashCode {{ }} {comparer} public struct C {{ public int Value; }}";
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, source, LanguageVersion.Preview, includeFramework: true);
        var changed = await ApplyEquatableAsync(document);
        var text = (await changed.GetTextAsync()).ToString();
        await Assert.That(text).Contains("global::System.IEquatable<C>");
        await Assert.That(text).Contains("global::System.HashCode.Combine(Value)");
        await Assert.That(text).Contains("global::System.Collections.Generic.EqualityComparer<int>");
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    /// <summary>Verifies the record struct action converts an immutable struct to a readonly record struct.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordActionConvertsToReadonlyRecordStructAsync()
    {
        const string Source = """
                              public struct {|PSH1005:Token|}
                              {
                                  private readonly int _id;

                                  public Token(int id) => _id = id;

                                  public int Id => _id;
                              }
                              """;
        const string FixedSource = """
                                   public readonly record struct Token
                                   {
                                       private readonly int _id;

                                       public Token(int id) => _id = id;

                                       public int Id => _id;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, RecordActionKey);
    }

    /// <summary>Verifies the record struct action keeps a mutable struct non-readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordActionKeepsMutableStructPlainAsync()
    {
        const string Source = """
                              public struct {|PSH1005:Counter|}
                              {
                                  public int Count;
                              }
                              """;
        const string FixedSource = """
                                   public record struct Counter
                                   {
                                       public int Count;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, RecordActionKey);
    }

    /// <summary>Verifies the equatable action generates the full member set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EquatableActionGeneratesMembersAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public struct {|PSH1005:Counter|}
                              {
                                  public int Count;
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public struct Counter : IEquatable<Counter>
                                   {
                                       public int Count;

                                       public bool Equals(Counter other) => EqualityComparer<int>.Default.Equals(Count, other.Count);

                                       public override bool Equals(object obj) => obj is Counter other && Equals(other);

                                       public override int GetHashCode() => HashCode.Combine(Count);

                                       public static bool operator ==(Counter left, Counter right) => left.Equals(right);

                                       public static bool operator !=(Counter left, Counter right) => !left.Equals(right);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.Equatable");
    }

    /// <summary>Verifies the combined action implements the interface and makes the struct readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EquatableReadonlyActionDoesBothAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public struct {|PSH1005:Token|}
                              {
                                  private readonly int _id;

                                  public Token(int id) => _id = id;

                                  public int Id => _id;
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public readonly struct Token : IEquatable<Token>
                                   {
                                       private readonly int _id;

                                       public Token(int id) => _id = id;

                                       public int Id => _id;

                                       public bool Equals(Token other) => EqualityComparer<int>.Default.Equals(_id, other._id);

                                       public override bool Equals(object obj) => obj is Token other && Equals(other);

                                       public override int GetHashCode() => HashCode.Combine(_id);

                                       public static bool operator ==(Token left, Token right) => left.Equals(right);

                                       public static bool operator !=(Token left, Token right) => !left.Equals(right);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.EquatableReadonly");
    }

    /// <summary>Creates a document with cached runtime references or an absent framework surface.</summary>
    /// <param name="workspace">The owning workspace.</param>
    /// <param name="source">The declaration source.</param>
    /// <param name="version">The parser language version.</param>
    /// <param name="includeFramework">Whether framework types are available.</param>
    /// <returns>The test document.</returns>
    private static Document CreateDocument(AdhocWorkspace workspace, string source, LanguageVersion version, bool includeFramework)
    {
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(version))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (includeFramework)
        {
            project = project.WithMetadataReferences(RuntimeMetadataReferences.Platform);
        }

        return project.AddDocument("Test.cs", SourceText.From(source));
    }

    /// <summary>Registers actions at the declaration identifier without depending on analyzer eligibility.</summary>
    /// <param name="document">The document containing the declaration.</param>
    /// <returns>The registered actions.</returns>
    private static async Task<List<CodeAction>> GetActionsAsync(Document document)
    {
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<StructDeclarationSyntax>().FirstOrDefault() as BaseTypeDeclarationSyntax
            ?? root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(AllocationRules.ValueTypeEqualityBoxes, declaration.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1005ValueTypeEqualityCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.GetFixAllProvider()).IsNull();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    /// <summary>Applies the equatable action and returns its document.</summary>
    /// <param name="document">The original document.</param>
    /// <returns>The rewritten document.</returns>
    private static async Task<Document> ApplyEquatableAsync(Document document)
    {
        var actions = await GetActionsAsync(document);
        var action = actions.Single(static a => a.EquivalenceKey == "Psh1005ValueTypeEqualityCodeFixProvider.Equatable");
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
    }

    /// <summary>Runs a code fix verification selecting one of the registered actions.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="equivalenceKey">The action to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource, string equivalenceKey)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource, CodeActionEquivalenceKey = equivalenceKey, };
        await test.RunAsync(CancellationToken.None);
    }
}
