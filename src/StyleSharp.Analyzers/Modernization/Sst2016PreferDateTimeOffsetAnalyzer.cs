// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>DateTime</c> written as the type of an externally visible field, property, parameter or
/// return type (SST2016) — the places a moment leaves the assembly and its offset is no longer written
/// down anywhere.
/// </summary>
/// <remarks>
/// <para>
/// This rule is about the <em>declared type</em>, never about an expression. A clock read
/// (<c>DateTime.Now</c>, <c>DateTime.UtcNow</c>) belongs to SST2010, which asks a different question — where
/// the time came from — and this rule never looks at a member access, so the two cannot both fire on the
/// same expression.
/// </para>
/// <para>
/// A local is not reported: a value that never leaves the method cannot be misread by anyone else. Neither
/// is a member whose shape is fixed elsewhere — an <c>override</c>, or an implementation of an interface
/// member — because the declaration that owns the type is the one worth changing, and it is reported on its
/// own if it lives in this compilation.
/// </para>
/// <para>
/// The suggestion is gated on <c>System.DateTimeOffset</c> resolving in the analyzed compilation, so a
/// framework that has no such type never sees a diagnostic it cannot act on.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2016PreferDateTimeOffsetAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>DateTime</c> type name as it is written in source.</summary>
    private const string DateTimeTypeName = "DateTime";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.PreferDateTimeOffset);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            // The advice is only honest where the replacement exists: no DateTimeOffset, no rule.
            if (start.Compilation.GetTypeByMetadataName(ClockPropertyAccess.DateTimeOffsetMetadataName) is null)
            {
                return;
            }

            var dateTime = start.Compilation.GetTypeByMetadataName(ClockPropertyAccess.DateTimeMetadataName);
            if (dateTime is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeField(nodeContext, dateTime), SyntaxKind.FieldDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeProperty(nodeContext, dateTime), SyntaxKind.PropertyDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, dateTime), SyntaxKind.MethodDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeDelegate(nodeContext, dateTime), SyntaxKind.DelegateDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeParameter(nodeContext, dateTime), SyntaxKind.Parameter);
        });
    }

    /// <summary>Reports the type of an externally visible field.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context, INamedTypeSymbol dateTime)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var type = field.Declaration.Type;
        if (!IsSpelledDateTime(type))
        {
            return;
        }

        var declarators = field.Declaration.Variables;
        if (declarators.Count == 0)
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declarators[0], context.CancellationToken);
        if (symbol is null || !IsExternallyVisible(symbol))
        {
            return;
        }

        Report(context, type, symbol.Name, dateTime);
    }

    /// <summary>Reports the type of an externally visible property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, INamedTypeSymbol dateTime)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (!IsSpelledDateTime(property.Type) || property.ExplicitInterfaceSpecifier is not null)
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken);
        if (symbol is null || !CanChooseItsOwnType(symbol))
        {
            return;
        }

        Report(context, property.Type, symbol.Name, dateTime);
    }

    /// <summary>Reports the return type of an externally visible method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol dateTime)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsSpelledDateTime(method.ReturnType) || method.ExplicitInterfaceSpecifier is not null)
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        if (symbol is null || !CanChooseItsOwnType(symbol))
        {
            return;
        }

        Report(context, method.ReturnType, symbol.Name, dateTime);
    }

    /// <summary>Reports the return type of an externally visible delegate.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void AnalyzeDelegate(SyntaxNodeAnalysisContext context, INamedTypeSymbol dateTime)
    {
        var declaration = (DelegateDeclarationSyntax)context.Node;
        if (!IsSpelledDateTime(declaration.ReturnType))
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        if (symbol is null || !IsExternallyVisible(symbol))
        {
            return;
        }

        Report(context, declaration.ReturnType, symbol.Name, dateTime);
    }

    /// <summary>Reports the type of a parameter on an externally visible member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, INamedTypeSymbol dateTime)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (parameter.Type is not { } type || !IsSpelledDateTime(type) || !IsOnAMemberSignature(parameter))
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken);
        if (symbol is null || !IsDeclaredByAnExternallyVisibleMember(symbol))
        {
            return;
        }

        Report(context, type, symbol.Name, dateTime);
    }

    /// <summary>Returns whether a parameter belongs to a member signature rather than to a lambda or a local function.</summary>
    /// <param name="parameter">The parameter to classify.</param>
    /// <returns><see langword="true"/> when the parameter is part of a boundary a caller outside the assembly can see.</returns>
    /// <remarks>
    /// Decided on syntax alone, before anything is bound. A record's positional parameter is included: it is
    /// the primary constructor's parameter, and the property the compiler generates from it takes its type.
    /// </remarks>
    private static bool IsOnAMemberSignature(ParameterSyntax parameter)
    {
        if (parameter.Parent is not ParameterListSyntax list)
        {
            return false;
        }

        return list.Parent is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or DelegateDeclarationSyntax
            or IndexerDeclarationSyntax
            or TypeDeclarationSyntax;
    }

    /// <summary>Returns whether a parameter's declaring member is visible outside the assembly and owns its own signature.</summary>
    /// <param name="parameter">The bound parameter.</param>
    /// <returns><see langword="true"/> when the member the parameter belongs to is a boundary that can be changed here.</returns>
    private static bool IsDeclaredByAnExternallyVisibleMember(IParameterSymbol parameter)
    {
        var member = parameter.ContainingSymbol;
        return member is IMethodSymbol method
            ? CanChooseItsOwnType(method)
            : IsExternallyVisible(member);
    }

    /// <summary>Returns whether a member is an externally visible declaration whose type is not dictated elsewhere.</summary>
    /// <param name="symbol">The member to test.</param>
    /// <returns><see langword="true"/> when changing the type here is both worthwhile and possible.</returns>
    /// <remarks>
    /// An override or an interface implementation restates a type chosen by the base declaration. Reporting it
    /// would ask for a change the member cannot make on its own; the declaration that chose the type is the one
    /// reported, if it is in this compilation at all.
    /// </remarks>
    private static bool CanChooseItsOwnType(ISymbol symbol)
        => !symbol.IsOverride
        && IsExternallyVisible(symbol)
        && !InterfaceImplementationLookup.ImplementsInterfaceMember(symbol);

    /// <summary>Reports one declared type once it really is <c>System.DateTime</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="type">The type syntax to report.</param>
    /// <param name="name">The name of the member or parameter the type belongs to.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    private static void Report(SyntaxNodeAnalysisContext context, TypeSyntax type, string name, INamedTypeSymbol dateTime)
    {
        if (!BindsToDateTime(context.SemanticModel, type, dateTime, context.CancellationToken))
        {
            return;
        }

        var diagnostic = DiagnosticHelper.Create(ModernizationRules.PreferDateTimeOffset, type.GetLocation(), name);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Returns whether a type is spelled <c>DateTime</c>, before anything is bound.</summary>
    /// <param name="type">The type syntax to inspect.</param>
    /// <returns><see langword="true"/> when the spelling matches; the symbol is not yet bound.</returns>
    /// <remarks>The nullable form is included: a <c>DateTime?</c> crossing the boundary loses the same offset.</remarks>
    private static bool IsSpelledDateTime(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == DateTimeTypeName,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == DateTimeTypeName,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText == DateTimeTypeName,
        NullableTypeSyntax nullable => IsSpelledDateTime(nullable.ElementType),
        _ => false,
    };

    /// <summary>Returns whether a type really binds to <c>System.DateTime</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The type syntax, already past <see cref="IsSpelledDateTime"/>.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the type is <c>DateTime</c> or <c>DateTime?</c>.</returns>
    private static bool BindsToDateTime(
        SemanticModel model,
        TypeSyntax type,
        INamedTypeSymbol dateTime,
        CancellationToken cancellationToken)
    {
        var bound = model.GetTypeInfo(type, cancellationToken).Type;
        if (bound is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            bound = nullable.TypeArguments[0];
        }

        return SymbolEqualityComparer.Default.Equals(bound, dateTime);
    }

    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The symbol to test.</param>
    /// <returns><see langword="true"/> when the symbol and every type containing it are visible.</returns>
    private static bool IsExternallyVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                {
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }

        return true;
    }
}
