// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a write made through an extension block's receiver when that receiver is a struct passed by
/// value (SST2469). The member holds a copy, so the write never reaches the caller's value and is lost
/// when the member returns — the code compiles and the accessor runs, which is what makes it hard to
/// see. A <c>ref</c> receiver is never reported, and neither is a reference type. The receiver's type is
/// resolved only after a write to it has been found syntactically.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2469DiscardedReceiverWriteAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.DiscardedReceiverWrite);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // An extension block has no syntax kind to register on across every Roslyn slot, so the
        // containing class is walked instead.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    /// <summary>Returns the identifier a write target is rooted at, or <see langword="null"/>.</summary>
    /// <param name="target">The assignment or increment target.</param>
    /// <returns>The leftmost identifier name, or <see langword="null"/> when the target is not rooted at one.</returns>
    /// <remarks>
    /// Only the left spine is followed: <c>point.X</c>, <c>point.Inner.X</c> and <c>point[0]</c> all write
    /// into <c>point</c>, while <c>Other(point).X</c> writes into whatever the call returned.
    /// </remarks>
    internal static IdentifierNameSyntax? WriteRoot(ExpressionSyntax target)
    {
        var current = target;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                {
                    current = parenthesized.Expression;
                    break;
                }

                case MemberAccessExpressionSyntax member when member.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                {
                    current = member.Expression;
                    break;
                }

                case ElementAccessExpressionSyntax element:
                {
                    current = element.Expression;
                    break;
                }

                case IdentifierNameSyntax identifier:
                {
                    return identifier;
                }

                default:
                {
                    return null;
                }
            }
        }
    }

    /// <summary>Reports every discarded receiver write in a class's extension blocks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var containingClass = (ClassDeclarationSyntax)context.Node;
        foreach (var member in containingClass.Members)
        {
            if (member is TypeDeclarationSyntax block && ExtensionBlockHelper.IsExtensionBlock(block))
            {
                AnalyzeBlock(in context, block);
            }
        }
    }

    /// <summary>Reports the writes one block makes into a by-value struct receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    private static void AnalyzeBlock(in SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block)
    {
        // A 'ref' receiver writes through to the caller's value; 'in' and 'ref readonly' cannot be
        // assigned at all, so the compiler already rejects those.
        if (block.ParameterList?.Parameters is not { Count: > 0 } parameters
            || parameters[0] is not { Type: { } receiverType } receiver
            || ModifierListHelper.Contains(receiver.Modifiers, SyntaxKind.RefKeyword))
        {
            return;
        }

        var receiverName = receiver.Identifier.ValueText;
        if (receiverName.Length == 0)
        {
            return;
        }

        var writes = new List<ExpressionSyntax>(block.Members.Count);
        CollectReceiverWrites(block, receiverName, writes);
        if (writes.Count == 0)
        {
            return;
        }

        // The type is resolved only now: a block that never writes to its receiver pays nothing.
        if (context.SemanticModel.GetTypeInfo(receiverType, context.CancellationToken).Type is not { IsValueType: true })
        {
            return;
        }

        for (var index = 0; index < writes.Count; index++)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.DiscardedReceiverWrite,
                writes[index].GetLocation(),
                receiverName));
        }
    }

    /// <summary>Collects every write into the receiver beneath a node.</summary>
    /// <param name="node">The node to scan.</param>
    /// <param name="receiverName">The receiver parameter name.</param>
    /// <param name="into">The list receiving each write target.</param>
    private static void CollectReceiverWrites(SyntaxNode node, string receiverName, List<ExpressionSyntax> into)
    {
        foreach (var child in node.ChildNodes())
        {
            if (WriteTarget(child) is { } target
                && WriteRoot(target) is { } root
                && string.Equals(root.Identifier.ValueText, receiverName, StringComparison.Ordinal))
            {
                into.Add(target);
            }

            CollectReceiverWrites(child, receiverName, into);
        }
    }

    /// <summary>Returns the target a node writes to, or <see langword="null"/> when it writes to nothing.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns>The write target expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? WriteTarget(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left,
        PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression) => prefix.Operand,
        PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression) => postfix.Operand,
        _ => null,
    };
}
