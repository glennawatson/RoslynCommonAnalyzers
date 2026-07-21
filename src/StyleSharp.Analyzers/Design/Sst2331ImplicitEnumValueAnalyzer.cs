// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum with a member whose value is left to declaration order rather than written out (SST2331).
/// A member with no <c>= value</c> takes the number of the slot it happens to sit in, and that number is the
/// one that gets persisted and compared — so inserting or reordering a member above it silently repoints
/// stored data at a different name.
/// </summary>
/// <remarks>
/// This is an opinionated, house-style rule — many enums are never persisted and read fine as an ordered
/// list — so it is disabled by default and opt-in through <c>.editorconfig</c>. The check is purely
/// syntactic: a scan of the members for a missing initializer, on the enum declaration.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2331ImplicitEnumValueAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.EnumMembersShouldBeExplicit);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Reports an enum that leaves at least one member's value implicit.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].EqualsValue is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignRules.EnumMembersShouldBeExplicit,
                    declaration.Identifier.GetLocation(),
                    declaration.Identifier.ValueText));
                return;
            }
        }
    }
}
