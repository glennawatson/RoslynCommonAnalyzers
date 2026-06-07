// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>base.</c> member access whose member the current type does not override (SST1100).
/// In that case <c>base.X</c> and <c>this.X</c> bind to the same member, so the <c>base.</c> prefix
/// is redundant; the prefix is meaningful only when it reaches a member the type itself overrides.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DoNotPrefixWithBaseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.DoNotPrefixWithBase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    /// <summary>Returns whether the type declares an override with the supplied member name.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="name">The member name to inspect.</param>
    /// <returns><see langword="true"/> when a matching override exists.</returns>
    internal static bool HasOverrideNamed(TypeDeclarationSyntax type, string name)
    {
        for (var i = 0; i < type.Members.Count; i++)
        {
            switch (type.Members[i])
            {
                case MethodDeclarationSyntax method
                    when method.Identifier.ValueText == name
                    && ModifierListHelper.Contains(method.Modifiers, SyntaxKind.OverrideKeyword):
                case PropertyDeclarationSyntax property
                    when property.Identifier.ValueText == name
                    && ModifierListHelper.Contains(property.Modifiers, SyntaxKind.OverrideKeyword):
                case EventDeclarationSyntax @event
                    when @event.Identifier.ValueText == name
                    && ModifierListHelper.Contains(@event.Modifiers, SyntaxKind.OverrideKeyword):
                    return true;
            }
        }

        return false;
    }

    /// <summary>Reports a redundant <c>base.</c> member access.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Expression is not BaseExpressionSyntax)
        {
            return;
        }

        if (access.Name is SimpleNameSyntax simpleName
            && access.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDeclaration
            && !HasOverrideNamed(typeDeclaration, GetIdentifierText(simpleName.Identifier)))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotPrefixWithBase, access.Expression.GetLocation()));
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not { } member)
        {
            return;
        }

        var enclosingType = context.SemanticModel.GetEnclosingSymbol(access.SpanStart, context.CancellationToken)?.ContainingType;
        if (enclosingType is null || TypeOverrides(enclosingType, member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotPrefixWithBase, access.Expression.GetLocation()));
    }

    /// <summary>Returns whether the type declares an override that reaches the given base member.</summary>
    /// <param name="type">The enclosing type.</param>
    /// <param name="baseMember">The member reached through <c>base.</c>.</param>
    /// <returns><see langword="true"/> when an override in the type makes the <c>base.</c> prefix meaningful.</returns>
    private static bool TypeOverrides(INamedTypeSymbol type, ISymbol baseMember)
    {
        foreach (var candidate in type.GetMembers(baseMember.Name))
        {
            if (!candidate.IsOverride)
            {
                continue;
            }

            for (var overridden = OverriddenOf(candidate); overridden is not null; overridden = OverriddenOf(overridden))
            {
                if (SymbolEqualityComparer.Default.Equals(overridden, baseMember))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the member a method, property, or event override directly overrides.</summary>
    /// <param name="symbol">The overriding member.</param>
    /// <returns>The overridden member, or <see langword="null"/>.</returns>
    private static ISymbol? OverriddenOf(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null
    };

    /// <summary>Returns the source identifier text, unescaping verbatim identifiers only when needed.</summary>
    /// <param name="identifier">The identifier token.</param>
    /// <returns>The comparison-ready identifier text.</returns>
    private static string GetIdentifierText(SyntaxToken identifier)
        => identifier.Text is ['@', ..] ? identifier.ValueText : identifier.Text;
}
