// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags structs whose instance state is already immutable but that are not declared
/// <c>readonly</c> (PSH1014). Without the modifier the compiler defensively copies the struct
/// before member calls through <c>in</c> parameters, ref readonly locals, and readonly fields.
/// The check is purely syntactic: every instance field must be readonly or const, every
/// auto-property accessor must be get or init, and nothing may reassign or ref-expose
/// <c>this</c>. Partial declarations, fixed buffers, field-like instance events, and record
/// structs with a primary constructor (whose synthesized properties are mutable) are skipped.
/// Reported only on C# 7.3+ trees, where the modifier exists.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1014ReadonlyStructAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.MakeStructReadonly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeStruct, SyntaxKind.StructDeclaration, SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Returns whether a struct declaration's instance state is immutable, making it readonly-eligible.</summary>
    /// <param name="declaration">The struct or record struct declaration.</param>
    /// <returns><see langword="true"/> when every instance field is readonly, no auto-property has a set accessor, and <c>this</c> is never reassigned.</returns>
    internal static bool HasImmutableInstanceState(TypeDeclarationSyntax declaration)
    {
        if (declaration is RecordDeclarationSyntax { ParameterList: not null })
        {
            return false;
        }

        foreach (var member in declaration.Members)
        {
            if (!IsImmutableMember(member))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether one struct member keeps the instance state immutable.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member neither stores mutable instance state nor mutates <c>this</c>.</returns>
    private static bool IsImmutableMember(MemberDeclarationSyntax member)
        => member switch
        {
            FieldDeclarationSyntax field => IsImmutableField(field),
            EventFieldDeclarationSyntax eventField => eventField.Modifiers.Any(SyntaxKind.StaticKeyword),
            PropertyDeclarationSyntax property => IsImmutableProperty(property),
            BaseTypeDeclarationSyntax => true,
            _ => !MutatesThis(member),
        };

    /// <summary>Returns whether a field declaration is compatible with a readonly struct.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the field is static, const, or readonly, and not a fixed buffer.</returns>
    private static bool IsImmutableField(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        if (modifiers.Any(SyntaxKind.FixedKeyword))
        {
            return false;
        }

        return modifiers.Any(SyntaxKind.StaticKeyword)
            || modifiers.Any(SyntaxKind.ConstKeyword)
            || modifiers.Any(SyntaxKind.ReadOnlyKeyword);
    }

    /// <summary>Returns whether a property declaration is compatible with a readonly struct.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> unless an instance auto-property declares a plain set accessor.</returns>
    private static bool IsImmutableProperty(PropertyDeclarationSyntax property)
    {
        if (property.Modifiers.Any(SyntaxKind.StaticKeyword) || property.AccessorList is not { } accessors)
        {
            return true;
        }

        foreach (var accessor in accessors.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                && accessor.Body is null
                && accessor.ExpressionBody is null)
            {
                return false;
            }

            if (MutatesThis(accessor))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a member's body reassigns <c>this</c> or exposes it by writable reference.</summary>
    /// <param name="node">The member or accessor to scan.</param>
    /// <returns><see langword="true"/> when a <c>this</c> token is an assignment target or a ref/out argument.</returns>
    private static bool MutatesThis(SyntaxNode node)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendantTokens(
            node,
            ref found,
            static (in SyntaxToken token, ref bool state) =>
            {
                if (!token.IsKind(SyntaxKind.ThisKeyword)
                    || token.Parent is not ThisExpressionSyntax thisExpression
                    || !IsWrittenThrough(thisExpression))
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a <c>this</c> expression is written through.</summary>
    /// <param name="thisExpression">The this expression.</param>
    /// <returns><see langword="true"/> for assignment targets and ref/out arguments.</returns>
    private static bool IsWrittenThrough(ThisExpressionSyntax thisExpression)
        => thisExpression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == thisExpression,
            ArgumentSyntax argument => !argument.RefOrOutKeyword.IsKind(SyntaxKind.None),
            _ => false,
        };

    /// <summary>Reports PSH1014 for an immutable struct missing the readonly modifier.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStruct(SyntaxNodeAnalysisContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || declaration.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7_2 }
            || !HasImmutableInstanceState(declaration))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.MakeStructReadonly,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }
}
