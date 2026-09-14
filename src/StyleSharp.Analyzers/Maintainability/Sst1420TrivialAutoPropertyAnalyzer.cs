// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Reports properties that trivially wrap a private single-use backing field (SST1420).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1420TrivialAutoPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The implicit parameter a write accessor assigns from.</summary>
    private const string SetterValueParameterName = "value";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.PreferAutoProperty);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Returns the single backing-field name when every accessor trivially reads or writes the same field.</summary>
    /// <param name="property">The property declaration.</param>
    /// <param name="fieldName">The matched backing-field name.</param>
    /// <returns><see langword="true"/> when every accessor trivially targets the same field.</returns>
    /// <remarks>
    /// An expression-bodied property is its own getter, so it qualifies on the expression alone. A property
    /// with an accessor list must expose a getter: an auto-property without one does not compile (CS8051),
    /// which would make the code fix produce broken source for a write-only property.
    /// </remarks>
    internal static bool TryGetSingleBackingFieldName(PropertyDeclarationSyntax property, out string? fieldName)
    {
        fieldName = null;
        if (property.AccessorList is not { Accessors.Count: > 0 } accessors)
        {
            return property.ExpressionBody is { } expressionBody && TryGetFieldName(expressionBody.Expression, out fieldName);
        }

        var hasGetter = false;
        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            var accessor = accessors.Accessors[i];
            if (!TryGetAccessorFieldName(accessor, out var accessorFieldName))
            {
                return false;
            }

            hasGetter |= accessor.Keyword.IsKind(SyntaxKind.GetKeyword);
            if (fieldName is null)
            {
                fieldName = accessorFieldName;
                continue;
            }

            if (!string.Equals(fieldName, accessorFieldName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return hasGetter && fieldName is not null;
    }

    /// <summary>Returns whether all property accessors directly read or assign the supplied backing field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="field">The backing-field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all accessors trivially target the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasOnlyTrivialAccessors(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        IFieldSymbol field,
        CancellationToken cancellationToken) =>
        HasOnlyTrivialAccessors(model, property, field, fieldName: null, cancellationToken);

    /// <summary>Returns whether all property accessors directly read or assign the supplied backing field, verifying the field name syntactically first.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="field">The backing-field symbol.</param>
    /// <param name="fieldName">The backing-field name already matched by <see cref="TryGetSingleBackingFieldName"/>, or <see langword="null"/> to skip the name check.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all accessors trivially target the field.</returns>
    /// <remarks>
    /// When a name is supplied, each accessor's read/assignment target is first compared by identifier text to
    /// <paramref name="fieldName"/> (free, syntactic) before a single bind confirms it resolves to <paramref name="field"/>.
    /// </remarks>
    internal static bool HasOnlyTrivialAccessors(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        IFieldSymbol field,
        string? fieldName,
        CancellationToken cancellationToken)
    {
        if (property.AccessorList is not { Accessors.Count: > 0 } accessors)
        {
            return property.ExpressionBody is { } expressionBody
                && IsFieldRead(model, expressionBody.Expression, field, fieldName, cancellationToken);
        }

        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            var accessor = accessors.Accessors[i];
            if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
            {
                if (!IsTrivialGet(model, accessor, field, fieldName, cancellationToken))
                {
                    return false;
                }

                continue;
            }

            if (!IsTrivialSet(model, accessor, field, fieldName, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports a property when every accessor is a direct field read or write.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;

        // Resolve the backing field syntactically by name first. The name-keyed overload of
        // TryFindSingleUseBackingField does a single GetDeclaredSymbol on the matched field instead
        // of FindReferencedField's recursive GetSymbolInfo-on-every-identifier walk, so the field is
        // bound once here and threaded straight into the trivial-accessor verification below.
        if (!TryGetSingleBackingFieldName(property, out var fieldName)
            || !FieldReferenceAnalysis.TryFindSingleUseBackingField(
                context.SemanticModel,
                property,
                fieldName!,
                context.CancellationToken,
                out _,
                out _,
                out var field)
            || !HasOnlyTrivialAccessors(
                context.SemanticModel,
                property,
                field!,
                fieldName!,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.PreferAutoProperty, property.Identifier.GetLocation()));
    }

    /// <summary>Returns the field name targeted by one trivial accessor.</summary>
    /// <param name="accessor">The accessor to inspect.</param>
    /// <param name="fieldName">The extracted field name.</param>
    /// <returns><see langword="true"/> when the accessor is a trivial field read or write.</returns>
    private static bool TryGetAccessorFieldName(AccessorDeclarationSyntax accessor, out string? fieldName)
    {
        if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
        {
            return TryGetGetterFieldName(accessor, out fieldName);
        }

        if (accessor.Keyword.IsKind(SyntaxKind.SetKeyword) || accessor.Keyword.IsKind(SyntaxKind.InitKeyword))
        {
            return TryGetSetterFieldName(accessor, out fieldName);
        }

        fieldName = null;
        return false;
    }

    /// <summary>Returns the field name from a trivial getter.</summary>
    /// <param name="accessor">The getter accessor.</param>
    /// <param name="fieldName">The extracted field name.</param>
    /// <returns><see langword="true"/> when the getter directly returns the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetGetterFieldName(AccessorDeclarationSyntax accessor, out string? fieldName) =>
        TryGetFieldName(GetReturnedExpression(accessor), out fieldName);

    /// <summary>Returns the field name from a trivial setter or init accessor.</summary>
    /// <param name="accessor">The setter or init accessor.</param>
    /// <param name="fieldName">The extracted field name.</param>
    /// <returns><see langword="true"/> when the accessor directly assigns <c>value</c> to the field.</returns>
    private static bool TryGetSetterFieldName(AccessorDeclarationSyntax accessor, out string? fieldName)
    {
        var assignment = accessor.ExpressionBody?.Expression as AssignmentExpressionSyntax;
        if (assignment is null && accessor.Body?.Statements is [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax bodyAssignment }])
        {
            assignment = bodyAssignment;
        }

        fieldName = null;
        return assignment is { Right: IdentifierNameSyntax right }
            && right.Identifier.Text == SetterValueParameterName
            && TryGetFieldName(assignment.Left, out fieldName);
    }

    /// <summary>Returns whether a getter directly returns the backing field, checking a supplied name syntactically before binding once.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="accessor">The getter.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="fieldName">The expected backing-field name, or <see langword="null"/> to skip the name check.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a trivial getter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTrivialGet(
        SemanticModel model,
        AccessorDeclarationSyntax accessor,
        IFieldSymbol field,
        string? fieldName,
        CancellationToken cancellationToken) =>
        IsFieldRead(model, GetReturnedExpression(accessor), field, fieldName, cancellationToken);

    /// <summary>Returns the expression a getter yields, whether it uses an expression body or a single return.</summary>
    /// <param name="accessor">The getter.</param>
    /// <returns>The yielded expression, or <see langword="null"/> when the getter does more than return one expression.</returns>
    private static ExpressionSyntax? GetReturnedExpression(AccessorDeclarationSyntax accessor)
    {
        var expression = accessor.ExpressionBody?.Expression;
        if (expression is null && accessor.Body?.Statements is [ReturnStatementSyntax returnStatement])
        {
            expression = returnStatement.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether an expression binds directly to the backing field, checking a supplied name syntactically before binding once.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="fieldName">The expected backing-field name, or <see langword="null"/> to skip the name check.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression reads the field.</returns>
    private static bool IsFieldRead(
        SemanticModel model,
        ExpressionSyntax? expression,
        IFieldSymbol field,
        string? fieldName,
        CancellationToken cancellationToken) =>
        expression is not null
            && MatchesFieldName(expression, fieldName)
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, cancellationToken).Symbol, field);

    /// <summary>Returns whether an expression names the expected backing field, before anything is bound.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="fieldName">The expected backing-field name, or <see langword="null"/> when any name is accepted.</param>
    /// <returns><see langword="true"/> when no name is expected or the expression is a direct reference to it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesFieldName(ExpressionSyntax expression, string? fieldName) =>
        fieldName is null || (TryGetFieldName(expression, out var name) && string.Equals(name, fieldName, StringComparison.Ordinal));

    /// <summary>Returns whether a write accessor directly assigns <c>value</c> to the backing field, checking a supplied name syntactically before binding once.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="accessor">The write accessor.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="fieldName">The expected backing-field name, or <see langword="null"/> to skip the name check.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a trivial write accessor.</returns>
    private static bool IsTrivialSet(
        SemanticModel model,
        AccessorDeclarationSyntax accessor,
        IFieldSymbol field,
        string? fieldName,
        CancellationToken cancellationToken)
    {
        var expression = accessor.ExpressionBody?.Expression;
        if (expression is null && accessor.Body?.Statements is [ExpressionStatementSyntax statement])
        {
            expression = statement.Expression;
        }

        return expression is AssignmentExpressionSyntax { Left: var left, Right: IdentifierNameSyntax { Identifier.Text: SetterValueParameterName } }
            && MatchesFieldName(left, fieldName)
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(left, cancellationToken).Symbol, field);
    }

    /// <summary>Returns the referenced field name from a simple identifier or <c>this.</c>-qualified access.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="fieldName">The extracted field name.</param>
    /// <returns><see langword="true"/> when the expression is a direct field reference.</returns>
    private static bool TryGetFieldName(ExpressionSyntax? expression, out string? fieldName)
    {
        fieldName = null;
        switch (expression)
        {
            case IdentifierNameSyntax identifier:
            {
                fieldName = GetIdentifierText(identifier.Identifier);
                return true;
            }

            case MemberAccessExpressionSyntax
                {
                    Expression: ThisExpressionSyntax,
                    Name: IdentifierNameSyntax identifier,
                }:
            {
                fieldName = GetIdentifierText(identifier.Identifier);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Returns the source identifier text, unescaping verbatim identifiers only when needed.</summary>
    /// <param name="identifier">The identifier token.</param>
    /// <returns>The comparison-ready identifier text.</returns>
    private static string GetIdentifierText(SyntaxToken identifier) =>
        identifier.Text is ['@', ..] ? identifier.ValueText : identifier.Text;
}
