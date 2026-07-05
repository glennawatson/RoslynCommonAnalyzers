// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Finds class and struct constructors whose body only copies constructor parameters into
/// instance fields or properties. The analyzer binds only assignment pairs after a strict syntax
/// prefilter, and it requires every parameter to be stored exactly once so the suggestion stays a
/// straightforward primary-constructor migration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2241PrimaryConstructorStorageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The largest constructor arity tracked by the bitmask scan.</summary>
    private const int MaximumTrackedParameters = 64;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UsePrimaryConstructorStorage);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
    }

    /// <summary>Reports a constructor that only stores each parameter once.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (!TryGetCandidate(constructor, out var body))
        {
            return;
        }

        if (!StoresEveryParameterOnce(constructor, body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UsePrimaryConstructorStorage,
            constructor.Identifier.GetLocation(),
            constructor.Identifier.ValueText));
    }

    /// <summary>Extracts a constructor body that is eligible for primary-constructor storage analysis.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <param name="body">The constructor body.</param>
    /// <returns><see langword="true"/> when the constructor has the strict storage shape.</returns>
    private static bool TryGetCandidate(ConstructorDeclarationSyntax constructor, out BlockSyntax body)
    {
        body = null!;
        if (constructor.Parent is not TypeDeclarationSyntax type || type is RecordDeclarationSyntax || type.ParameterList is not null)
        {
            return false;
        }

        var parameterCount = constructor.ParameterList.Parameters.Count;
        if (parameterCount == 0
            || parameterCount > MaximumTrackedParameters
            || constructor.Body is not { } constructorBody
            || constructorBody.Statements.Count != parameterCount
            || constructor.Initializer is { ArgumentList.Arguments.Count: > 0 })
        {
            return false;
        }

        body = constructorBody;
        return true;
    }

    /// <summary>Returns whether each constructor parameter is copied into instance storage once.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <param name="body">The constructor body.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when every parameter is stored once.</returns>
    private static bool StoresEveryParameterOnce(
        ConstructorDeclarationSyntax constructor,
        BlockSyntax body,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        ulong seenMask = 0;
        for (var i = 0; i < body.Statements.Count; i++)
        {
            if (!TryGetStoredParameterIndex(constructor, body.Statements[i], model, cancellationToken, out var parameterIndex)
                || (seenMask & (1UL << parameterIndex)) != 0)
            {
                return false;
            }

            seenMask |= 1UL << parameterIndex;
        }

        return seenMask == ((1UL << constructor.ParameterList.Parameters.Count) - 1UL);
    }

    /// <summary>Returns the parameter index stored by a constructor statement.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <param name="statement">The candidate storage statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="parameterIndex">The stored parameter index.</param>
    /// <returns><see langword="true"/> when the statement stores one constructor parameter.</returns>
    private static bool TryGetStoredParameterIndex(
        ConstructorDeclarationSyntax constructor,
        StatementSyntax statement,
        SemanticModel model,
        CancellationToken cancellationToken,
        out int parameterIndex)
    {
        parameterIndex = -1;
        if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment }
            || assignment.Right is not IdentifierNameSyntax parameterName
            || model.GetSymbolInfo(parameterName, cancellationToken).Symbol is not IParameterSymbol parameter
            || !IsConstructorParameter(constructor, parameter, model, cancellationToken)
            || !TryGetParameterIndex(constructor.ParameterList.Parameters, parameter.Name, out parameterIndex))
        {
            return false;
        }

        return IsInstanceStorageTarget(assignment.Left, model, cancellationToken);
    }

    /// <summary>Returns whether a parameter belongs to the constructor being analyzed.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <param name="parameter">The parameter symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the parameter belongs to this constructor.</returns>
    private static bool IsConstructorParameter(
        ConstructorDeclarationSyntax constructor,
        IParameterSymbol parameter,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } method
            && SymbolEqualityComparer.Default.Equals(method, model.GetDeclaredSymbol(constructor, cancellationToken));
    }

    /// <summary>Maps a parameter name to its index in the constructor parameter list.</summary>
    /// <param name="parameters">The parameter list.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="index">The matched index.</param>
    /// <returns><see langword="true"/> when a matching parameter exists.</returns>
    private static bool TryGetParameterIndex(SeparatedSyntaxList<ParameterSyntax> parameters, string name, out int index)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == name)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>Returns whether an assignment target is an instance field or property.</summary>
    /// <param name="left">The assignment target.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the target stores instance state.</returns>
    private static bool IsInstanceStorageTarget(ExpressionSyntax left, SemanticModel model, CancellationToken cancellationToken)
    {
        var target = left is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess
            ? memberAccess.Name
            : left;

        return model.GetSymbolInfo(target, cancellationToken).Symbol switch
        {
            IFieldSymbol { IsStatic: false } => true,
            IPropertySymbol { IsStatic: false, SetMethod: not null } => true,
            _ => false
        };
    }
}
