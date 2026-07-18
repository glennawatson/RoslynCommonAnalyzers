// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a classic partial method — a <c>partial void</c> defining declaration with no accessibility modifier,
/// no return value, and no <c>out</c> parameters — that has no implementing declaration anywhere in the
/// compilation (SST2468). Such a declaration and every call to it are silently removed by the compiler, so a
/// hook the author expected to run does nothing.
/// </summary>
/// <remarks>
/// <para>
/// Only the classic form is reported. A partial method that carries an accessibility modifier, returns a value,
/// or takes an <c>out</c> parameter is an "extended" partial method that the compiler requires an implementation
/// for; the missing body is already a compiler error, so those are left to the compiler and never reported here.
/// </para>
/// <para>
/// The clean path is a syntactic prepass over the method declaration: it must be a bare signature terminated by
/// a semicolon, return <c>void</c>, take no <c>out</c> parameter, and carry the <c>partial</c> modifier with no
/// accessibility modifier. Only a declaration that survives that prepass binds, and it is reported only when the
/// semantic model confirms it is a partial definition whose
/// <see cref="IMethodSymbol.PartialImplementationPart"/> is <see langword="null"/>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2468UnimplementedPartialMethodAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.UnimplementedPartialMethod);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Returns whether a method declaration is the classic <c>partial void</c> defining form.</summary>
    /// <param name="declaration">The method declaration to inspect.</param>
    /// <returns><see langword="true"/> for a bodyless, <c>void</c>, non-<c>out</c>, unqualified partial declaration.</returns>
    internal static bool IsClassicPartialDefiningDeclaration(MethodDeclarationSyntax declaration)
    {
        // The defining declaration is a bare signature terminated by a semicolon; the implementing part has a body.
        if (declaration.Body is not null || declaration.ExpressionBody is not null)
        {
            return false;
        }

        // A classic partial method returns void; a value return makes it extended and compiler-owned.
        if (declaration.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return false;
        }

        // An out parameter makes it extended and compiler-owned.
        if (HasOutParameter(declaration.ParameterList))
        {
            return false;
        }

        // It must carry 'partial' and no accessibility modifier; an accessibility modifier makes it extended.
        return IsUnqualifiedPartial(declaration.Modifiers);
    }

    /// <summary>Analyzes one method declaration for an unimplemented classic partial method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (!IsClassicPartialDefiningDeclaration(declaration))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsPartialDefinition: true, PartialImplementationPart: null })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            CorrectnessRules.UnimplementedPartialMethod,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }

    /// <summary>Returns whether any parameter carries the <c>out</c> modifier.</summary>
    /// <param name="parameterList">The declaration's parameter list.</param>
    /// <returns><see langword="true"/> when at least one parameter is an <c>out</c> parameter.</returns>
    private static bool HasOutParameter(ParameterListSyntax parameterList)
    {
        var parameters = parameterList.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var modifiers = parameters[i].Modifiers;
            for (var j = 0; j < modifiers.Count; j++)
            {
                if (modifiers[j].RawKind == (int)SyntaxKind.OutKeyword)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a modifier list carries <c>partial</c> and no accessibility modifier.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> for a partial declaration with no accessibility modifier.</returns>
    private static bool IsUnqualifiedPartial(SyntaxTokenList modifiers)
    {
        var hasPartial = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var rawKind = modifiers[i].RawKind;
            if (rawKind == (int)SyntaxKind.PartialKeyword)
            {
                hasPartial = true;
            }
            else if (IsAccessibilityModifier(rawKind))
            {
                return false;
            }
        }

        return hasPartial;
    }

    /// <summary>Returns whether a modifier kind is one of the four accessibility modifiers.</summary>
    /// <param name="rawKind">The modifier token's raw syntax kind.</param>
    /// <returns><see langword="true"/> for <c>public</c>, <c>private</c>, <c>protected</c>, or <c>internal</c>.</returns>
    private static bool IsAccessibilityModifier(int rawKind)
        => rawKind is (int)SyntaxKind.PublicKeyword
            or (int)SyntaxKind.PrivateKeyword
            or (int)SyntaxKind.ProtectedKeyword
            or (int)SyntaxKind.InternalKeyword;
}
