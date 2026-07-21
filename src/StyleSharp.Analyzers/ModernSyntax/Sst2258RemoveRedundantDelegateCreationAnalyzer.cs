// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method group wrapped in an explicit delegate creation the target already supplies (SST2258):
/// <c>Changed += new EventHandler(OnChanged)</c> can drop the wrapper to <c>Changed += OnChanged</c>. Reported
/// only when the argument is a method group, the created type is a delegate, the syntactic position accepts a
/// method-group conversion, and dropping the wrapper provably binds to the same delegate.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2258RemoveRedundantDelegateCreationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.RemoveRedundantDelegateCreation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    /// <summary>Returns the wrapped method group when the delegate creation is a redundant wrapper.</summary>
    /// <param name="creation">The delegate creation to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="methodGroup">The wrapped method group.</param>
    /// <param name="delegateName">The created delegate type's name, for the diagnostic message.</param>
    /// <returns><see langword="true"/> when the wrapper can be dropped safely.</returns>
    internal static bool TryGetUnwrapped(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax methodGroup,
        out string delegateName)
    {
        methodGroup = null!;
        delegateName = string.Empty;

        if (creation.Initializer is not null || creation.ArgumentList is not { Arguments.Count: 1 } argumentList)
        {
            return false;
        }

        var argument = argumentList.Arguments[0];
        if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != 0)
        {
            return false;
        }

        var typeInfo = model.GetTypeInfo(creation, cancellationToken);
        if ((typeInfo.Type ?? typeInfo.ConvertedType) is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType)
        {
            return false;
        }

        if (!IsMethodGroup(model.GetSymbolInfo(argument.Expression, cancellationToken))
            || !DropStillBinds(creation, argument.Expression, delegateType, model))
        {
            return false;
        }

        methodGroup = argument.Expression;
        delegateName = delegateType.Name;
        return true;
    }

    /// <summary>Reports a redundant delegate wrapper around a method group.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (!TryGetUnwrapped(creation, context.SemanticModel, context.CancellationToken, out _, out var delegateName))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.RemoveRedundantDelegateCreation, creation.GetLocation(), delegateName));
    }

    /// <summary>Returns whether a symbol lookup resolved to a method group.</summary>
    /// <param name="info">The symbol info for the wrapped argument.</param>
    /// <returns><see langword="true"/> when the argument names one or more methods.</returns>
    private static bool IsMethodGroup(SymbolInfo info)
    {
        if (info.Symbol is IMethodSymbol)
        {
            return true;
        }

        if (info.CandidateReason is not (CandidateReason.OverloadResolutionFailure or CandidateReason.MemberGroup)
            || info.CandidateSymbols.Length == 0)
        {
            return false;
        }

        foreach (var candidate in info.CandidateSymbols)
        {
            if (candidate is not IMethodSymbol)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether dropping the wrapper keeps the same delegate binding.</summary>
    /// <param name="creation">The delegate creation.</param>
    /// <param name="methodGroup">The wrapped method group.</param>
    /// <param name="delegateType">The created delegate type.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns><see langword="true"/> when the bare method group converts to the same delegate in place.</returns>
    private static bool DropStillBinds(
        ObjectCreationExpressionSyntax creation,
        ExpressionSyntax methodGroup,
        INamedTypeSymbol delegateType,
        SemanticModel model)
    {
        var conversion = model.ClassifyConversion(methodGroup, delegateType);
        return conversion.Exists && conversion.IsMethodGroup && IsMethodGroupTargetContext(creation);
    }

    /// <summary>Returns whether the creation sits where a bare method group would also be convertible.</summary>
    /// <param name="creation">The delegate creation.</param>
    /// <returns><see langword="true"/> for assignment, initializer, return, arrow, and cast positions.</returns>
    /// <remarks>
    /// A method group has no type of its own, so it converts only where a delegate target is supplied directly.
    /// An operand of <c>+</c>/<c>-</c> delegate combination needs an already-typed delegate, so dropping the
    /// wrapper there would not compile; an argument position is excluded because a method group cannot always be
    /// re-bound there without changing overload resolution.
    /// </remarks>
    private static bool IsMethodGroupTargetContext(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is EqualsValueClauseSyntax or ReturnStatementSyntax or ArrowExpressionClauseSyntax or CastExpressionSyntax)
        {
            return true;
        }

        return creation.Parent is AssignmentExpressionSyntax assignment
            && assignment.Right == creation
            && IsDelegateAssignmentKind(assignment.Kind());
    }

    /// <summary>Returns whether an assignment kind takes a method-group right-hand side.</summary>
    /// <param name="kind">The assignment expression's kind.</param>
    /// <returns><see langword="true"/> for <c>=</c>, <c>+=</c>, and <c>-=</c>.</returns>
    private static bool IsDelegateAssignmentKind(SyntaxKind kind)
        => kind is SyntaxKind.SimpleAssignmentExpression or SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression;
}
