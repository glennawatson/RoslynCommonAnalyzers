// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a single method or type declaration that carries both <c>[AllowAnonymous]</c> and
/// <c>[Authorize]</c> (SES1507). ASP.NET Core's authorization middleware honours the anonymous marker
/// first and short-circuits, so the co-located <c>[Authorize]</c> never runs: the declaration is
/// reachable without authentication even though it reads as protected. The rule reports the misleading
/// <c>[Authorize]</c> attribute. It is purely local -- both markers must sit on the same declaration --
/// so the deliberate pattern of an <c>[Authorize]</c> type with one <c>[AllowAnonymous]</c> member is not
/// flagged. Each attribute is bound to its symbol and matched by attribute class (including a subclass of
/// either marker), never by written name, so <c>[Authorize]</c>, <c>[AuthorizeAttribute]</c>, and a fully
/// qualified spelling all count. The whole rule is gated on
/// <c>Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute</c> resolving; a project without ASP.NET
/// Core authorization registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1507ConflictingAnonymousAuthorizationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the marker whose presence enables the rule and wins at runtime.</summary>
    private const string AllowAnonymousMetadataName = "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute";

    /// <summary>The metadata name of the dead authorization marker the rule reports.</summary>
    private const string AuthorizeMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";

    /// <summary>The message word used when the conflict sits on a method declaration.</summary>
    private const string MethodWord = "method";

    /// <summary>The message word used when the conflict sits on a type declaration.</summary>
    private const string TypeWord = "type";

    /// <summary>The number of attributes a declaration must carry before the conflict is possible.</summary>
    private const int MinimumConflictingAttributeCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ConflictingAnonymousAuthorization);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var allowAnonymous = start.Compilation.GetTypeByMetadataName(AllowAnonymousMetadataName);
            var authorize = start.Compilation.GetTypeByMetadataName(AuthorizeMetadataName);
            if (allowAnonymous is null || authorize is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaration(nodeContext, allowAnonymous, authorize),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        });
    }

    /// <summary>Reports SES1507 when one declaration carries both authorization markers.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="allowAnonymous">The resolved <c>AllowAnonymousAttribute</c> type.</param>
    /// <param name="authorize">The resolved <c>AuthorizeAttribute</c> type.</param>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol allowAnonymous, INamedTypeSymbol authorize)
    {
        var declaration = (MemberDeclarationSyntax)context.Node;
        var attributeLists = declaration.AttributeLists;

        // Syntactic prefilter: the conflict needs at least two attributes on this one declaration.
        if (!HasAtLeastTwoAttributes(attributeLists))
        {
            return;
        }

        if (FindConflictingAuthorizeAttribute(context, attributeLists, allowAnonymous, authorize) is not { } authorizeAttribute)
        {
            return;
        }

        var declarationWord = context.Node.IsKind(SyntaxKind.MethodDeclaration) ? MethodWord : TypeWord;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ConflictingAnonymousAuthorization,
            authorizeAttribute.SyntaxTree,
            authorizeAttribute.Span,
            declarationWord));
    }

    /// <summary>Finds the misleading <c>[Authorize]</c> attribute when the declaration also carries <c>[AllowAnonymous]</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <param name="allowAnonymous">The resolved <c>AllowAnonymousAttribute</c> type.</param>
    /// <param name="authorize">The resolved <c>AuthorizeAttribute</c> type.</param>
    /// <returns>The first authorize attribute when both markers are present; otherwise <see langword="null"/>.</returns>
    private static AttributeSyntax? FindConflictingAuthorizeAttribute(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol allowAnonymous,
        INamedTypeSymbol authorize)
    {
        AttributeSyntax? authorizeAttribute = null;
        var sawAllowAnonymous = false;

        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attribute = attributes[j];
                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeType })
                {
                    continue;
                }

                if (IsOrDerivesFrom(attributeType, allowAnonymous))
                {
                    sawAllowAnonymous = true;
                }
                else if (authorizeAttribute is null && IsOrDerivesFrom(attributeType, authorize))
                {
                    authorizeAttribute = attribute;
                }
            }
        }

        return sawAllowAnonymous ? authorizeAttribute : null;
    }

    /// <summary>Returns whether a declaration carries at least two attributes across all its lists.</summary>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <returns><see langword="true"/> when two or more attributes are present.</returns>
    private static bool HasAtLeastTwoAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var count = 0;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            count += attributeLists[i].Attributes.Count;
            if (count >= MinimumConflictingAttributeCount)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute class is, or derives from, a marker attribute type.</summary>
    /// <param name="attributeType">The bound attribute class.</param>
    /// <param name="marker">The marker attribute type to match.</param>
    /// <returns><see langword="true"/> when the attribute is the marker or a subclass of it.</returns>
    private static bool IsOrDerivesFrom(INamedTypeSymbol attributeType, INamedTypeSymbol marker)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, marker))
            {
                return true;
            }
        }

        return false;
    }
}
