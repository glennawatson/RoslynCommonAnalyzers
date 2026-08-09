// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum member that repeats an earlier member's value without saying so (SST2455). A deliberate
/// alias names the member it duplicates — <c>Default = Read</c> — and is left alone; a bare number that
/// happens to collide is reported.
/// </summary>
/// <remarks>
/// One pass over the members collects the declared constant values, so the enum is walked once however many
/// members it has. An enum whose members are all distinct allocates only the lookup that pass needs.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2455DuplicateEnumValueAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.DuplicateEnumValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Returns whether a member states its value by naming another member of the same enum.</summary>
    /// <param name="member">The enum member.</param>
    /// <param name="declaration">The enum the member belongs to.</param>
    /// <returns><see langword="true"/> when the initializer mentions a sibling member's name.</returns>
    /// <remarks>
    /// The names are compared syntactically. An initializer that reads a sibling is an alias or a combination
    /// written in terms of the enum's own vocabulary, which is the deliberate form this rule exists to allow.
    /// </remarks>
    internal static bool IsExpressedAsAnAlias(EnumMemberDeclarationSyntax member, EnumDeclarationSyntax declaration)
    {
        if (member.EqualsValue is not { } initializer)
        {
            return false;
        }

        var state = (Declaration: declaration, Found: false);
        if (initializer.Value is IdentifierNameSyntax root && NamesSibling(root, declaration))
        {
            return true;
        }

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (EnumDeclarationSyntax Declaration, bool Found)>(
            initializer.Value,
            ref state,
            static (node, ref current) =>
            {
                if (!NamesSibling(node, current.Declaration))
                {
                    return true;
                }

                current.Found = true;
                return false;
            });

        return state.Found;
    }

    /// <summary>Returns whether an identifier names one of the enum's own members.</summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns><see langword="true"/> when a member of the enum carries that name.</returns>
    private static bool NamesSibling(IdentifierNameSyntax identifier, EnumDeclarationSyntax declaration)
    {
        var name = identifier.Identifier.ValueText;
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (string.Equals(members[i].Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports every member of one enum that silently repeats an earlier value.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        var members = declaration.Members;
        if (members.Count < 2)
        {
            return;
        }

        var seen = new Dictionary<object, string>(members.Count);
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { ConstantValue: { } value })
            {
                continue;
            }

            if (!seen.TryGetValue(value, out var first))
            {
                seen.Add(value, member.Identifier.ValueText);
                continue;
            }

            if (IsExpressedAsAnAlias(member, declaration))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.DuplicateEnumValue,
                member.SyntaxTree,
                member.Identifier.Span,
                member.Identifier.ValueText,
                first));
        }
    }
}
