// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports qualified names and instance-member accesses that do not match the configured
/// shortest-name and <c>this.</c>-qualification style.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NameSimplificationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.SimplifyName,
        ReadabilityRules.SimplifyMemberAccess);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(AnalyzeQualifiedName, SyntaxKind.QualifiedName);
        context.RegisterSyntaxNodeAction(AnalyzeAliasQualifiedName, SyntaxKind.AliasQualifiedName);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSemanticModelAction(AnalyzeBareMemberAccesses);
    }

    /// <summary>Reports qualified type or namespace names that can be shortened.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeQualifiedName(SyntaxNodeAnalysisContext context)
    {
        var qualifiedName = (QualifiedNameSyntax)context.Node;
        if ((qualifiedName.Parent is QualifiedNameSyntax parent && parent.Left == qualifiedName)
            || IsExcludedNameContext(qualifiedName))
        {
            return;
        }

        var replacement = CloneSimpleName(qualifiedName.Right);
        if (qualifiedName.Right.Span.Length >= qualifiedName.Span.Length
            || !BindsToSameTypeOrNamespace(context.SemanticModel, qualifiedName, replacement, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.SimplifyName, qualifiedName.GetLocation()));
    }

    /// <summary>Reports <c>global::T</c> names that can be shortened.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeAliasQualifiedName(SyntaxNodeAnalysisContext context)
    {
        var aliasQualifiedName = (AliasQualifiedNameSyntax)context.Node;
        if (aliasQualifiedName.Alias.Identifier.ValueText != "global"
            || aliasQualifiedName.Parent is QualifiedNameSyntax
            || IsExcludedNameContext(aliasQualifiedName))
        {
            return;
        }

        var replacement = CloneSimpleName(aliasQualifiedName.Name);
        if (aliasQualifiedName.Name.Span.Length >= aliasQualifiedName.Span.Length
            || !BindsToSameTypeOrNamespace(context.SemanticModel, aliasQualifiedName, replacement, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.SimplifyName, aliasQualifiedName.GetLocation()));
    }

    /// <summary>Reports redundant <c>this.</c> member accesses.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (memberAccess.Expression is not ThisExpressionSyntax)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        if (InstanceMemberQualificationOptions.Read(options) == InstanceMemberQualification.RequireThis)
        {
            return;
        }

        var replacement = CloneSimpleName(memberAccess.Name);
        if (!BindsToSameExpression(context.SemanticModel, memberAccess, replacement, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.SimplifyMemberAccess, memberAccess.GetLocation()));
    }

    /// <summary>Reports bare instance-member accesses when the configured style requires <c>this.</c>.</summary>
    /// <param name="context">The semantic model context.</param>
    private static void AnalyzeBareMemberAccesses(SemanticModelAnalysisContext context)
    {
        var tree = context.SemanticModel.SyntaxTree;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        if (InstanceMemberQualificationOptions.Read(options) != InstanceMemberQualification.RequireThis)
        {
            return;
        }

        var root = tree.GetRoot(context.CancellationToken);
        foreach (var node in root.DescendantNodes())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (node is IdentifierNameSyntax identifier)
            {
                AnalyzeBareMemberAccess(context, identifier);
            }
        }
    }

    /// <summary>Reports one bare instance-member access that should be qualified.</summary>
    /// <param name="context">The semantic model context.</param>
    /// <param name="identifier">The identifier candidate.</param>
    private static void AnalyzeBareMemberAccess(SemanticModelAnalysisContext context, IdentifierNameSyntax identifier)
    {
        if (!IsBareReference(identifier))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        if (!IsInstanceMemberReference(symbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.SimplifyMemberAccess, identifier.GetLocation()));
    }

    /// <summary>Returns whether the candidate name appears in a context that must remain qualified.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name should not be simplified.</returns>
    private static bool IsExcludedNameContext(NameSyntax name)
    {
        for (var parent = name.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is UsingDirectiveSyntax)
            {
                return true;
            }

            if ((parent is NamespaceDeclarationSyntax namespaceDeclaration && namespaceDeclaration.Name.Span.Contains(name.Span))
                || (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace && fileScopedNamespace.Name.Span.Contains(name.Span)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the identifier is an unqualified expression that can take a <c>this.</c> prefix.</summary>
    /// <param name="identifier">The identifier name.</param>
    /// <returns><see langword="true"/> when the identifier is a bare member reference in value position.</returns>
    private static bool IsBareReference(IdentifierNameSyntax identifier)
    {
        switch (identifier.Parent)
        {
            case MemberAccessExpressionSyntax access when access.Name == identifier:
            case MemberBindingExpressionSyntax:
            case QualifiedNameSyntax:
            case AliasQualifiedNameSyntax:
            case AssignmentExpressionSyntax assignment when assignment.Left == identifier && assignment.Parent is InitializerExpressionSyntax:
            case NameColonSyntax:
            case NameEqualsSyntax:
                return false;
            default:
                return !IsInNameof(identifier);
        }
    }

    /// <summary>Returns whether the identifier sits inside a <c>nameof(...)</c> expression.</summary>
    /// <param name="node">The identifier node.</param>
    /// <returns><see langword="true"/> when an enclosing <c>nameof</c> is found before the statement boundary.</returns>
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

    /// <summary>Returns whether the symbol is a non-static field, property, ordinary method, or event.</summary>
    /// <param name="symbol">The bound symbol.</param>
    /// <returns><see langword="true"/> when a <c>this.</c> prefix can represent the symbol.</returns>
    private static bool IsInstanceMemberReference(ISymbol? symbol) =>
        symbol is { IsStatic: false, ContainingType: not null }
            && symbol switch
            {
                IMethodSymbol method => method.MethodKind is MethodKind.Ordinary,
                IFieldSymbol => true,
                IPropertySymbol => true,
                IEventSymbol => true,
                _ => false
            };

    /// <summary>Returns whether a shortened name binds to the same type or namespace symbol.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="original">The original name.</param>
    /// <param name="replacement">The replacement name.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both names bind to the same symbol.</returns>
    private static bool BindsToSameTypeOrNamespace(
        SemanticModel model,
        NameSyntax original,
        NameSyntax replacement,
        CancellationToken cancellationToken)
    {
        var originalSymbol = GetSingleSymbol(model.GetSymbolInfo(original, cancellationToken));
        if (originalSymbol is null)
        {
            return false;
        }

        if (TryLookupSameTypeOrNamespace(model, original.SpanStart, replacement, originalSymbol, out var binds))
        {
            return binds;
        }

        var replacementSymbol = GetSingleSymbol(model.GetSpeculativeSymbolInfo(
            original.SpanStart,
            replacement,
            SpeculativeBindingOption.BindAsTypeOrNamespace));

        return SymbolEqualityComparer.Default.Equals(originalSymbol, replacementSymbol);
    }

    /// <summary>Creates a detached simple name for speculative binding.</summary>
    /// <param name="name">The source simple name.</param>
    /// <returns>A detached simple name with the same identifier and type arguments.</returns>
    private static SimpleNameSyntax CloneSimpleName(SimpleNameSyntax name)
        => name switch
        {
            GenericNameSyntax genericName => SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(genericName.Identifier.ValueText),
                genericName.TypeArgumentList),
            _ => SyntaxFactory.IdentifierName(name.Identifier.ValueText)
        };

    /// <summary>Returns whether an unqualified expression binds to the same symbol.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="original">The original expression.</param>
    /// <param name="replacement">The replacement expression.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both expressions bind to the same symbol.</returns>
    private static bool BindsToSameExpression(
        SemanticModel model,
        ExpressionSyntax original,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var originalSymbol = GetSingleSymbol(model.GetSymbolInfo(original, cancellationToken));
        if (originalSymbol is null)
        {
            return false;
        }

        if (original is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess
            && replacement is SimpleNameSyntax simpleName)
        {
            return !HasLocalNameInScope(memberAccess, simpleName.Identifier.ValueText);
        }

        if (replacement is SimpleNameSyntax lookupName
            && TryLookupSameExpression(model, original.SpanStart, lookupName.Identifier.ValueText, originalSymbol, out var binds))
        {
            return binds;
        }

        var replacementSymbol = GetSingleSymbol(model.GetSpeculativeSymbolInfo(
            original.SpanStart,
            replacement,
            SpeculativeBindingOption.BindAsExpression));

        return SymbolEqualityComparer.Default.Equals(originalSymbol, replacementSymbol);
    }

    /// <summary>Returns whether a local declaration can hide an unqualified member name at the candidate position.</summary>
    /// <param name="memberAccess">The original <c>this.</c> member access.</param>
    /// <param name="name">The member name that would be used unqualified.</param>
    /// <returns><see langword="true"/> when a nearer declaration may change binding.</returns>
    private static bool HasLocalNameInScope(MemberAccessExpressionSyntax memberAccess, string name)
    {
        var position = memberAccess.SpanStart;
        for (SyntaxNode? current = memberAccess; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case BlockSyntax block when BlockHasLocalNameBefore(block, name, position):
                case BaseMethodDeclarationSyntax methodDeclaration when ParameterListHasName(methodDeclaration.ParameterList, name):
                case LocalFunctionStatementSyntax localFunction when LocalFunctionShadowsName(localFunction, name, position):
                case ParenthesizedLambdaExpressionSyntax lambda when ParameterListHasName(lambda.ParameterList, name):
                case SimpleLambdaExpressionSyntax simpleLambda when simpleLambda.Parameter.Identifier.ValueText == name:
                case AnonymousMethodExpressionSyntax anonymousMethod when anonymousMethod.ParameterList is not null
                    && ParameterListHasName(anonymousMethod.ParameterList, name):
                case ForEachStatementSyntax forEachStatement when forEachStatement.Identifier.ValueText == name
                    && forEachStatement.SpanStart < position:
                case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == name
                    && catchDeclaration.SpanStart < position:
                    return true;
                case TypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a block has a local name declaration before the candidate position.</summary>
    /// <param name="block">The block to scan.</param>
    /// <param name="name">The name to find.</param>
    /// <param name="position">The candidate position.</param>
    /// <returns><see langword="true"/> when a previous statement declares the name.</returns>
    private static bool BlockHasLocalNameBefore(BlockSyntax block, string name, int position)
    {
        foreach (var statement in block.Statements)
        {
            if (statement.SpanStart >= position)
            {
                return false;
            }

            if (StatementDeclaresLocalName(statement, name, position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a previous statement declares a local name.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <param name="position">The candidate position.</param>
    /// <returns><see langword="true"/> when the statement declares the name before the candidate.</returns>
    private static bool StatementDeclaresLocalName(StatementSyntax statement, string name, int position)
        => statement switch
        {
            LocalDeclarationStatementSyntax localDeclaration => VariableDeclarationHasName(localDeclaration.Declaration, name),
            LocalFunctionStatementSyntax localFunction => LocalFunctionShadowsName(localFunction, name, position),
            ForEachStatementSyntax forEachStatement => forEachStatement.Identifier.ValueText == name,
            ForEachVariableStatementSyntax forEachVariableStatement => PatternDeclaresName(forEachVariableStatement.Variable, name),
            UsingStatementSyntax { Declaration: { } declaration } => VariableDeclarationHasName(declaration, name),
            FixedStatementSyntax fixedStatement => VariableDeclarationHasName(fixedStatement.Declaration, name),
            _ => false
        };

    /// <summary>Returns whether a local function declares the name before the candidate position.</summary>
    /// <param name="localFunction">The local function to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <param name="position">The candidate position.</param>
    /// <returns><see langword="true"/> when the local function shadows the member name.</returns>
    private static bool LocalFunctionShadowsName(LocalFunctionStatementSyntax localFunction, string name, int position)
        => (localFunction.Identifier.ValueText == name && localFunction.SpanStart < position)
        || ParameterListHasName(localFunction.ParameterList, name);

    /// <summary>Returns whether a parameter list contains a name.</summary>
    /// <param name="parameterList">The parameter list to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when a parameter declares the name.</returns>
    private static bool ParameterListHasName(BaseParameterListSyntax parameterList, string name)
    {
        foreach (var parameter in parameterList.Parameters)
        {
            if (parameter.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a variable declaration contains a name.</summary>
    /// <param name="declaration">The variable declaration to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when the declaration contains the name.</returns>
    private static bool VariableDeclarationHasName(VariableDeclarationSyntax declaration, string name)
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

    /// <summary>Returns whether a declaration expression introduces the requested name.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when the expression declares the name.</returns>
    private static bool PatternDeclaresName(ExpressionSyntax expression, string name)
        => expression switch
        {
            DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation } => designation.Identifier.ValueText == name,
            DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax designation } => DesignationDeclaresName(designation, name),
            _ => false
        };

    /// <summary>Returns whether a variable designation contains the requested name.</summary>
    /// <param name="designation">The designation to inspect.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when the designation declares the name.</returns>
    private static bool DesignationDeclaresName(ParenthesizedVariableDesignationSyntax designation, string name)
    {
        foreach (var variable in designation.Variables)
        {
            if ((variable is SingleVariableDesignationSyntax single && single.Identifier.ValueText == name)
                || (variable is ParenthesizedVariableDesignationSyntax nested && DesignationDeclaresName(nested, name)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Uses symbol lookup for the common non-generic type or namespace simplification path.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="replacement">The proposed shorter name.</param>
    /// <param name="originalSymbol">The symbol bound by the original spelling.</param>
    /// <param name="binds">Whether lookup proved the replacement keeps the same symbol.</param>
    /// <returns><see langword="true"/> when lookup produced a decisive answer.</returns>
    private static bool TryLookupSameTypeOrNamespace(
        SemanticModel model,
        int position,
        NameSyntax replacement,
        ISymbol originalSymbol,
        out bool binds)
    {
        binds = false;
        if (replacement is not IdentifierNameSyntax identifierName)
        {
            return false;
        }

        var candidates = model.LookupNamespacesAndTypes(position, name: identifierName.Identifier.ValueText);
        if (candidates.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(candidates[i], originalSymbol))
            {
                binds = true;
                return true;
            }
        }

        return true;
    }

    /// <summary>Uses symbol lookup for unqualified member-access candidates before speculative binding.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The proposed unqualified member name.</param>
    /// <param name="originalSymbol">The symbol bound by the qualified expression.</param>
    /// <param name="binds">Whether lookup proved the replacement keeps the same symbol.</param>
    /// <returns><see langword="true"/> when lookup produced a decisive answer.</returns>
    private static bool TryLookupSameExpression(
        SemanticModel model,
        int position,
        string name,
        ISymbol originalSymbol,
        out bool binds)
    {
        binds = false;
        var candidates = model.LookupSymbols(position, name: name);
        if (candidates.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(candidates[i], originalSymbol))
            {
                binds = true;
                return true;
            }
        }

        return true;
    }

    /// <summary>Gets the resolved symbol when the semantic info is unambiguous.</summary>
    /// <param name="symbolInfo">The symbol info.</param>
    /// <returns>The resolved symbol, or <see langword="null"/>.</returns>
    private static ISymbol? GetSingleSymbol(SymbolInfo symbolInfo)
        => symbolInfo.Symbol ?? (symbolInfo.CandidateSymbols.Length == 1 ? symbolInfo.CandidateSymbols[0] : null);
}
