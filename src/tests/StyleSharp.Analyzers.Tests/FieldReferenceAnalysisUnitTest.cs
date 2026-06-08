// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests shared field-reference helper fast paths used by lock-target analysis.</summary>
public class FieldReferenceAnalysisUnitTest
{
    /// <summary>Verifies the shared private-object-field check recognizes a simple lock target.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateObjectFieldLockTargetCheckRecognizesPrivateObjectField()
    {
        var lockStatement = ParseLockStatement(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");
        var type = (TypeDeclarationSyntax)lockStatement.Parent!.Parent!.Parent!;

        await Assert.That(FieldReferenceAnalysis.IsPrivateObjectFieldLockTarget(type, lockStatement.Expression)).IsTrue();
    }

    /// <summary>Verifies single-use backing-field discovery keeps selecting the first referenced field.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TryFindSingleUseBackingFieldKeepsFirstReferencedFieldAsync()
    {
        const string Source = "public class C { private int _first; private int _second; public int Value { get => _first + _second; set => _first = value; } }";
        var (_, property, model) = CreateSemanticModel(Source);

        var success = FieldReferenceAnalysis.TryFindSingleUseBackingField(
            model,
            property,
            CancellationToken.None,
            out _,
            out _,
            out var symbol);

        await Assert.That(success).IsTrue();
        await Assert.That(symbol!.Name).IsEqualTo("_first");
    }

    /// <summary>Verifies sibling scopes that shadow the field name do not count as field references.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OnlyReferencedInsideIgnoresShadowedNameOutsideAllowedNodeAsync()
    {
        const string Source = "public class C { private int _value; public int Value { get => _value; set => _value = value; } void M(int _value) { _ = _value; } }";
        var (type, property, model) = CreateSemanticModel(Source);
        var field = GetDeclaredFieldSymbol(model, type, "_value");

        var result = FieldReferenceAnalysis.OnlyReferencedInside(model, type, field, property, CancellationToken.None);

        await Assert.That(result).IsTrue();
    }

    /// <summary>Verifies qualified field references outside the allowed node are still detected.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OnlyReferencedInsideDetectsQualifiedFieldReferenceOutsideAllowedNodeAsync()
    {
        const string Source = "public class C { private int _value; public int Value { get => _value; set => _value = value; } int Read() => this._value; }";
        var (type, property, model) = CreateSemanticModel(Source);
        var field = GetDeclaredFieldSymbol(model, type, "_value");

        var result = FieldReferenceAnalysis.OnlyReferencedInside(model, type, field, property, CancellationToken.None);

        await Assert.That(result).IsFalse();
    }

    /// <summary>Parses the first lock statement from the supplied source.</summary>
    /// <param name="source">The source containing the lock statement.</param>
    /// <returns>The parsed lock statement.</returns>
    private static LockStatementSyntax ParseLockStatement(string source)
        => (LockStatementSyntax)((MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[1]).Body!.Statements[0];

    /// <summary>Creates a semantic model for a single-type test source and returns the type and first property.</summary>
    /// <param name="source">The source to compile.</param>
    /// <returns>The containing type, property, and semantic model.</returns>
    private static (TypeDeclarationSyntax Type, PropertyDeclarationSyntax Property, SemanticModel Model) CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();
        var compilation = CSharpCompilation.Create(
            assemblyName: "FieldReferenceAnalysisUnitTest",
            syntaxTrees: [tree],
            references: CreateReferences());
        var model = compilation.GetSemanticModel(tree);
        var type = (TypeDeclarationSyntax)root.Members[0];
        var property = type.Members.OfType<PropertyDeclarationSyntax>().Single();
        return (type, property, model);
    }

    /// <summary>Gets the declared field symbol for the supplied field name.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The declared field symbol.</returns>
    private static IFieldSymbol GetDeclaredFieldSymbol(SemanticModel model, TypeDeclarationSyntax type, string fieldName)
    {
        var declaration = type.Members.OfType<FieldDeclarationSyntax>()
            .Single(field => field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == fieldName));
        var declarator = declaration.Declaration.Variables.Single(variable => variable.Identifier.ValueText == fieldName);
        return (IFieldSymbol)model.GetDeclaredSymbol(declarator)!;
    }

    /// <summary>Creates metadata references for the current runtime.</summary>
    /// <returns>The metadata references.</returns>
    private static MetadataReference[] CreateReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = new MetadataReference[trustedAssemblies.Length];
        for (var i = 0; i < trustedAssemblies.Length; i++)
        {
            references[i] = MetadataReference.CreateFromFile(trustedAssemblies[i]);
        }

        return references;
    }
}
