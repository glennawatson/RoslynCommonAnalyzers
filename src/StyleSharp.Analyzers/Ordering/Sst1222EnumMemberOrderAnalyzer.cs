// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum whose members all carry explicit values but are not declared in ascending order (SST1222).
/// A <c>[Flags]</c> enum is left alone, and so is an enum with any implicitly numbered member, because
/// reordering there would change what the numbering assigns.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1222EnumMemberOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute name that marks an enum as a bit field.</summary>
    private const string FlagsAttributeName = "Flags";

    /// <summary>The fully suffixed attribute name that marks an enum as a bit field.</summary>
    private const string FlagsAttributeFullName = "FlagsAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(OrderingRules.EnumMemberOrder);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Returns whether an enum is a bit field, whose grouping outranks its numeric order.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns><see langword="true"/> when a <c>Flags</c> attribute is written on the declaration.</returns>
    internal static bool IsFlagsEnum(EnumDeclarationSyntax declaration)
    {
        var lists = declaration.AttributeLists;
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var name = attributes[j].Name switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.ValueText,
                    _ => null,
                };

                if (name is FlagsAttributeName or FlagsAttributeFullName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Collects the members' declared values, in declaration order.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The values, or <see langword="null"/> when any member lacks an explicit constant value.</returns>
    internal static long[]? TryGetExplicitValues(
        EnumDeclarationSyntax declaration,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var members = declaration.Members;
        var values = new long[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member.EqualsValue is null
                || model.GetDeclaredSymbol(member, cancellationToken) is not { ConstantValue: { } constant })
            {
                return null;
            }

            try
            {
                values[i] = Convert.ToInt64(constant, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                // A ulong past long.MaxValue cannot be ordered against the rest on one scale; leave the enum alone.
                return null;
            }
        }

        return values;
    }

    /// <summary>Reports the first member of an enum that breaks ascending value order.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        if (declaration.Members.Count < 2 || IsFlagsEnum(declaration))
        {
            return;
        }

        if (TryGetExplicitValues(declaration, context.SemanticModel, context.CancellationToken) is not { } values)
        {
            return;
        }

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] >= values[i - 1])
            {
                continue;
            }

            var member = declaration.Members[i];
            context.ReportDiagnostic(DiagnosticHelper.Create(
                OrderingRules.EnumMemberOrder,
                member.SyntaxTree,
                member.Identifier.Span,
                member.Identifier.ValueText));
            return;
        }
    }
}
