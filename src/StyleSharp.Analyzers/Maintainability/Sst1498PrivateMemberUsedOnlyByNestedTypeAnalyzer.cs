// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a private field, property or method that only one of the type's nested types ever uses (SST1498).
/// It is declared one level further out than anything needs it to be.
/// </summary>
/// <remarks>
/// <para>
/// The analysis is whole-type, but the diagnostic stays <em>local</em>: a private member cannot be named
/// from outside the type that declares it, so every use of it is inside the one declaration a syntax-node
/// action already hands over. Nothing is deferred to a compilation-end action, which would produce a
/// non-local diagnostic the IDE cannot attach a fix to and cannot refresh as you type.
/// </para>
/// <para>
/// The rule is deliberately quiet where the move is not obvious. A member two nested types share has no
/// single home. A member the type itself also uses is not the nested type's to take. A member with an
/// attribute may be used by something the compiler never shows us — a serializer, a test runner, a source
/// generator — so it is left alone. A <c>partial</c> type is skipped entirely: its other parts are other
/// files, and a use in one of them would be invisible here.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1498PrivateMemberUsedOnlyByNestedTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.PrivateMemberUsedOnlyByNestedType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    /// <summary>Reports the type's private members that one nested type has taken over.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        if (NestedTypeOnlyMembers.Collect(context.SemanticModel, type, context.CancellationToken) is not { } members)
        {
            return;
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.PrivateMemberUsedOnlyByNestedType,
                member.Identifier.GetLocation(),
                member.Symbol.Name,
                member.NestedUser!.Identifier.ValueText));
        }
    }
}
