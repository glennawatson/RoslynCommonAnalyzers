// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member of an <c>extension(Receiver) { … }</c> block whose body never reads the block's
/// receiver (SST1711). Such a member answers the same for every instance and belongs outside the block.
/// Static members are skipped: the receiver is not in scope for one, so not reading it says nothing.
/// There is no code fix — moving the member out changes how every call site spells it, so the author
/// decides. The body is scanned only after the cheap syntactic gates, and the scan stops at the first
/// read of the receiver.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1711UnusedBlockReceiverAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name an indexer is reported under, since it has no identifier of its own.</summary>
    private const string IndexerName = "this[]";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ExtensionRules.UnusedBlockReceiver);

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

    /// <summary>Reports every extension-block member in a class that ignores its block's receiver.</summary>
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

    /// <summary>Reports the members of one block that never read its receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    private static void AnalyzeBlock(in SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block)
    {
        // A block written without a receiver name declares only static members, which never read one.
        if (block.ParameterList?.Parameters is not { Count: > 0 } parameters)
        {
            return;
        }

        var receiverName = parameters[0].Identifier.ValueText;
        if (receiverName.Length == 0 || receiverName == "_")
        {
            return;
        }

        foreach (var member in block.Members)
        {
            if (ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword)
                || BodyOf(member) is not { } body
                || ExtensionBlockHelper.ReadsIdentifier(body, receiverName))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ExtensionRules.UnusedBlockReceiver,
                NameToken(member).GetLocation(),
                MemberName(member),
                receiverName));
        }
    }

    /// <summary>Returns the node holding a member's executable body, or <see langword="null"/> when it has none.</summary>
    /// <param name="member">The block member.</param>
    /// <returns>The body, accessor list, or expression body.</returns>
    /// <remarks>
    /// An accessor list stands in for the body of a property or indexer, so one walk covers every accessor
    /// it declares — a receiver read in either the getter or the setter counts.
    /// </remarks>
    private static SyntaxNode? BodyOf(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Body ?? (SyntaxNode?)method.ExpressionBody,
        PropertyDeclarationSyntax property => property.AccessorList ?? (SyntaxNode?)property.ExpressionBody,
        IndexerDeclarationSyntax indexer => indexer.AccessorList ?? (SyntaxNode?)indexer.ExpressionBody,
        _ => null,
    };

    /// <summary>Returns the token a member is reported at.</summary>
    /// <param name="member">The block member.</param>
    /// <returns>The member's name token.</returns>
    private static SyntaxToken NameToken(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        PropertyDeclarationSyntax property => property.Identifier,
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
        _ => default,
    };

    /// <summary>Returns the name a member is reported under.</summary>
    /// <param name="member">The block member.</param>
    /// <returns>The member name.</returns>
    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        _ => IndexerName,
    };
}
