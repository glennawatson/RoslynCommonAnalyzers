// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Suggests declaring a dedicated lock object as <c>System.Threading.Lock</c>
/// instead of <see cref="object"/> (SST1900). A field qualifies only when it is a
/// private readonly <c>object</c> initialized with a parameterless <c>new()</c> and
/// every reference to it within the (non-partial) declaring type is the target of a
/// <c>lock</c> statement — so swapping the type cannot break another use. The rule is
/// resolved once per compilation by probing for <c>System.Threading.Lock</c>, so it
/// reports nothing on pre-.NET 9 runtimes; no target framework string is parsed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1900PreferLockTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the .NET 9 lock type.</summary>
    private const string LockMetadataName = "System.Threading.Lock";

    /// <summary>Classifies a matching field-name token for the syntax-only fast path.</summary>
    internal enum FieldNameTokenKind
    {
        /// <summary>The token does not affect the fast path.</summary>
        Ignore,

        /// <summary>The token is a direct lock-target field use.</summary>
        LockUse,

        /// <summary>The token represents a conflicting declaration or a non-lock use.</summary>
        Conflict
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.PreferLockType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(LockMetadataName) is null)
            {
                return;
            }

            var analyzedTypes = new ConcurrentDictionary<TypeDeclarationSyntax, byte>();
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeField(nodeContext, analyzedTypes), SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Returns the single syntax-only candidate field when the containing type has an unambiguous fast-path shape.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="variable">The matched variable declarator when the fast path applies.</param>
    /// <returns><see langword="true"/> when the type has exactly one unambiguous syntax-only candidate.</returns>
    internal static bool TryGetSingleSyntaxOnlyCandidate(TypeDeclarationSyntax type, out VariableDeclaratorSyntax? variable)
    {
        if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
        {
            variable = null;
            return false;
        }

        VariableDeclaratorSyntax? candidateVariable = null;
        var candidateCount = 0;
        for (var i = 0; i < type.Members.Count; i++)
        {
            if (type.Members[i] is not FieldDeclarationSyntax field
                || !HasPrivateReadonlyModifiers(field.Modifiers)
                || !IsUnambiguousObjectType(field.Declaration.Type)
                || field.Declaration.Variables is not [var candidate]
                || candidate.Initializer is null
                || !IsParameterlessNew(candidate.Initializer.Value))
            {
                continue;
            }

            candidateCount++;
            if (candidateCount > 1)
            {
                variable = null;
                return false;
            }

            candidateVariable = candidate;
        }

        variable = candidateVariable;
        return candidateCount == 1;
    }

    /// <summary>Returns whether a type contains only unshadowed lock-target uses of the named field.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="fieldName">The candidate field name.</param>
    /// <returns><see langword="true"/> when every matching identifier is a direct lock target and at least one exists.</returns>
    internal static bool HasOnlyUnshadowedLockUses(TypeDeclarationSyntax type, string fieldName)
    {
        var hasLockUse = false;
        foreach (var token in type.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.ValueText != fieldName)
            {
                continue;
            }

            switch (ClassifyFieldNameToken(type, token, fieldName))
            {
                case FieldNameTokenKind.Ignore:
                {
                    continue;
                }

                case FieldNameTokenKind.LockUse:
                {
                    hasLockUse = true;
                    continue;
                }

                case FieldNameTokenKind.Conflict:
                    return false;
            }
        }

        return hasLockUse;
    }

    /// <summary>Classifies how a matching identifier token affects the syntax-only lock-use fast path.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="token">The identifier token to classify.</param>
    /// <param name="fieldName">The candidate field name.</param>
    /// <returns>The token classification.</returns>
    internal static FieldNameTokenKind ClassifyFieldNameToken(TypeDeclarationSyntax type, SyntaxToken token, string fieldName) =>
        !TryGetRelevantFieldNameParent(type, token, fieldName, out var parent)
            ? FieldNameTokenKind.Ignore
            : ClassifyFieldNameParent(parent!);

    /// <summary>Returns whether a field declaration could be an object lock candidate using syntax-only checks.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the field is worth further candidate analysis.</returns>
    internal static bool CouldBeCandidateLockField(FieldDeclarationSyntax field)
        => IsObjectType(field.Declaration.Type)
            && HasPrivateReadonlyModifiers(field.Modifiers)
            && field.Declaration.Variables is [var only]
            && field.Parent is TypeDeclarationSyntax type
            && !ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            && only.Initializer is not null;

    /// <summary>Reports SST1900 for a candidate field's containing type once per compilation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="analyzedTypes">The containing types already analyzed in this compilation.</param>
    private static void AnalyzeField(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<TypeDeclarationSyntax, byte> analyzedTypes)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!CouldBeCandidateLockField(field)
            || field.Parent is not TypeDeclarationSyntax type
            || !analyzedTypes.TryAdd(type, 0))
        {
            return;
        }

        AnalyzeType(context, type);
    }

    /// <summary>Reports SST1900 when a type contains a dedicated object lock that could be a Lock.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="type">The containing type to analyze.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax type)
    {
        if (TryGetSingleSyntaxOnlyCandidate(type, out var syntaxCandidate)
            && syntaxCandidate is not null
            && HasOnlyUnshadowedLockUses(type, syntaxCandidate.Identifier.ValueText))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                ConcurrencyRules.PreferLockType,
                syntaxCandidate.SyntaxTree,
                syntaxCandidate.Identifier.Span,
                syntaxCandidate.Identifier.ValueText));

            return;
        }

        var candidates = GetCandidateFields(context.SemanticModel, type, context.CancellationToken);
        if (candidates is null || candidates.Count == 0)
        {
            return;
        }

        if (!TryMarkSingleCandidateSyntaxOnlyLockUse(type, candidates))
        {
            ScanFieldReferences(context.SemanticModel, type, candidates, context.CancellationToken);
        }

        foreach (var candidate in candidates.Values)
        {
            if (!candidate.HasLockUse || candidate.HasNonLockUse)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ConcurrencyRules.PreferLockType,
                candidate.Variable.SyntaxTree,
                candidate.Variable.Identifier.Span,
                candidate.Variable.Identifier.ValueText));
        }
    }

    /// <summary>Tries to satisfy the common single-candidate case with syntax only.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="candidates">The candidate fields.</param>
    /// <returns><see langword="true"/> when the syntax-only path fully handled the type.</returns>
    private static bool TryMarkSingleCandidateSyntaxOnlyLockUse(TypeDeclarationSyntax type, Dictionary<string, CandidateFieldState> candidates)
    {
        if (candidates.Count != 1)
        {
            return false;
        }

        using var enumerator = candidates.Values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        var candidate = enumerator.Current;
        if (!HasOnlyUnshadowedLockUses(type, candidate.Variable.Identifier.ValueText))
        {
            return false;
        }

        candidate.HasLockUse = true;
        return true;
    }

    /// <summary>Returns whether a field is a private readonly object initialized with a parameterless new.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="variable">The single variable declarator when the field qualifies.</param>
    /// <returns><see langword="true"/> when the field is a candidate lock object.</returns>
    private static bool IsCandidateLockField(FieldDeclarationSyntax field, out VariableDeclaratorSyntax? variable)
    {
        if (!CouldBeCandidateLockField(field))
        {
            variable = null;
            return false;
        }

        variable = field.Declaration.Variables[0];
        var initializer = variable.Initializer!.Value;
        if (IsParameterlessNew(initializer))
        {
            return true;
        }

        variable = null;
        return false;
    }

    /// <summary>Returns whether an initializer is <c>new()</c> or <c>new object()</c> with no arguments.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <returns><see langword="true"/> for a parameterless object creation.</returns>
    private static bool IsParameterlessNew(ExpressionSyntax expression) => expression switch
    {
        ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } => true,
        ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } creation => IsObjectType(creation.Type),
        _ => false
    };

    /// <summary>Returns whether a type syntax is one of the common spellings of <c>System.Object</c>.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for a syntax shape that can name <c>System.Object</c>.</returns>
    private static bool IsObjectType(TypeSyntax type)
        => type switch
        {
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword) => true,
            IdentifierNameSyntax { Identifier.ValueText: "Object" } => true,
            QualifiedNameSyntax { Right.Identifier.ValueText: "Object", Left: var left } => IsSystemNamespace(left),
            _ => false
        };

    /// <summary>Returns the relevant parent node for a matching field-name token in the current type.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="token">The identifier token to inspect.</param>
    /// <param name="fieldName">The candidate field name.</param>
    /// <param name="parent">The relevant syntax parent when the token belongs to the current type.</param>
    /// <returns><see langword="true"/> when the token is relevant to the current type.</returns>
    private static bool TryGetRelevantFieldNameParent(TypeDeclarationSyntax type, SyntaxToken token, string fieldName, out SyntaxNode? parent)
    {
        if (token.ValueText != fieldName
            || token.Parent is not { } syntaxParent
            || syntaxParent.FirstAncestorOrSelf<TypeDeclarationSyntax>() != type)
        {
            parent = null;
            return false;
        }

        parent = syntaxParent;
        return true;
    }

    /// <summary>Classifies how a relevant field-name syntax parent affects the syntax-only lock-use fast path.</summary>
    /// <param name="parent">The syntax parent to classify.</param>
    /// <returns>The token classification.</returns>
    [SuppressMessage("Critical Code Smell", "the rule:Methods and properties should not be too complex", Justification = "The method uses a switch for performance reasons.")]
    private static FieldNameTokenKind ClassifyFieldNameParent(SyntaxNode parent) =>
        parent switch
        {
            IdentifierNameSyntax identifier => IsLockTarget(identifier)
                ? FieldNameTokenKind.LockUse
                : FieldNameTokenKind.Conflict,
            VariableDeclaratorSyntax variable => variable.Parent?.Parent is BaseFieldDeclarationSyntax
                ? FieldNameTokenKind.Ignore
                : FieldNameTokenKind.Conflict,
            ParameterSyntax or SingleVariableDesignationSyntax or CatchDeclarationSyntax or ForEachStatementSyntax =>
                FieldNameTokenKind.Conflict,
            _ => FieldNameTokenKind.Ignore
        };

    /// <summary>Returns whether a type syntax unambiguously denotes <c>System.Object</c> without semantic binding.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for unambiguous object spellings.</returns>
    private static bool IsUnambiguousObjectType(TypeSyntax type)
        => (type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword))
            || (type is QualifiedNameSyntax { Right.Identifier.ValueText: "Object", Left: var left } && IsSystemNamespace(left));

    /// <summary>Returns whether a modifier list contains both <c>private</c> and <c>readonly</c>.</summary>
    /// <param name="modifiers">The modifier list to inspect.</param>
    /// <returns><see langword="true"/> when both required modifiers are present.</returns>
    private static bool HasPrivateReadonlyModifiers(SyntaxTokenList modifiers)
    {
        var hasPrivate = false;
        var hasReadonly = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            switch (modifiers[i].Kind())
            {
                case SyntaxKind.PrivateKeyword:
                {
                    hasPrivate = true;
                    break;
                }

                case SyntaxKind.ReadOnlyKeyword:
                {
                    hasReadonly = true;
                    break;
                }
            }
        }

        return hasPrivate && hasReadonly;
    }

    /// <summary>Returns whether a name syntax denotes the <c>System</c> namespace.</summary>
    /// <param name="name">The syntax to inspect.</param>
    /// <returns><see langword="true"/> when the syntax denotes <c>System</c>.</returns>
    private static bool IsSystemNamespace(NameSyntax name)
        => name is IdentifierNameSyntax { Identifier.ValueText: "System" }
            or AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global", Name.Identifier.ValueText: "System" };

    /// <summary>Returns whether every reference to the field within the type is a lock target (and there is at least one).</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The candidate fields keyed by simple field name.</returns>
    private static Dictionary<string, CandidateFieldState>? GetCandidateFields(
        SemanticModel model,
        TypeDeclarationSyntax type,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CandidateFieldState>? candidates = null;
        for (var i = 0; i < type.Members.Count; i++)
        {
            var member = type.Members[i];
            if (member is not FieldDeclarationSyntax field || !IsCandidateLockField(field, out var variable))
            {
                continue;
            }

            if (model.GetDeclaredSymbol(variable!, cancellationToken) is not IFieldSymbol { Type.SpecialType: SpecialType.System_Object } fieldSymbol)
            {
                continue;
            }

            candidates ??= new(type.Members.Count, StringComparer.Ordinal);
            candidates[variable!.Identifier.ValueText] = new(fieldSymbol, variable);
        }

        return candidates;
    }

    /// <summary>Scans the declaring type once and records whether candidate fields are used only as lock targets.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="candidates">The candidate fields to track.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    private static void ScanFieldReferences(
        SemanticModel model,
        TypeDeclarationSyntax type,
        Dictionary<string, CandidateFieldState> candidates,
        CancellationToken cancellationToken)
    {
        var state = new LockFieldReferenceScanState(model, candidates, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, LockFieldReferenceScanState>(type, ref state, VisitFieldReference);
    }

    /// <summary>Returns whether an identifier reference is the expression locked by a <c>lock</c> statement.</summary>
    /// <param name="identifier">The reference to the field (bare or as the name of a member access).</param>
    /// <returns><see langword="true"/> when the reference is a lock target.</returns>
    private static bool IsLockTarget(IdentifierNameSyntax identifier)
    {
        ExpressionSyntax expression = identifier;
        if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
        {
            expression = access;
        }

        return expression.Parent is LockStatementSyntax lockStatement && lockStatement.Expression == expression;
    }

    /// <summary>Records one candidate field reference encountered during the type scan.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitFieldReference(IdentifierNameSyntax identifier, ref LockFieldReferenceScanState state)
    {
        if (!state.Candidates.TryGetValue(identifier.Identifier.ValueText, out var candidate)
            || !SymbolEqualityComparer.Default.Equals(state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol, candidate.FieldSymbol))
        {
            return true;
        }

        if (IsLockTarget(identifier))
        {
            candidate.HasLockUse = true;
            return true;
        }

        candidate.HasNonLockUse = true;
        return true;
    }

    /// <summary>Captures the state required while scanning candidate field references.</summary>
    private readonly record struct LockFieldReferenceScanState(
        SemanticModel Model,
        Dictionary<string, CandidateFieldState> Candidates,
        CancellationToken CancellationToken);

    /// <summary>Tracks the lock-only usage state for one candidate field.</summary>
    private sealed class CandidateFieldState
    {
        /// <summary>Initializes a new instance of the <see cref="CandidateFieldState"/> class.</summary>
        /// <param name="fieldSymbol">The candidate field symbol.</param>
        /// <param name="variable">The candidate variable declarator.</param>
        public CandidateFieldState(IFieldSymbol fieldSymbol, VariableDeclaratorSyntax variable)
        {
            FieldSymbol = fieldSymbol;
            Variable = variable;
        }

        /// <summary>Gets the candidate field symbol.</summary>
        public IFieldSymbol FieldSymbol { get; }

        /// <summary>Gets the candidate variable declarator.</summary>
        public VariableDeclaratorSyntax Variable { get; }

        /// <summary>Gets or sets a value indicating whether a qualifying lock use was found.</summary>
        public bool HasLockUse { get; set; }

        /// <summary>Gets or sets a value indicating whether a non-lock use was found.</summary>
        public bool HasNonLockUse { get; set; }
    }
}
