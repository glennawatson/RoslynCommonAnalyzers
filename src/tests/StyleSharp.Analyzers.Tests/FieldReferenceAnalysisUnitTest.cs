// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests shared field-reference helper fast paths used by lock-target analysis.</summary>
public class FieldReferenceAnalysisUnitTest
{
    /// <summary>The backing field used by semantic checks.</summary>
    private const string BackingFieldName = "_value";

    /// <summary>Verifies lock targets are rejected when a callable parameter shadows the field.</summary>
    /// <param name="member">The callable containing the lock.</param>
    /// <param name="expected">Whether the lock can be bound syntactically to the field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M(object _gate) { lock (_gate) { } }", false)]
    [Arguments("void M() { void Local(object _gate) { lock (_gate) { } } }", false)]
    [Arguments("void M() { void Local(object other) { lock (_gate) { } } }", true)]
    [Arguments("void M() { System.Action<object> f = _gate => { lock (_gate) { } }; }", false)]
    [Arguments("void M() { System.Action<object> f = other => { lock (_gate) { } }; }", true)]
    [Arguments("void M() { System.Action<object> f = (object _gate) => { lock (_gate) { } }; }", false)]
    [Arguments("void M() { System.Action<object> f = (object other) => { lock (_gate) { } }; }", true)]
    [Arguments("void M() { System.Action<object> f = delegate(object _gate) { lock (_gate) { } }; }", false)]
    [Arguments("void M() { System.Action<object> f = delegate(object other) { lock (_gate) { } }; }", true)]
    [Arguments("void M() { System.Action f = delegate { lock (_gate) { } }; }", true)]
    [Arguments("int P { get { lock (_gate) { } return 0; } }", true)]
    [Arguments("void M() { object _gate = new(); lock (_gate) { } }", false)]
    [Arguments("void M() { object other = new(); lock (_gate) { } }", true)]
    [Arguments("void M() { lock (_gate) { } object _gate = new(); }", true)]
    [Arguments("void M(object value) { if (value is object _gate) { lock (_gate) { } } }", false)]
    [Arguments("void M(object value) { if (value is object other) { lock (_gate) { } } }", true)]
    [Arguments("void M() { foreach (var _gate in values) { lock (_gate) { } } }", false)]
    [Arguments("void M() { foreach (var other in values) { lock (_gate) { } } }", true)]
    [Arguments("void M() { try { } catch (System.Exception _gate) { lock (_gate) { } } }", false)]
    [Arguments("void M() { try { } catch (System.Exception other) { lock (_gate) { } } }", true)]
    public async Task LockTargetRespectsCallableShadowingAsync(string member, bool expected)
    {
        var type = (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"class C {{ private object _gate; {member} }}")!;
        var expression = type.DescendantNodes().OfType<LockStatementSyntax>().Single().Expression;
        await Assert.That(FieldReferenceAnalysis.TryGetPrivateObjectFieldLockTarget(type, expression, out var declaration)).IsEqualTo(expected);
        await Assert.That(declaration is not null).IsEqualTo(expected);
    }

    /// <summary>Verifies only explicit private fields with unambiguous object types qualify as lock targets.</summary>
    /// <param name="members">The field declarations to inspect.</param>
    /// <param name="expression">The lock expression.</param>
    /// <param name="expected">Whether a private object field is found.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("private object _gate;", "_gate", true)]
    [Arguments("static private System.Object _gate;", "_gate", true)]
    [Arguments("private global::System.Object _gate;", "_gate", true)]
    [Arguments("private object first, _gate;", "_gate", true)]
    [Arguments("private object other; private object _gate;", "_gate", true)]
    [Arguments("private Other.Object _gate;", "_gate", false)]
    [Arguments("private alias::System.Object _gate;", "_gate", false)]
    [Arguments("private global::Other.Object _gate;", "_gate", false)]
    [Arguments("private System.String _gate;", "_gate", false)]
    [Arguments("private Object _gate;", "_gate", false)]
    [Arguments("private int _gate;", "_gate", false)]
    [Arguments("public object _gate;", "_gate", false)]
    [Arguments("object _gate;", "_gate", false)]
    [Arguments("private object other;", "_gate", false)]
    [Arguments("private object _gate;", "this._gate", false)]
    [Arguments("", "_gate", false)]
    public async Task LockTargetRequiresPrivateObjectFieldAsync(string members, string expression, bool expected)
    {
        var type = (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"class C {{ {members} }}")!;
        await Assert.That(FieldReferenceAnalysis.IsPrivateObjectFieldLockTarget(type, SyntaxFactory.ParseExpression(expression))).IsEqualTo(expected);
    }

    /// <summary>Verifies direct, qualified and tuple writes are distinguished from reads.</summary>
    /// <param name="source">The expression containing the field.</param>
    /// <param name="expected">Whether the occurrence writes the field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_value = 1", true)]
    [Arguments("this._value = 1", true)]
    [Arguments("other = _value", false)]
    [Arguments("++_value", true)]
    [Arguments("--_value", true)]
    [Arguments("-_value", false)]
    [Arguments("_value++", true)]
    [Arguments("Use(ref _value)", true)]
    [Arguments("Use(out _value)", true)]
    [Arguments("Use(_value)", false)]
    [Arguments("(_value, other) = pair", true)]
    [Arguments("((this._value, other), last) = pair", true)]
    [Arguments("pair = (_value, other)", false)]
    [Arguments("(_value, other)", false)]
    public async Task FieldWriteClassificationMatchesSyntaxAsync(string source, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(source);
        var identifier = expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Single(static node => node.Identifier.ValueText == BackingFieldName);
        await Assert.That(FieldReferenceAnalysis.IsWrite(identifier)).IsEqualTo(expected);
    }

    /// <summary>Verifies both discovery overloads reject ineligible or externally used storage.</summary>
    /// <param name="source">The type with one property.</param>
    /// <param name="expected">Whether its private field can become property storage.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { private int _value; int P => _value; }", true)]
    [Arguments("class C { private static int _value; static int P => _value; }", true)]
    [Arguments("partial class C { private int _value; int P => _value; }", false)]
    [Arguments("class C { private int _value; int P { get; } }", false)]
    [Arguments("class C { int P => 0; }", false)]
    [Arguments("class C { private int _value; int P => missing; }", false)]
    [Arguments("class C { private static int _value; int P => _value; }", false)]
    [Arguments("class C { private const int _value = 1; static int P => _value; }", false)]
    [Arguments("class C { public int _value; int P => _value; }", false)]
    [Arguments("class C { private int _value, other; int P => _value; }", false)]
    [Arguments("class C { [System.Obsolete] private int _value; int P => _value; }", false)]
    [Arguments("class C { private volatile int _value; int P => _value; }", false)]
    [Arguments("class C { private int _value; int P => _value; int M() => _value; }", false)]
    [Arguments("class C { private int _value; int P => _value; int M(int _value) => _value; }", true)]
    public async Task BackingFieldMustBeEligibleAndExclusiveAsync(string source, bool expected)
    {
        var (_, property, model) = CreateSemanticModel(source);
        var inferred = FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, CancellationToken.None, out _, out _, out _);
        var named = FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, BackingFieldName, CancellationToken.None, out _, out _, out _);
        await Assert.That(inferred).IsEqualTo(expected);
        await Assert.That(named).IsEqualTo(expected);
    }

    /// <summary>Verifies a standalone property is rejected before binding a field.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedPropertyHasNoBackingFieldAsync()
    {
        var (_, _, model) = CreateSemanticModel("class C { int P => 0; }");
        var property = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("int P => _value;")!;
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, CancellationToken.None, out _, out _, out _)).IsFalse();
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, BackingFieldName, CancellationToken.None, out _, out _, out _)).IsFalse();
    }

    /// <summary>Verifies a property whose body is missing is rejected before semantic lookup.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PropertyWithoutBodyHasNoBackingFieldAsync()
    {
        var (_, _, model) = CreateSemanticModel("class C { int P => 0; }");
        var property = ((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("int P { get; }")!).WithAccessorList(null);
        var type = SyntaxFactory.ClassDeclaration("C").AddMembers(property);
        property = (PropertyDeclarationSyntax)type.Members[0];
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, CancellationToken.None, out _, out _, out _)).IsFalse();
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, BackingFieldName, CancellationToken.None, out _, out _, out _)).IsFalse();
    }

    /// <summary>Verifies a name present only as a parameter is not counted as a field reference.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnreferencedFieldDoesNotSatisfyExclusiveUseAsync()
    {
        var (type, property, model) = CreateSemanticModel("class C { private int _value; int P => 0; int M(int _value) => _value; }");
        var field = GetDeclaredFieldSymbol(model, type, BackingFieldName);
        await Assert.That(FieldReferenceAnalysis.OnlyReferencedInside(model, type, field, property, CancellationToken.None)).IsFalse();
        await Assert.That(FieldReferenceAnalysis.FieldNameReferences(type, "missing")).IsEmpty();
    }

    /// <summary>Verifies a same-named external or enum field cannot supply a backing-field declaration.</summary>
    /// <param name="source">The source referencing a different field with the same name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { private string Empty; string P => string.Empty; }")]
    [Arguments("class C { private int Empty; object P => E.Empty; enum E { Empty } }")]
    public async Task NonstorageFieldDeclarationIsRejectedAsync(string source)
    {
        var (_, property, model) = CreateSemanticModel(source);
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, CancellationToken.None, out _, out _, out _)).IsFalse();
        await Assert.That(FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, "Empty", CancellationToken.None, out _, out _, out _)).IsFalse();
    }

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
        var field = GetDeclaredFieldSymbol(model, type, BackingFieldName);

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
        var field = GetDeclaredFieldSymbol(model, type, BackingFieldName);

        var result = FieldReferenceAnalysis.OnlyReferencedInside(model, type, field, property, CancellationToken.None);

        await Assert.That(result).IsFalse();
    }

    /// <summary>Parses the first lock statement from the supplied source.</summary>
    /// <param name="source">The source containing the lock statement.</param>
    /// <returns>The parsed lock statement.</returns>
    private static LockStatementSyntax ParseLockStatement(string source) =>
        (LockStatementSyntax)((MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[1]).Body!.Statements[0];

    /// <summary>Creates a semantic model for a single-type test source and returns the type and first property.</summary>
    /// <param name="source">The source to compile.</param>
    /// <returns>The containing type, property, and semantic model.</returns>
    private static (TypeDeclarationSyntax Type, PropertyDeclarationSyntax Property, SemanticModel Model) CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();
        var compilation = CSharpCompilation.Create(
            assemblyName: nameof(FieldReferenceAnalysisUnitTest),
            syntaxTrees: [tree],
            references: RuntimeMetadataReferences.Platform);
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
}
