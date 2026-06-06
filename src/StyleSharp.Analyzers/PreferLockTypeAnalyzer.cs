// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
public sealed class PreferLockTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the .NET 9 lock type.</summary>
    private const string LockMetadataName = "System.Threading.Lock";

    /// <summary>The type declarations that can own fields eligible for SST1900.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration);

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

            start.RegisterSyntaxNodeAction(AnalyzeType, HandledKinds);
        });
    }

    /// <summary>Returns whether a field declaration could be an object lock candidate using syntax-only checks.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the field is worth further candidate analysis.</returns>
    internal static bool CouldBeCandidateLockField(FieldDeclarationSyntax field)
        => IsObjectType(field.Declaration.Type)
            && HasPrivateReadonlyModifiers(field.Modifiers)
            && field.Declaration.Variables is [var only]
            && only.Initializer is not null;

    /// <summary>Reports SST1900 when a type contains a dedicated object lock that could be a Lock.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
        {
            return;
        }

        var candidates = GetCandidateFields(context.SemanticModel, type, context.CancellationToken);
        if (candidates is null || candidates.Count == 0)
        {
            return;
        }

        ScanFieldReferences(context.SemanticModel, type, candidates, context.CancellationToken);

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
        _ => false,
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
            _ => false,
        };

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

            candidates ??= new Dictionary<string, CandidateFieldState>(type.Members.Count, StringComparer.Ordinal);
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
        foreach (var node in type.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (!candidates.TryGetValue(identifier.Identifier.ValueText, out var candidate)
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, candidate.FieldSymbol))
            {
                continue;
            }

            if (IsLockTarget(identifier))
            {
                candidate.HasLockUse = true;
                continue;
            }

            candidate.HasNonLockUse = true;
        }
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
