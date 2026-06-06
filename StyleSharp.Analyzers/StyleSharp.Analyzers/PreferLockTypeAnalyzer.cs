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

            start.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Reports SST1900 when a field is a dedicated object lock that could be a Lock.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!IsCandidateLockField(field, out var variable)
            || field.Parent is not TypeDeclarationSyntax type
            || type.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return;
        }

        var name = variable!.Identifier.ValueText;
        var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as IFieldSymbol;
        if (fieldSymbol is not { Type.SpecialType: SpecialType.System_Object }
            || !UsedOnlyAsLockTarget(context.SemanticModel, type, fieldSymbol, name, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.PreferLockType, variable.Identifier.GetLocation(), name));
    }

    /// <summary>Returns whether a field is a private readonly object initialized with a parameterless new.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="variable">The single variable declarator when the field qualifies.</param>
    /// <returns><see langword="true"/> when the field is a candidate lock object.</returns>
    private static bool IsCandidateLockField(FieldDeclarationSyntax field, out VariableDeclaratorSyntax? variable)
    {
        variable = null;
        if (!field.Modifiers.Any(SyntaxKind.PrivateKeyword)
            || !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            || field.Declaration.Variables is not [var only]
            || only.Initializer is not { Value: { } initializer }
            || !IsParameterlessNew(initializer))
        {
            return false;
        }

        variable = only;
        return true;
    }

    /// <summary>Returns whether an initializer is <c>new()</c> or <c>new object()</c> with no arguments.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <returns><see langword="true"/> for a parameterless object creation.</returns>
    private static bool IsParameterlessNew(ExpressionSyntax expression) => expression switch
    {
        ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } => true,
        ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } creation => IsObjectKeyword(creation.Type),
        _ => false,
    };

    /// <summary>Returns whether a type syntax is the <c>object</c> keyword.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for the <c>object</c> predefined type.</returns>
    private static bool IsObjectKeyword(TypeSyntax type)
        => type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword);

    /// <summary>Returns whether every reference to the field within the type is a lock target (and there is at least one).</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="fieldSymbol">The field symbol.</param>
    /// <param name="name">The field name (used as a cheap pre-filter).</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the field is used solely as a lock target.</returns>
    private static bool UsedOnlyAsLockTarget(SemanticModel model, TypeDeclarationSyntax type, IFieldSymbol fieldSymbol, string name, CancellationToken cancellationToken)
    {
        var lockUses = 0;
        foreach (var identifier in type.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (identifier.Identifier.ValueText != name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, fieldSymbol))
            {
                continue;
            }

            if (!IsLockTarget(identifier))
            {
                return false;
            }

            lockUses++;
        }

        return lockUses > 0;
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
}
