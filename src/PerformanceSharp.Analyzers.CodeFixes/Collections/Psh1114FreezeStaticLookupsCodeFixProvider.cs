// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Freezes a reported static lookup field (PSH1114): the field type becomes
/// <c>FrozenDictionary&lt;K,V&gt;</c>/<c>FrozenSet&lt;T&gt;</c> and the initializer is wrapped in
/// <c>ToFrozenDictionary</c>/<c>ToFrozenSet</c>, carrying an explicit comparer argument through
/// so lookup semantics never change silently. The fix is offered only when the declared type is
/// an unqualified generic name and the initializer is an object creation — anywhere the comparer
/// cannot be seen, the diagnostic stands without a fix. When
/// <c>System.Collections.Frozen</c> is not imported the frozen names are spelled fully
/// qualified and the wrapper is called as a static method, so the fix never breaks the build.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1114FreezeStaticLookupsCodeFixProvider))]
[Shared]
public sealed class Psh1114FreezeStaticLookupsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The frozen namespace probed for in the file's usings.</summary>
    private const string FrozenNamespace = "System.Collections.Frozen";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.FreezeStaticLookups.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Freeze the lookup", nameof(Psh1114FreezeStaticLookupsCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported lookup field and builds its frozen replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => TryGetFixableField(root, diagnostic) is { } field
            ? new NodeReplacement(field, Rewrite(root, model, field, CancellationToken.None))
            : null;

    /// <summary>Returns the reported field when the declared type and initializer support the rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The field declaration, or <see langword="null"/> when the fix cannot apply safely.</returns>
    private static FieldDeclarationSyntax? TryGetFixableField(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not VariableDeclaratorSyntax declarator
            || declarator.Parent?.Parent is not FieldDeclarationSyntax field
            || field.Declaration.Type is not GenericNameSyntax
            || declarator.Initializer?.Value is not BaseObjectCreationExpressionSyntax)
        {
            return null;
        }

        return Psh1114FreezeStaticLookupsAnalyzer.TryGetLookupTypeName(field.Declaration.Type) is null ? null : field;
    }

    /// <summary>Builds the frozen field declaration.</summary>
    /// <param name="root">The syntax root, probed for the frozen namespace import.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="field">The field to rewrite; callers must have validated the shape.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The rewritten field declaration.</returns>
    private static FieldDeclarationSyntax Rewrite(SyntaxNode root, SemanticModel model, FieldDeclarationSyntax field, CancellationToken cancellationToken)
    {
        var typeName = (GenericNameSyntax)field.Declaration.Type;
        var isDictionary = typeName.Identifier.ValueText == Psh1114FreezeStaticLookupsAnalyzer.DictionaryTypeName;
        var frozenName = isDictionary ? "FrozenDictionary" : "FrozenSet";
        var wrapperName = isDictionary ? "ToFrozenDictionary" : "ToFrozenSet";
        var hasImport = HasFrozenImport(root);

        var declarator = field.Declaration.Variables[0];
        var creation = (BaseObjectCreationExpressionSyntax)declarator.Initializer!.Value;
        var source = WithDeclaredType(creation, typeName);
        var wrapped = BuildWrapperCall(model, creation, source, wrapperName, frozenName, hasImport, cancellationToken);

        TypeSyntax frozenType = SyntaxFactory.GenericName(SyntaxFactory.Identifier(frozenName), typeName.TypeArgumentList);
        if (!hasImport)
        {
            frozenType = SyntaxFactory.QualifiedName(
                SyntaxFactory.ParseName($"global::{FrozenNamespace}"),
                (SimpleNameSyntax)frozenType);
        }

        var newDeclarator = declarator.WithInitializer(declarator.Initializer.WithValue(wrapped));
        return field.WithDeclaration(
            field.Declaration
                .WithType(frozenType.WithTriviaFrom(typeName))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(newDeclarator)));
    }

    /// <summary>Gives a target-typed creation the type the field declared.</summary>
    /// <param name="creation">The original creation expression.</param>
    /// <param name="declaredType">The field's declared collection type.</param>
    /// <returns>The creation as an expression that carries its own type.</returns>
    /// <remarks>
    /// A target-typed <c>new</c> takes its type from where it sits. Both forms of the rewrite move the
    /// creation — into an argument, or onto a member access — so it would be re-pointed at the wrapper's
    /// parameter, or left with no target at all. Naming the type keeps it building what it built before.
    /// </remarks>
    private static ExpressionSyntax WithDeclaredType(BaseObjectCreationExpressionSyntax creation, GenericNameSyntax declaredType)
        => creation is ImplicitObjectCreationExpressionSyntax implicitCreation
            ? SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.Token(default, SyntaxKind.NewKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                declaredType.WithoutTrivia(),
                implicitCreation.ArgumentList,
                implicitCreation.Initializer)
            : creation.WithoutTrivia();

    /// <summary>Builds the <c>ToFrozen*</c> call, fluent when the namespace is imported and static otherwise.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="creation">The original creation expression, bound for its comparer argument.</param>
    /// <param name="source">The creation as it should be written into the call.</param>
    /// <param name="wrapperName">The wrapper method name.</param>
    /// <param name="frozenName">The frozen factory class name.</param>
    /// <param name="hasImport">Whether the frozen namespace is imported.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The wrapping invocation.</returns>
    private static InvocationExpressionSyntax BuildWrapperCall(
        SemanticModel model,
        BaseObjectCreationExpressionSyntax creation,
        ExpressionSyntax source,
        string wrapperName,
        string frozenName,
        bool hasImport,
        CancellationToken cancellationToken)
    {
        var arguments = SyntaxFactory.SeparatedList<ArgumentSyntax>();
        if (!hasImport)
        {
            arguments = arguments.Add(SyntaxFactory.Argument(source));
        }

        if (TryGetComparerArgument(model, creation, cancellationToken) is { } comparer)
        {
            arguments = arguments.Add(SyntaxFactory.Argument(comparer.WithoutTrivia()));
        }

        if (hasImport)
        {
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    source,
                    SyntaxFactory.IdentifierName(wrapperName)),
                SyntaxFactory.ArgumentList(arguments));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression($"global::{FrozenNamespace}.{frozenName}"),
                SyntaxFactory.IdentifierName(wrapperName)),
            SyntaxFactory.ArgumentList(arguments));
    }

    /// <summary>Returns the creation's comparer argument, when one is visible.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="creation">The creation expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The comparer expression, or <see langword="null"/> when the constructor takes none.</returns>
    private static ExpressionSyntax? TryGetComparerArgument(SemanticModel model, BaseObjectCreationExpressionSyntax creation, CancellationToken cancellationToken)
    {
        if (creation.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || model.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var parameter = FindParameter(constructor, argument, i);
            if (parameter is { Type.Name: "IEqualityComparer" })
            {
                return argument.Expression;
            }
        }

        return null;
    }

    /// <summary>Returns the parameter an argument binds to, honoring named arguments.</summary>
    /// <param name="constructor">The bound constructor.</param>
    /// <param name="argument">The argument to resolve.</param>
    /// <param name="ordinal">The argument's position.</param>
    /// <returns>The matching parameter, or <see langword="null"/>.</returns>
    private static IParameterSymbol? FindParameter(IMethodSymbol constructor, ArgumentSyntax argument, int ordinal)
    {
        var parameters = constructor.Parameters;
        if (argument.NameColon is { } nameColon)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == nameColon.Name.Identifier.ValueText)
                {
                    return parameters[i];
                }
            }

            return null;
        }

        return ordinal < parameters.Length ? parameters[ordinal] : null;
    }

    /// <summary>Returns whether the compilation unit imports the frozen namespace.</summary>
    /// <param name="root">The syntax root.</param>
    /// <returns><see langword="true"/> when a matching using directive exists.</returns>
    private static bool HasFrozenImport(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax unit)
        {
            return false;
        }

        foreach (var directive in unit.Usings)
        {
            if (directive.Alias is null
                && !directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                && directive.StaticKeyword.IsKind(SyntaxKind.None)
                && directive.Name?.ToString() == FrozenNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
