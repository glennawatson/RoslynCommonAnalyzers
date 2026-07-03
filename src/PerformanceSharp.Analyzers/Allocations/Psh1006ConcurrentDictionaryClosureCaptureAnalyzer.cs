// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests using a <c>ConcurrentDictionary</c> factory lambda's own key parameter instead of
/// capturing the outer key variable (PSH1006). An invocation qualifies when it is a
/// <c>GetOrAdd</c> or <c>AddOrUpdate</c> member access whose first argument is a plain
/// identifier, at least one later argument is a simple or parenthesized lambda, and the
/// receiver's type is (or derives from) <c>System.Collections.Concurrent.ConcurrentDictionary`2</c>.
/// Each lambda whose body references the outer key identifier — binding to the same local or
/// parameter symbol as the first argument — is reported, because the capture allocates a
/// closure on every call where the lambda's own key parameter would let the delegate be
/// cached. The rule is resolved once per compilation by probing for the dictionary type, so it
/// costs nothing when the type is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1006ConcurrentDictionaryClosureCaptureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The GetOrAdd factory method name that gates the syntax fast path.</summary>
    internal const string GetOrAddMethodName = "GetOrAdd";

    /// <summary>The AddOrUpdate factory method name that gates the syntax fast path.</summary>
    internal const string AddOrUpdateMethodName = "AddOrUpdate";

    /// <summary>The metadata name of the concurrent dictionary type that gates the rule.</summary>
    private const string ConcurrentDictionaryMetadataName = "System.Collections.Concurrent.ConcurrentDictionary`2";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.ConcurrentDictionaryClosureCapture);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ConcurrentDictionaryMetadataName) is not { } dictionaryType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, dictionaryType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Extracts the syntax-only factory-call shape for a candidate invocation.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="memberAccess">The GetOrAdd/AddOrUpdate member access when the shape matches.</param>
    /// <param name="keyIdentifier">The first argument's key identifier when the shape matches.</param>
    /// <returns><see langword="true"/> for a factory call with an identifier key and at least one lambda argument.</returns>
    internal static bool TryGetFactoryCallShape(
        InvocationExpressionSyntax invocation,
        out MemberAccessExpressionSyntax? memberAccess,
        out IdentifierNameSyntax? keyIdentifier)
    {
        memberAccess = null;
        keyIdentifier = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !IsFactoryMethodName(access.Name.Identifier.ValueText))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2
            || arguments[0] is not { NameColon: null } firstArgument
            || !firstArgument.RefKindKeyword.IsKind(SyntaxKind.None)
            || firstArgument.Expression is not IdentifierNameSyntax identifier
            || !HasLambdaArgument(arguments))
        {
            return false;
        }

        memberAccess = access;
        keyIdentifier = identifier;
        return true;
    }

    /// <summary>Returns whether an identifier inside a lambda body is a reference to the captured key.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="identifier">The identifier to classify.</param>
    /// <param name="keyName">The captured key variable's name.</param>
    /// <param name="keySymbol">The captured key variable's symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the identifier binds to the same local or parameter as the key argument.</returns>
    internal static bool IsCapturedKeyReference(SemanticModel model, IdentifierNameSyntax identifier, string keyName, ISymbol keySymbol, CancellationToken cancellationToken)
        => identifier.Identifier.ValueText == keyName
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, keySymbol);

    /// <summary>Reports PSH1006 for each factory lambda that captures the outer key variable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dictionaryType">The resolved <c>ConcurrentDictionary`2</c> type for this compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionaryType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetFactoryCallShape(invocation, out var memberAccess, out var keyIdentifier)
            || !IsConcurrentDictionaryReceiver(context.SemanticModel, memberAccess!.Expression, dictionaryType, context.CancellationToken)
            || context.SemanticModel.GetSymbolInfo(keyIdentifier!, context.CancellationToken).Symbol is not { } keySymbol
            || keySymbol is not (ILocalSymbol or IParameterSymbol))
        {
            return;
        }

        var keyName = keyIdentifier!.Identifier.ValueText;
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 1; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is not LambdaExpressionSyntax lambda
                || !HasOwnKeyParameter(lambda)
                || !LambdaCapturesKey(context.SemanticModel, lambda, keyName, keySymbol, context.CancellationToken))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                AllocationRules.ConcurrentDictionaryClosureCapture,
                lambda.SyntaxTree,
                lambda.Span,
                keyName));
        }
    }

    /// <summary>Returns whether a factory method name gates further analysis.</summary>
    /// <param name="name">The member access name to test.</param>
    /// <returns><see langword="true"/> for GetOrAdd and AddOrUpdate.</returns>
    private static bool IsFactoryMethodName(string name)
        => name is GetOrAddMethodName or AddOrUpdateMethodName;

    /// <summary>Returns whether any argument after the key is a simple or parenthesized lambda.</summary>
    /// <param name="arguments">The invocation's arguments.</param>
    /// <returns><see langword="true"/> when at least one later argument is a lambda.</returns>
    private static bool HasLambdaArgument(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var i = 1; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is LambdaExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a lambda declares its own (first) key parameter.</summary>
    /// <param name="lambda">The lambda to inspect.</param>
    /// <returns><see langword="true"/> when the lambda has at least one parameter.</returns>
    private static bool HasOwnKeyParameter(LambdaExpressionSyntax lambda)
        => lambda switch
        {
            SimpleLambdaExpressionSyntax => true,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count > 0,
            _ => false
        };

    /// <summary>Returns whether the receiver's type is (or derives from) <c>ConcurrentDictionary`2</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="receiver">The member access receiver expression.</param>
    /// <param name="dictionaryType">The resolved <c>ConcurrentDictionary`2</c> type for this compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the receiver is a concurrent dictionary.</returns>
    private static bool IsConcurrentDictionaryReceiver(SemanticModel model, ExpressionSyntax receiver, INamedTypeSymbol dictionaryType, CancellationToken cancellationToken)
    {
        for (var current = model.GetTypeInfo(receiver, cancellationToken).Type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, dictionaryType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a lambda's body references the captured key variable.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="lambda">The lambda to scan.</param>
    /// <param name="keyName">The captured key variable's name.</param>
    /// <param name="keySymbol">The captured key variable's symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when at least one body identifier binds to the key.</returns>
    private static bool LambdaCapturesKey(SemanticModel model, LambdaExpressionSyntax lambda, string keyName, ISymbol keySymbol, CancellationToken cancellationToken)
    {
        var state = new KeyReferenceScanState(model, keyName, keySymbol, cancellationToken);
        if (lambda.Body is IdentifierNameSyntax bodyIdentifier
            && IsCapturedKeyReference(model, bodyIdentifier, keyName, keySymbol, cancellationToken))
        {
            return true;
        }

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, KeyReferenceScanState>(lambda.Body, ref state, VisitKeyReference);
        return state.Found;
    }

    /// <summary>Records one captured-key reference encountered during the lambda body scan.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a capture is found.</returns>
    private static bool VisitKeyReference(IdentifierNameSyntax identifier, ref KeyReferenceScanState state)
    {
        if (!IsCapturedKeyReference(state.Model, identifier, state.KeyName, state.KeySymbol, state.CancellationToken))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Captures the state required while scanning a lambda body for captured-key references.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="KeyName">The captured key variable's name.</param>
    /// <param name="KeySymbol">The captured key variable's symbol.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct KeyReferenceScanState(
        SemanticModel Model,
        string KeyName,
        ISymbol KeySymbol,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether a captured-key reference was found.</summary>
        public bool Found { get; set; }
    }
}
