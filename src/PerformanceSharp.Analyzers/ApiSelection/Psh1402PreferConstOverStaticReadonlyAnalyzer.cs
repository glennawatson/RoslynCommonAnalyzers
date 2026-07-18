// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports compile-time-constant values that are not declared <c>const</c> (PSH1402):
/// non-public <c>static readonly</c> fields whose initializer is a compile-time constant
/// of a const-capable type, and locals with such an initializer that are never written
/// again. A <c>const</c> folds the value into use sites instead of paying a load on every
/// use. Public and protected fields are skipped because const values bake into consuming
/// assemblies; locals written after initialization — reassigned, incremented, passed by
/// <c>ref</c>/<c>out</c>/<c>in</c>, aliased, or mutated through a capture — are skipped
/// because they cannot be const.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1402PreferConstOverStaticReadonlyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key holding the explicit type that replaces <c>var</c> in the fix.</summary>
    internal const string ExplicitTypeKey = "ExplicitType";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.PreferConstOverStaticReadonly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Reports PSH1402 for a static readonly field whose initializer is a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!HasConstConvertibleModifiers(field.Modifiers)
            || field.Declaration.Variables is not [var variable]
            || variable.Initializer is not { } initializer)
        {
            return;
        }

        var fieldType = context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type;
        if (fieldType is null
            || !AdmitsConst(fieldType)
            || !context.SemanticModel.GetConstantValue(initializer.Value, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferConstOverStaticReadonly,
            variable.SyntaxTree,
            variable.Identifier.Span,
            variable.Identifier.ValueText));
    }

    /// <summary>Reports PSH1402 for a local whose initializer is a compile-time constant and that is never written again.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (local.IsConst
            || !local.UsingKeyword.IsKind(SyntaxKind.None)
            || local.Declaration.Type is RefTypeSyntax
            || local.Declaration.Variables is not [var variable]
            || variable.Initializer is not { } initializer
            || IsNeverConstant(initializer.Value))
        {
            return;
        }

        var localType = context.SemanticModel.GetTypeInfo(local.Declaration.Type, context.CancellationToken).Type;
        if (localType is null || !AllowsConstLocal(context, localType, initializer, local, variable))
        {
            return;
        }

        ReportLocal(context, local, variable, localType);
    }

    /// <summary>Returns whether the local's type, initializer, and later uses all allow <c>const</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="localType">The local's declared or inferred type.</param>
    /// <param name="initializer">The local's initializer.</param>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="variable">The single declarator.</param>
    /// <returns><see langword="true"/> when the type admits const, the value folds, and nothing writes the local.</returns>
    private static bool AllowsConstLocal(
        in SyntaxNodeAnalysisContext context,
        ITypeSymbol localType,
        EqualsValueClauseSyntax initializer,
        LocalDeclarationStatementSyntax local,
        VariableDeclaratorSyntax variable)
        => AdmitsConst(localType)
            && context.SemanticModel.GetConstantValue(initializer.Value, context.CancellationToken).HasValue
            && GetEnclosingScope(local) is { } scope
            && !IsWrittenInScope(scope, variable.Identifier.ValueText);

    /// <summary>Reports the local, carrying the explicit type spelling when the declaration used <c>var</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="variable">The single declarator.</param>
    /// <param name="localType">The local's declared or inferred type.</param>
    private static void ReportLocal(
        in SyntaxNodeAnalysisContext context,
        LocalDeclarationStatementSyntax local,
        VariableDeclaratorSyntax variable,
        ITypeSymbol localType)
    {
        var typeSyntax = local.Declaration.Type;
        if (typeSyntax.IsVar)
        {
            var properties = ImmutableDictionary<string, string?>.Empty.Add(
                ExplicitTypeKey,
                localType.ToMinimalDisplayString(context.SemanticModel, typeSyntax.SpanStart));
            context.ReportDiagnostic(DiagnosticHelper.Create(
                ApiSelectionRules.PreferConstOverStaticReadonly,
                variable.SyntaxTree,
                variable.Identifier.Span,
                properties,
                variable.Identifier.ValueText));
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferConstOverStaticReadonly,
            variable.SyntaxTree,
            variable.Identifier.Span,
            variable.Identifier.ValueText));
    }

    /// <summary>Returns whether an initializer's shape can never be a compile-time constant.</summary>
    /// <param name="value">The initializer expression.</param>
    /// <returns><see langword="true"/> for creations, element accesses, awaits, and non-<c>nameof</c> invocations.</returns>
    /// <remarks>
    /// A syntax-only prepass that skips semantic binding for the initializer shapes that dominate
    /// real code and can never fold. It only rejects; every other shape is still verified with
    /// <see cref="SemanticModel.GetConstantValue(SyntaxNode, CancellationToken)"/>.
    /// </remarks>
    private static bool IsNeverConstant(ExpressionSyntax value)
        => value switch
        {
            BaseObjectCreationExpressionSyntax => true,
            ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax => true,
            CollectionExpressionSyntax or ElementAccessExpressionSyntax or AwaitExpressionSyntax => true,
            InvocationExpressionSyntax invocation => invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "nameof" },
            _ => false,
        };

    /// <summary>Gets the syntax that bounds where a local can be referenced.</summary>
    /// <param name="node">The local declaration statement.</param>
    /// <returns>The enclosing block, or the compilation unit for top-level statements.</returns>
    /// <remarks>
    /// A local declared straight into a switch section is in scope across the whole switch, so the
    /// walk climbs past the section to the enclosing block. A scope that is too wide can only find
    /// more writes, which can only silence a report; a scope that is too narrow would invent one.
    /// </remarks>
    private static SyntaxNode? GetEnclosingScope(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BlockSyntax or CompilationUnitSyntax)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Returns whether anything in the scope writes the named local after initialization.</summary>
    /// <param name="scope">The syntax that bounds the local.</param>
    /// <param name="name">The local's name.</param>
    /// <returns><see langword="true"/> as soon as one write is found.</returns>
    /// <remarks>
    /// Writes are found by name, not by symbol: a plain identifier inside the local's scope can only
    /// mean that local, because C# does not let a nested declaration shadow it, and every write to a
    /// local must spell its name. That keeps the whole check off the semantic model, and the scan
    /// covers lambda and local-function bodies, so a captured-and-mutated local is seen exactly
    /// where it is written.
    /// </remarks>
    private static bool IsWrittenInScope(SyntaxNode scope, string name)
    {
        var state = new WriteScanState(name);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, WriteScanState>(
            scope,
            ref state,
            static (IdentifierNameSyntax identifier, ref WriteScanState scan) => scan.Observe(identifier));
        return state.Written;
    }

    /// <summary>Returns whether a modifier list is <c>static readonly</c> without public exposure.</summary>
    /// <param name="modifiers">The modifier list to inspect.</param>
    /// <returns><see langword="true"/> when both <c>static</c> and <c>readonly</c> are present and neither <c>public</c> nor <c>protected</c> is.</returns>
    private static bool HasConstConvertibleModifiers(SyntaxTokenList modifiers)
    {
        var hasStatic = false;
        var hasReadonly = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            switch (modifiers[i].Kind())
            {
                case SyntaxKind.StaticKeyword:
                {
                    hasStatic = true;
                    break;
                }

                case SyntaxKind.ReadOnlyKeyword:
                {
                    hasReadonly = true;
                    break;
                }

                case SyntaxKind.PublicKeyword:
                case SyntaxKind.ProtectedKeyword:
                    return false;
            }
        }

        return hasStatic && hasReadonly;
    }

    /// <summary>Returns whether a type can be declared <c>const</c>.</summary>
    /// <param name="type">The field or local type to inspect.</param>
    /// <returns><see langword="true"/> for enum types and the const-capable special types.</returns>
    /// <remarks>
    /// <see cref="SpecialType.System_Boolean"/> through <see cref="SpecialType.System_String"/> is a
    /// contiguous run covering exactly the const-capable primitives: bool, char, the integral types,
    /// decimal, float, double, and string.
    /// </remarks>
    private static bool AdmitsConst(ITypeSymbol type)
        => type.TypeKind == TypeKind.Enum
            || type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String;

    /// <summary>Tracks whether one named local has been written, and stops the walk as soon as it has.</summary>
    private struct WriteScanState : IEquatable<WriteScanState>
    {
        /// <summary>The local's name.</summary>
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="WriteScanState"/> struct.</summary>
        /// <param name="name">The local's name.</param>
        public WriteScanState(string name)
        {
            _name = name;
            Written = false;
        }

        /// <summary>Gets a value indicating whether a write has been seen.</summary>
        public bool Written { get; private set; }

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(WriteScanState other) => Written == other.Written && _name == other._name;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is WriteScanState other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked((_name.GetHashCode() * 397) ^ (Written ? 1 : 0));

        /// <summary>Observes one identifier and returns whether scanning should continue.</summary>
        /// <param name="identifier">The identifier.</param>
        /// <returns><see langword="false"/> once a write has been seen.</returns>
        public bool Observe(IdentifierNameSyntax identifier)
        {
            if (identifier.Identifier.ValueText == _name && IsWriteReference(identifier))
            {
                Written = true;
            }

            return !Written;
        }

        /// <summary>Returns whether an identifier writes, aliases, or takes the address of the local it names.</summary>
        /// <param name="identifier">The identifier to classify.</param>
        /// <returns><see langword="true"/> for any use a <c>const</c> local could not satisfy.</returns>
        /// <remarks>
        /// The name of a member access (<c>this.x = 1</c>) is a member write, never a local write, and
        /// its assignment target is the member access rather than the identifier, so it falls through
        /// to the read default on its own.
        /// </remarks>
        private static bool IsWriteReference(IdentifierNameSyntax identifier)
            => identifier.Parent switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left == identifier,
                PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
                PrefixUnaryExpressionSyntax prefix => IsMutatingPrefix(prefix),
                ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) || IsDeconstructionTarget(argument),
                RefExpressionSyntax or MakeRefExpressionSyntax => true,
                _ => false,
            };

        /// <summary>Returns whether a prefix operator writes or takes the address of its operand.</summary>
        /// <param name="prefix">The prefix expression to classify.</param>
        /// <returns><see langword="true"/> for pre-increment, pre-decrement, and address-of.</returns>
        private static bool IsMutatingPrefix(PrefixUnaryExpressionSyntax prefix)
            => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression)
                || prefix.IsKind(SyntaxKind.AddressOfExpression);

        /// <summary>Returns whether a tuple argument sits on the left of a deconstruction assignment.</summary>
        /// <param name="argument">The tuple argument holding the identifier.</param>
        /// <returns><see langword="true"/> when the enclosing tuple — however deeply nested — is a deconstruction target.</returns>
        private static bool IsDeconstructionTarget(ArgumentSyntax argument)
        {
            SyntaxNode node = argument;
            while (node.Parent is TupleExpressionSyntax tuple)
            {
                if (tuple.Parent is AssignmentExpressionSyntax assignment && assignment.Left == tuple)
                {
                    return true;
                }

                if (tuple.Parent is not ArgumentSyntax outer)
                {
                    return false;
                }

                node = outer;
            }

            return false;
        }
    }
}
