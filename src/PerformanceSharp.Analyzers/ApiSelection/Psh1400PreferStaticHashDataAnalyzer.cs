// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests the static <c>HashData</c> methods for one-shot hashing (PSH1400). Two shapes are
/// reported: a chained <c>SHA256.Create().ComputeHash(data)</c> invocation whose single argument
/// is a <c>byte[]</c>, and a using-scoped algorithm local whose every reference is the receiver
/// of such a <c>ComputeHash</c> call — so the instance exists only to hash. The rule is resolved
/// once per compilation by probing each algorithm type for a static <c>HashData(byte[])</c>
/// method, so it reports nothing on runtimes without the one-shot API (pre-.NET 5); no target
/// framework string is parsed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1400PreferStaticHashDataAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the instance hashing method replaced by HashData.</summary>
    private const string ComputeHashMethodName = "ComputeHash";

    /// <summary>The name of the static factory method on the algorithm types.</summary>
    private const string CreateMethodName = "Create";

    /// <summary>The name of the static one-shot hashing method probed on each algorithm type.</summary>
    private const string HashDataMethodName = "HashData";

    /// <summary>The metadata names of the algorithm types probed for a static HashData method.</summary>
    private static readonly string[] AlgorithmMetadataNames =
    [
        "System.Security.Cryptography.MD5",
        "System.Security.Cryptography.SHA1",
        "System.Security.Cryptography.SHA256",
        "System.Security.Cryptography.SHA384",
        "System.Security.Cryptography.SHA512",
        "System.Security.Cryptography.SHA3_256",
        "System.Security.Cryptography.SHA3_384",
        "System.Security.Cryptography.SHA3_512"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.PreferStaticHashData);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var algorithmTypes = GetHashDataAlgorithmTypes(start.Compilation);
            if (algorithmTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, algorithmTypes), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeLocalDeclaration(nodeContext, algorithmTypes), SyntaxKind.LocalDeclarationStatement);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeUsingStatement(nodeContext, algorithmTypes), SyntaxKind.UsingStatement);
        });
    }

    /// <summary>Returns whether an invocation has the chained <c>X.Create().ComputeHash(arg)</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="createInvocation">The inner <c>Create()</c> invocation when the shape matches.</param>
    /// <returns><see langword="true"/> when the syntax-only chained shape matches.</returns>
    internal static bool IsChainedComputeHashShape(InvocationExpressionSyntax invocation, out InvocationExpressionSyntax? createInvocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 1
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ComputeHashMethodName } computeAccess
            && computeAccess.Expression is InvocationExpressionSyntax create
            && IsParameterlessCreateShape(create))
        {
            createInvocation = create;
            return true;
        }

        createInvocation = null;
        return false;
    }

    /// <summary>Reports PSH1400 for a chained <c>Create().ComputeHash(byte[])</c> invocation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="algorithmTypes">The gated algorithm types exposing a static HashData method.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] algorithmTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsChainedComputeHashShape(invocation, out var createInvocation)
            || !IsSingleByteArrayComputeHash(context.SemanticModel, invocation, context.CancellationToken)
            || GetGatedCreateAlgorithm(context.SemanticModel, createInvocation!, algorithmTypes, context.CancellationToken) is not { } algorithmType)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferStaticHashData,
            invocation.SyntaxTree,
            invocation.Span,
            algorithmType.Name));
    }

    /// <summary>Reports PSH1400 for using-declaration locals used only as ComputeHash receivers.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="algorithmTypes">The gated algorithm types exposing a static HashData method.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] algorithmTypes)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) || declaration.Parent is not { } scope)
        {
            return;
        }

        AnalyzeUsingScopedVariables(context, declaration.Declaration, scope, algorithmTypes);
    }

    /// <summary>Reports PSH1400 for using-statement locals used only as ComputeHash receivers.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="algorithmTypes">The gated algorithm types exposing a static HashData method.</param>
    private static void AnalyzeUsingStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] algorithmTypes)
    {
        var usingStatement = (UsingStatementSyntax)context.Node;
        if (usingStatement.Declaration is not { } declaration)
        {
            return;
        }

        AnalyzeUsingScopedVariables(context, declaration, usingStatement, algorithmTypes);
    }

    /// <summary>Reports PSH1400 for each using-scoped variable that holds a hash-only algorithm instance.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The using-scoped variable declaration.</param>
    /// <param name="scope">The scope containing every possible reference to the variables.</param>
    /// <param name="algorithmTypes">The gated algorithm types exposing a static HashData method.</param>
    private static void AnalyzeUsingScopedVariables(
        SyntaxNodeAnalysisContext context,
        VariableDeclarationSyntax declaration,
        SyntaxNode scope,
        INamedTypeSymbol[] algorithmTypes)
    {
        for (var i = 0; i < declaration.Variables.Count; i++)
        {
            var variable = declaration.Variables[i];
            if (variable.Initializer is not { Value: InvocationExpressionSyntax createInvocation }
                || !IsParameterlessCreateShape(createInvocation)
                || GetGatedCreateAlgorithm(context.SemanticModel, createInvocation, algorithmTypes, context.CancellationToken) is not { } algorithmType
                || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local
                || !IsHashOnlyLocal(context.SemanticModel, scope, variable, local, context.CancellationToken))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ApiSelectionRules.PreferStaticHashData,
                variable.SyntaxTree,
                variable.Identifier.Span,
                algorithmType.Name));
        }
    }

    /// <summary>Resolves the algorithm types that expose a static HashData(byte[]) method.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The gated algorithm types, or <see langword="null"/> when none qualify.</returns>
    private static INamedTypeSymbol[]? GetHashDataAlgorithmTypes(Compilation compilation)
    {
        INamedTypeSymbol[]? types = null;
        var count = 0;
        for (var i = 0; i < AlgorithmMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(AlgorithmMetadataNames[i]) is not { } type
                || !HasStaticByteArrayHashData(type))
            {
                continue;
            }

            types ??= new INamedTypeSymbol[AlgorithmMetadataNames.Length];
            types[count] = type;
            count++;
        }

        if (types is null)
        {
            return null;
        }

        if (count == types.Length)
        {
            return types;
        }

        var exact = new INamedTypeSymbol[count];
        Array.Copy(types, exact, count);
        return exact;
    }

    /// <summary>Returns whether a type exposes a static HashData method taking a single byte array.</summary>
    /// <param name="type">The algorithm type to probe.</param>
    /// <returns><see langword="true"/> when a static HashData(byte[]) overload exists.</returns>
    private static bool HasStaticByteArrayHashData(INamedTypeSymbol type)
    {
        var members = type.GetMembers(HashDataMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method
                && IsByteArray(method.Parameters[0].Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is a single-dimensional byte array.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <c>byte[]</c>.</returns>
    private static bool IsByteArray(ITypeSymbol type)
        => type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte };

    /// <summary>Returns whether the bound ComputeHash overload takes a single byte array.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The ComputeHash invocation to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to a single-parameter byte[] overload.</returns>
    private static bool IsSingleByteArrayComputeHash(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Parameters.Length: 1 } method
            && IsByteArray(method.Parameters[0].Type);

    /// <summary>Returns the gated algorithm type when an invocation binds to its static parameterless Create method.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="createInvocation">The Create invocation to bind.</param>
    /// <param name="algorithmTypes">The gated algorithm types exposing a static HashData method.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The gated algorithm type, or <see langword="null"/> when the invocation is not a gated factory call.</returns>
    private static INamedTypeSymbol? GetGatedCreateAlgorithm(
        SemanticModel model,
        InvocationExpressionSyntax createInvocation,
        INamedTypeSymbol[] algorithmTypes,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(createInvocation, cancellationToken).Symbol is not IMethodSymbol { IsStatic: true, Parameters.Length: 0 } createMethod)
        {
            return null;
        }

        var containingType = createMethod.ContainingType;
        for (var i = 0; i < algorithmTypes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(algorithmTypes[i], containingType))
            {
                return containingType;
            }
        }

        return null;
    }

    /// <summary>Returns whether an invocation has the parameterless <c>X.Create()</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax-only factory shape matches.</returns>
    private static bool IsParameterlessCreateShape(InvocationExpressionSyntax invocation)
        => invocation is
        {
            ArgumentList.Arguments.Count: 0,
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: CreateMethodName }
        };

    /// <summary>Returns whether every reference to the local within the scope is a ComputeHash(byte[]) receiver (and at least one exists).</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="scope">The scope containing every possible reference to the local.</param>
    /// <param name="variable">The variable declarator that declares the local.</param>
    /// <param name="local">The using-scoped local symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local is used only to hash.</returns>
    private static bool IsHashOnlyLocal(
        SemanticModel model,
        SyntaxNode scope,
        VariableDeclaratorSyntax variable,
        ILocalSymbol local,
        CancellationToken cancellationToken)
    {
        var state = new HashOnlyLocalScanState(model, local, variable.Identifier.ValueText, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashOnlyLocalScanState>(scope, ref state, VisitLocalReference);
        return state.HasComputeHashUse && !state.HasOtherUse;
    }

    /// <summary>Records one local reference encountered during the scope scan.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a non-hash use is found.</returns>
    private static bool VisitLocalReference(IdentifierNameSyntax identifier, ref HashOnlyLocalScanState state)
    {
        if (identifier.Identifier.ValueText != state.LocalName
            || !SymbolEqualityComparer.Default.Equals(state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol, state.Local))
        {
            return true;
        }

        if (IsComputeHashReceiver(state.Model, identifier, state.CancellationToken))
        {
            state.HasComputeHashUse = true;
            return true;
        }

        state.HasOtherUse = true;
        return false;
    }

    /// <summary>Returns whether an identifier is the receiver of a single-argument ComputeHash(byte[]) invocation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="identifier">The local reference to classify.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the reference only feeds a one-shot hash call.</returns>
    private static bool IsComputeHashReceiver(SemanticModel model, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
        => identifier.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ComputeHashMethodName } access
            && access.Expression == identifier
            && access.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation
            && IsSingleByteArrayComputeHash(model, invocation, cancellationToken);

    /// <summary>Captures the state required while scanning references to one using-scoped local.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Local">The using-scoped local symbol.</param>
    /// <param name="LocalName">The local's simple name, used as a bind-free prefilter.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct HashOnlyLocalScanState(
        SemanticModel Model,
        ILocalSymbol Local,
        string LocalName,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether a qualifying ComputeHash receiver use was found.</summary>
        public bool HasComputeHashUse { get; set; }

        /// <summary>Gets or sets a value indicating whether any other use was found.</summary>
        public bool HasOtherUse { get; set; }
    }
}
