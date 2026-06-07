// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an instance member (field, property, method, or event) accessed as a bare identifier
/// without a <c>this.</c> prefix (SST1101). Disabled by default: most .NET style guides, and this
/// repository, deliberately omit <c>this.</c>. The bare identifier must bind to a non-static member
/// of the enclosing type and must not already be qualified.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrefixLocalCallsWithThisAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.PrefixLocalCallsWithThis);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName);
    }

    /// <summary>Reports a bare instance-member reference that should carry a <c>this.</c> prefix.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (!IsBareReference(identifier))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        if (!IsInstanceMemberOfEnclosingType(symbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.PrefixLocalCallsWithThis, identifier.GetLocation(), identifier.Identifier.ValueText));
    }

    /// <summary>Returns whether the identifier is an unqualified reference that could take a <c>this.</c> prefix.</summary>
    /// <param name="identifier">The identifier name.</param>
    /// <returns><see langword="true"/> when the identifier is a bare reference in value position.</returns>
    private static bool IsBareReference(IdentifierNameSyntax identifier)
    {
        switch (identifier.Parent)
        {
            // Already qualified ('x.Name', 'this.Name', '?.Name') or a type/namespace name.
            case MemberAccessExpressionSyntax access when access.Name == identifier:
            case MemberBindingExpressionSyntax:
            case QualifiedNameSyntax:
            case AliasQualifiedNameSyntax:
            // A member name in an object initializer ('new T { Name = ... }') cannot take 'this.'.
            case AssignmentExpressionSyntax assignment when assignment.Left == identifier && assignment.Parent is InitializerExpressionSyntax:
            // The name of a named argument ('M(name: value)').
            case NameColonSyntax:
            case NameEqualsSyntax:
                return false;
            default:
                return !IsInNameof(identifier);
        }
    }

    /// <summary>Returns whether the identifier sits inside a <c>nameof(...)</c> expression.</summary>
    /// <param name="node">The identifier node.</param>
    /// <returns><see langword="true"/> when an enclosing <c>nameof</c> invocation is found before the statement boundary.</returns>
    private static bool IsInNameof(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } }:
                    return true;
                case StatementSyntax or MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether the symbol is a non-static field, property, method, or event of a type.</summary>
    /// <param name="symbol">The bound symbol.</param>
    /// <returns><see langword="true"/> when a <c>this.</c> prefix would apply.</returns>
    private static bool IsInstanceMemberOfEnclosingType(ISymbol? symbol) =>
        symbol is { IsStatic: false, ContainingType: not null }
            && symbol switch
            {
                IMethodSymbol method => method.MethodKind is MethodKind.Ordinary,
                IFieldSymbol => true,
                IPropertySymbol => true,
                IEventSymbol => true,
                _ => false
            };
}
