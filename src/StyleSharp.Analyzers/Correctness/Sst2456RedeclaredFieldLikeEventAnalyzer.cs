// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a field-like event declaration that carries <c>override</c>, or <c>new</c> that hides an inherited
/// event (SST2456). A field-like redeclaration synthesizes a second backing delegate field in the hierarchy:
/// a handler added through one type's accessor lands in one field, while a raise that reads another type's
/// field walks the other, so the handler silently never runs.
/// </summary>
/// <remarks>
/// <para>
/// Only a redeclaration that brings its own storage is reported. An <c>override</c> is definitional — the
/// derived field-like event always gets a field distinct from the base's. A <c>new</c> declaration is
/// reported only when the semantic model confirms a same-named event is inherited from a base type; a
/// <c>new</c> that hides nothing is left to the compiler's own diagnostic.
/// </para>
/// <para>Several shapes cannot split their storage, and none of them is reported:</para>
/// <list type="bullet">
/// <item><description>A plain field-like event with neither modifier keeps its single compiler-generated
/// field. An overridable field-like event with no <c>override</c> or <c>new</c> is reported elsewhere and is
/// not repeated here.</description></item>
/// <item><description>An <c>abstract</c> event (including an <c>abstract override</c> that re-abstracts)
/// declares no backing field.</description></item>
/// <item><description>An event with explicit <c>add</c>/<c>remove</c> accessors — an
/// <see cref="EventDeclarationSyntax"/> the callback never visits — stores its subscribers wherever the
/// author chose, shared across the hierarchy.</description></item>
/// <item><description>An interface event declares no storage; a base interface's event lives outside the
/// class base chain, so a <c>new</c> interface event hides nothing that could split.</description></item>
/// </list>
/// <para>
/// The clean path is one scan of the modifier list and binds nothing. The declared symbol is resolved only
/// for a declaration that spells <c>override</c> or <c>new</c>, and a base type is walked for a hiding
/// <c>new</c> only after the symbol proves it is not an override.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2456RedeclaredFieldLikeEventAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The message argument used when the field-like event overrides an inherited event.</summary>
    private const string OverridesVerb = "overrides";

    /// <summary>The message argument used when the field-like event hides an inherited event.</summary>
    private const string HidesVerb = "hides";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.RedeclaredFieldLikeEvent);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>Analyzes one field-like event declaration for an override or a hiding <c>new</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EventFieldDeclarationSyntax)context.Node;
        if (!IsRedeclarationCandidate(declaration.Modifiers, out var hasNew))
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var declarator = variables[i];
            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken)
                is not IEventSymbol symbol)
            {
                continue;
            }

            var verb = Classify(symbol, hasNew);
            if (verb is null)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.RedeclaredFieldLikeEvent,
                declarator.Identifier.GetLocation(),
                symbol.Name,
                verb));
        }
    }

    /// <summary>Reads the modifiers that decide whether the declaration is worth binding.</summary>
    /// <param name="modifiers">The event declaration's modifiers.</param>
    /// <param name="hasNew">Set to whether the declaration spells <c>new</c>.</param>
    /// <returns><see langword="true"/> when the declaration carries <c>override</c> or <c>new</c> and is not <c>abstract</c>.</returns>
    /// <remarks>
    /// An <c>abstract</c> event — including an <c>abstract override</c> that re-abstracts — declares no
    /// backing field, so it ends the analysis before anything binds.
    /// </remarks>
    private static bool IsRedeclarationCandidate(SyntaxTokenList modifiers, out bool hasNew)
    {
        hasNew = false;
        var hasOverride = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind == SyntaxKind.AbstractKeyword)
            {
                return false;
            }

            if (kind == SyntaxKind.OverrideKeyword)
            {
                hasOverride = true;
            }
            else if (kind == SyntaxKind.NewKeyword)
            {
                hasNew = true;
            }
        }

        return hasOverride || hasNew;
    }

    /// <summary>Decides how a candidate event splits its storage, if it does.</summary>
    /// <param name="symbol">The declared event symbol.</param>
    /// <param name="hasNew">Whether the declaration spells <c>new</c>.</param>
    /// <returns>The message verb, or <see langword="null"/> when the event does not split its storage.</returns>
    /// <remarks>
    /// <c>IsOverride</c> confirms the compiler accepted an <c>override</c> — a broken
    /// override that binds to nothing is not reported. A <c>new</c> is reported only once a base type is
    /// confirmed to declare a same-named event; hiding nothing is the compiler's own diagnostic.
    /// </remarks>
    private static string? Classify(IEventSymbol symbol, bool hasNew)
    {
        if (symbol.IsOverride)
        {
            return OverridesVerb;
        }

        return hasNew && HidesInheritedEvent(symbol) ? HidesVerb : null;
    }

    /// <summary>Returns whether a base type in the class chain declares a same-named event.</summary>
    /// <param name="symbol">The declared event symbol.</param>
    /// <returns><see langword="true"/> when a base type declares an event with the same name.</returns>
    /// <remarks>
    /// The walk follows <c>BaseType</c>, so an interface's base interfaces are not
    /// searched: a <c>new</c> interface event hides no storage and is not reported.
    /// </remarks>
    private static bool HidesInheritedEvent(IEventSymbol symbol)
    {
        var baseType = symbol.ContainingType.BaseType;
        while (baseType is not null)
        {
            var candidates = baseType.GetMembers(symbol.Name);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IEventSymbol)
                {
                    return true;
                }
            }

            baseType = baseType.BaseType;
        }

        return false;
    }
}
