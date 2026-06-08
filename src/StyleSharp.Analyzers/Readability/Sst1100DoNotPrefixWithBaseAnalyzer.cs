// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>base.</c> member access whose member the current type neither overrides nor hides
/// (SST1100). In that case <c>base.X</c> and <c>this.X</c> bind to the same member, so the
/// <c>base.</c> prefix is redundant. The prefix is meaningful when the type declares its own member
/// with that name — an <c>override</c> (so <c>base.</c> reaches the base implementation) or a
/// <c>new</c>/hiding member (so <c>base.</c> is required to bypass the local member).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1100DoNotPrefixWithBaseAnalyzer : DiagnosticAnalyzer
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

    /// <summary>Returns whether the type declares its own member (override or hiding) with the supplied name.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="name">The member name to inspect.</param>
    /// <returns><see langword="true"/> when the type declares a member with that name.</returns>
    internal static bool HasOwnMemberNamed(TypeDeclarationSyntax type, string name)
    {
        for (var i = 0; i < type.Members.Count; i++)
        {
            if (DeclaresName(type.Members[i], name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member declaration introduces the supplied name.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="name">The member name to inspect.</param>
    /// <returns><see langword="true"/> when the declaration introduces the name.</returns>
    private static bool DeclaresName(MemberDeclarationSyntax member, string name) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText == name,
        PropertyDeclarationSyntax property => property.Identifier.ValueText == name,
        EventDeclarationSyntax @event => @event.Identifier.ValueText == name,
        EventFieldDeclarationSyntax eventField => DeclaresVariable(eventField.Declaration, name),
        FieldDeclarationSyntax field => DeclaresVariable(field.Declaration, name),
        _ => false
    };

    /// <summary>Returns whether a variable declaration declares the supplied name.</summary>
    /// <param name="declaration">The variable declaration (field or event-field).</param>
    /// <param name="name">The member name to inspect.</param>
    /// <returns><see langword="true"/> when one of the declared variables matches.</returns>
    private static bool DeclaresVariable(VariableDeclarationSyntax declaration, string name)
    {
        foreach (var variable in declaration.Variables)
        {
            if (variable.Identifier.ValueText == name)
            {
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

        // Fast path: when the type declares no member with this name, this.X and base.X both bind to
        // the same inherited member, so base. is redundant — no semantic model is needed. When the type
        // does declare its own member (an override or a hiding member), fall through to the semantic
        // check below, which decides precisely whether base. reaches a different member.
        if (access.Name is SimpleNameSyntax simpleName
            && access.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDeclaration
            && !HasOwnMemberNamed(typeDeclaration, GetIdentifierText(simpleName.Identifier)))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotPrefixWithBase, access.Expression.GetLocation()));
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not { } member)
        {
            return;
        }

        var enclosingType = context.SemanticModel.GetEnclosingSymbol(access.SpanStart, context.CancellationToken)?.ContainingType;
        if (enclosingType is null || EnclosingTypeRedeclares(enclosingType, member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotPrefixWithBase, access.Expression.GetLocation()));
    }

    /// <summary>Returns whether the type declares its own member that overrides or hides the given base member.</summary>
    /// <param name="type">The enclosing type.</param>
    /// <param name="baseMember">The member reached through <c>base.</c>.</param>
    /// <returns><see langword="true"/> when the type makes the <c>base.</c> prefix meaningful.</returns>
    private static bool EnclosingTypeRedeclares(INamedTypeSymbol type, ISymbol baseMember)
    {
        foreach (var candidate in type.GetMembers(baseMember.Name))
        {
            // A member the type declares itself that is not an override hides the inherited member,
            // so base. is required to reach the base implementation.
            if (!candidate.IsOverride)
            {
                return true;
            }

            // An override makes base. meaningful only when it reaches the member being accessed.
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
