// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a throw inside a member whose callers have no way to defend against it (SST1485). Extra member
/// names are added with <c>stylesharp.SST1485.additional_members</c>.
/// </summary>
/// <remarks>
/// <para>
/// The members are the ones nobody writes a <c>try</c> around, because nobody writes the call: a hash-based
/// collection calls <c>GetHashCode</c> and <c>Equals</c>, a debugger and an interpolated string call
/// <c>ToString</c>, a <c>using</c> block calls <c>Dispose</c> while an exception is already unwinding, the
/// runtime calls a static constructor and a finalizer, and an implicit conversion runs at a call site that
/// does not even mention it. An exception from any of them surfaces somewhere the cause is no longer
/// visible.
/// </para>
/// <para>
/// Ordered so a member is rejected on its name. Almost every declaration in a compilation is not one of
/// these members, and answering that costs a string comparison and — only when a name matched — a walk of
/// the body. The thrown type is bound last, and a <c>throw new NotImplementedException()</c> is recognized
/// on its syntax before even that, so the exemption usually costs nothing either.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1485UnexpectedThrowAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the equality method the runtime and the collections call.</summary>
    private const string EqualsName = "Equals";

    /// <summary>The name of the hash method every hash-based collection calls.</summary>
    private const string GetHashCodeName = "GetHashCode";

    /// <summary>The name of the method a debugger, a logger and string interpolation all call.</summary>
    private const string ToStringName = "ToString";

    /// <summary>The name of the method a <c>using</c> block calls while an exception may be unwinding.</summary>
    private const string DisposeName = "Dispose";

    /// <summary>The name of the asynchronous disposal method.</summary>
    private const string DisposeAsyncName = "DisposeAsync";

    /// <summary>The number of parameters an implicitly invoked no-argument member declares.</summary>
    private const int NoParameters = 0;

    /// <summary>The number of parameters an equality method declares.</summary>
    private const int OneParameter = 1;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UnexpectedThrow);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Sets up the per-compilation state, then analyzes every member that can carry a body.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// The allowed exception types stay unresolved until a throw actually reaches the exemption, so a
    /// compilation whose implicitly invoked members never throw — the common case — pays nothing for them.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        var allowed = new Lazy<AllowedThrowTypes>(
            () => AllowedThrowTypes.Create(compilation),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, UnexpectedThrowOptions>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, optionsByTree, allowed),
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration);
    }

    /// <summary>Walks the body of a member that must not throw and reports the throws it originates.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="allowed">The exception types that mark a member as deliberately absent.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnexpectedThrowOptions> optionsByTree,
        Lazy<AllowedThrowTypes> allowed)
    {
        var member = (BaseMethodDeclarationSyntax)context.Node;
        if (!MustNotThrow(member, context, optionsByTree))
        {
            return;
        }

        if (GetBody(member) is not { } body)
        {
            return;
        }

        ScanForThrows(body, context, member, allowed);
    }

    /// <summary>Gets the code a member runs, whichever body form it declares.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The body, or <see langword="null"/> when the member declares none.</returns>
    /// <remarks>
    /// The arrow clause is handed over whole rather than the expression under it, because the scan reports the
    /// nodes <em>inside</em> what it is given — an expression-bodied <c>=&gt; throw new InvalidOperationException()</c>
    /// is the throw itself, and unwrapping the clause would walk straight past it.
    /// </remarks>
    private static SyntaxNode? GetBody(BaseMethodDeclarationSyntax member)
        => (SyntaxNode?)member.Body ?? member.ExpressionBody;

    /// <summary>Returns whether a member's callers have no way to handle an exception it throws.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns><see langword="true"/> when the member is invoked implicitly or from code with no catch.</returns>
    /// <remarks>
    /// An explicit conversion is not measured: the caller wrote the cast, so the conversion — and its
    /// failure — is visible at the call site. An instance constructor is not measured either; only a static
    /// one, whose failure the runtime turns into a <c>TypeInitializationException</c> raised at whichever
    /// unrelated line happened to touch the type first.
    /// </remarks>
    private static bool MustNotThrow(
        BaseMethodDeclarationSyntax member,
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnexpectedThrowOptions> optionsByTree)
    {
        return member switch
        {
            MethodDeclarationSyntax method => IsImplicitlyInvoked(method)
                || GetOptions(context, optionsByTree).Contains(method.Identifier.ValueText),
            ConstructorDeclarationSyntax constructor => ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword),
            DestructorDeclarationSyntax => true,
            OperatorDeclarationSyntax @operator => IsComparisonOperator(@operator.OperatorToken),
            ConversionOperatorDeclarationSyntax conversion => conversion.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword),
            _ => false,
        };
    }

    /// <summary>Returns whether a method is one the runtime or the framework calls on the caller's behalf.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> for the equality, hashing, formatting and disposal members.</returns>
    /// <remarks>
    /// The arity is part of the identity: a <c>Dispose(bool disposing)</c> is the pattern's helper rather
    /// than the member a <c>using</c> block calls, and it is left to the <c>Dispose()</c> that calls it.
    /// </remarks>
    private static bool IsImplicitlyInvoked(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters.Count;
        return method.Identifier.ValueText switch
        {
            EqualsName => parameters == OneParameter,
            GetHashCodeName or ToStringName or DisposeName or DisposeAsyncName => parameters == NoParameters,
            _ => false,
        };
    }

    /// <summary>Returns whether an operator token is one of the equality or ordering operators.</summary>
    /// <param name="operatorToken">The declared operator.</param>
    /// <returns><see langword="true"/> when sorting or comparing a value can raise the exception.</returns>
    private static bool IsComparisonOperator(SyntaxToken operatorToken) => operatorToken.Kind() is SyntaxKind.EqualsEqualsToken
        or SyntaxKind.ExclamationEqualsToken
        or SyntaxKind.LessThanToken
        or SyntaxKind.GreaterThanToken
        or SyntaxKind.LessThanEqualsToken
        or SyntaxKind.GreaterThanEqualsToken;

    /// <summary>Reads the settings for the member's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static UnexpectedThrowOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnexpectedThrowOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = UnexpectedThrowOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Walks a member's body in preorder, reporting every throw the member itself originates.</summary>
    /// <param name="node">The node being scanned.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="member">The member that must not throw.</param>
    /// <param name="allowed">The exception types that mark a member as deliberately absent.</param>
    /// <remarks>
    /// An indexed child walk rather than <c>DescendantNodes()</c>, which also lets the walk step over a
    /// lambda or a local function: code inside one runs when its delegate is invoked, which need not be
    /// during the member at all.
    /// </remarks>
    private static void ScanForThrows(
        SyntaxNode node,
        SyntaxNodeAnalysisContext context,
        BaseMethodDeclarationSyntax member,
        Lazy<AllowedThrowTypes> allowed)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is not { } child || IsSeparateFunction(child))
            {
                continue;
            }

            ReportThrow(child, context, member, allowed);
            ScanForThrows(child, context, member, allowed);
        }
    }

    /// <summary>Returns whether a node is a function of its own rather than part of the member's body.</summary>
    /// <param name="node">The node being scanned.</param>
    /// <returns><see langword="true"/> for a lambda, an anonymous method, or a local function.</returns>
    private static bool IsSeparateFunction(SyntaxNode node)
        => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;

    /// <summary>Reports one throw when the member is not allowed to originate it.</summary>
    /// <param name="node">The node being scanned.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="member">The member that must not throw.</param>
    /// <param name="allowed">The exception types that mark a member as deliberately absent.</param>
    /// <remarks>
    /// A bare <c>throw;</c> is not reported: it propagates the exception that is already unwinding rather
    /// than originating one, so the member is passing on a failure it did not cause. Removing it would
    /// swallow that failure, which is a worse outcome than the one this rule is guarding against.
    /// </remarks>
    private static void ReportThrow(
        SyntaxNode node,
        SyntaxNodeAnalysisContext context,
        BaseMethodDeclarationSyntax member,
        Lazy<AllowedThrowTypes> allowed)
    {
        SyntaxToken keyword;
        ExpressionSyntax thrown;
        switch (node)
        {
            case ThrowStatementSyntax { Expression: { } thrownByStatement } statement:
            {
                keyword = statement.ThrowKeyword;
                thrown = thrownByStatement;
                break;
            }

            case ThrowExpressionSyntax throwExpression:
            {
                keyword = throwExpression.ThrowKeyword;
                thrown = throwExpression.Expression;
                break;
            }

            default:
                return;
        }

        if (IsDeliberateAbsence(thrown, context, allowed))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.UnexpectedThrow,
            keyword.GetLocation(),
            GetMemberName(member)));
    }

    /// <summary>Returns whether a throw states the member is deliberately absent rather than failing.</summary>
    /// <param name="thrown">The thrown expression.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="allowed">The exception types that mark a member as deliberately absent.</param>
    /// <returns><see langword="true"/> for a <c>NotImplementedException</c> or a <c>NotSupportedException</c>.</returns>
    /// <remarks>
    /// The written name is checked first, so the overwhelmingly common <c>throw new NotImplementedException()</c>
    /// never reaches the semantic model. Anything else — a factory call, a captured exception, an alias — is
    /// bound, which also recognizes a type that derives from one of the two.
    /// </remarks>
    private static bool IsDeliberateAbsence(
        ExpressionSyntax thrown,
        SyntaxNodeAnalysisContext context,
        Lazy<AllowedThrowTypes> allowed)
    {
        if (thrown is ObjectCreationExpressionSyntax creation
            && GetSimpleName(creation.Type) is AllowedThrowTypes.NotImplementedName or AllowedThrowTypes.NotSupportedName)
        {
            return true;
        }

        return allowed.Value.Contains(context.SemanticModel.GetTypeInfo(thrown, context.CancellationToken).Type);
    }

    /// <summary>Gets the name the diagnostic uses for the member.</summary>
    /// <param name="member">The member that must not throw.</param>
    /// <returns>The member's name, as a reader would say it.</returns>
    private static string GetMemberName(BaseMethodDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => "static " + constructor.Identifier.ValueText,
        DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
        OperatorDeclarationSyntax @operator => "operator " + @operator.OperatorToken.ValueText,
        ConversionOperatorDeclarationSyntax conversion => "implicit operator " + conversion.Type,
        _ => string.Empty,
    };

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased type name.</summary>
    /// <param name="type">The constructed type.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
