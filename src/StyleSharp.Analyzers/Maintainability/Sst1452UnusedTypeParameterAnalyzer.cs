// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags generic type parameters nothing in the declaration references (SST1452). An unused type
/// parameter forces every caller to supply a meaningless type argument and usually marks a
/// refactoring leftover. The check is purely syntactic — one token scan over the declaration, no
/// semantic binds. Declarations whose arity is not theirs to change are skipped: partial types
/// and methods, overrides, interface implementations, and virtual or abstract members.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1452UnusedTypeParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The largest arity the bitmask scan tracks; larger declarations are skipped.</summary>
    private const int MaximumTrackedParameters = 64;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UnusedTypeParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Scans one generic declaration for type parameters nothing references.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetScanShape(context.Node, out var typeParameterList, out var modifiers))
        {
            return;
        }

        if (typeParameterList is not { Parameters.Count: > 0 })
        {
            return;
        }

        if (HasArityLockingModifier(modifiers) || IsExplicitInterfaceImplementation(context.Node))
        {
            return;
        }

        var parameters = typeParameterList.Parameters;
        if (parameters.Count > MaximumTrackedParameters)
        {
            return;
        }

        var state = new ScanState(parameters, typeParameterList.Span);
        DescendantTraversalHelper.VisitDescendantTokens(context.Node, ref state, static (in SyntaxToken token, ref ScanState scan) => scan.Observe(token));

        for (var i = 0; i < parameters.Count; i++)
        {
            if (state.IsSeen(i))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.UnusedTypeParameter,
                parameters[i].SyntaxTree,
                parameters[i].Identifier.Span,
                parameters[i].Identifier.ValueText));
        }
    }

    /// <summary>Extracts the type parameter list and modifiers of a supported declaration.</summary>
    /// <param name="node">The declaration node.</param>
    /// <param name="typeParameterList">The declaration's type parameter list.</param>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the node is a supported declaration.</returns>
    private static bool TryGetScanShape(SyntaxNode node, out TypeParameterListSyntax? typeParameterList, out SyntaxTokenList modifiers)
    {
        switch (node)
        {
            case MethodDeclarationSyntax method:
            {
                typeParameterList = method.TypeParameterList;
                modifiers = method.Modifiers;
                return true;
            }

            case LocalFunctionStatementSyntax localFunction:
            {
                typeParameterList = localFunction.TypeParameterList;
                modifiers = localFunction.Modifiers;
                return true;
            }

            case TypeDeclarationSyntax type:
            {
                typeParameterList = type.TypeParameterList;
                modifiers = type.Modifiers;
                return true;
            }

            default:
            {
                typeParameterList = null;
                modifiers = default;
                return false;
            }
        }
    }

    /// <summary>Returns whether the declaration's arity is fixed by polymorphism or partials.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the declaration should be skipped.</returns>
    private static bool HasArityLockingModifier(SyntaxTokenList modifiers)
        => modifiers.Any(SyntaxKind.PartialKeyword)
            || modifiers.Any(SyntaxKind.OverrideKeyword)
            || modifiers.Any(SyntaxKind.VirtualKeyword)
            || modifiers.Any(SyntaxKind.AbstractKeyword);

    /// <summary>Returns whether a method explicitly implements an interface member.</summary>
    /// <param name="node">The declaration node.</param>
    /// <returns><see langword="true"/> when the declaration is an explicit implementation.</returns>
    private static bool IsExplicitInterfaceImplementation(SyntaxNode node)
        => node is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null };

    /// <summary>Tracks which type parameter names have been seen outside the parameter list.</summary>
    private sealed class ScanState
    {
        /// <summary>The declared type parameters.</summary>
        private readonly SeparatedSyntaxList<TypeParameterSyntax> _parameters;

        /// <summary>The type parameter list span, excluded from usage.</summary>
        private readonly TextSpan _listSpan;

        /// <summary>The bitmask of parameters seen so far.</summary>
        private ulong _seenMask;

        /// <summary>The number of parameters not yet seen.</summary>
        private int _remaining;

        /// <summary>Initializes a new instance of the <see cref="ScanState"/> class.</summary>
        /// <param name="parameters">The declared type parameters.</param>
        /// <param name="listSpan">The type parameter list span.</param>
        public ScanState(SeparatedSyntaxList<TypeParameterSyntax> parameters, TextSpan listSpan)
        {
            _parameters = parameters;
            _listSpan = listSpan;
            _seenMask = 0;
            _remaining = parameters.Count;
        }

        /// <summary>Returns whether the parameter at an index was seen.</summary>
        /// <param name="index">The parameter index.</param>
        /// <returns><see langword="true"/> when the parameter is referenced.</returns>
        public bool IsSeen(int index) => (_seenMask & (1UL << index)) != 0;

        /// <summary>Records one token and reports whether the scan should continue.</summary>
        /// <param name="token">The current descendant token.</param>
        /// <returns><see langword="false"/> once every parameter has been seen.</returns>
        public bool Observe(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || _listSpan.Contains(token.Span) || IsConstraintClauseName(token))
            {
                return _remaining > 0;
            }

            var text = token.ValueText;
            for (var i = 0; i < _parameters.Count; i++)
            {
                if (!IsSeen(i) && _parameters[i].Identifier.ValueText == text)
                {
                    _seenMask |= 1UL << i;
                    _remaining--;
                }
            }

            return _remaining > 0;
        }

        /// <summary>Returns whether a token is the declared name of a constraint clause.</summary>
        /// <param name="token">The identifier token.</param>
        /// <returns><see langword="true"/> when the token only restates the parameter in <c>where</c>.</returns>
        private static bool IsConstraintClauseName(SyntaxToken token)
            => token.Parent is IdentifierNameSyntax { Parent: TypeParameterConstraintClauseSyntax clause } name && clause.Name == name;
    }
}
