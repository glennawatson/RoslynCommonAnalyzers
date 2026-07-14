// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>[Obsolete]</c> attribute that carries a message but no <c>DiagnosticId</c> (SST2314). The
/// message tells a caller what to do; the id is what lets them do it — suppress the one migration they have
/// already scheduled, or promote it to an error — instead of being handed the same CS0618 as every other
/// deprecation in every library they reference.
/// </summary>
/// <remarks>
/// <c>ObsoleteAttribute.DiagnosticId</c> and <c>UrlFormat</c> arrived in .NET 5, so the rule is gated on the
/// property actually existing in the analyzed compilation: it resolves <c>System.ObsoleteAttribute</c> once
/// at compilation start and registers nothing at all when the type has no <c>DiagnosticId</c>. On
/// netstandard2.0, .NET Framework, and any older target the rule therefore costs nothing and can never fire,
/// which is the point — it must never ask for a property the compilation cannot bind.
/// <para>
/// An attribute with no message at all is SST2308's business and is left alone here, so the two never report
/// the same attribute.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2314ObsoleteWithoutDiagnosticIdAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ObsoleteWithoutDiagnosticId);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only when the analyzed compilation has a <c>DiagnosticId</c> to ask for.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (!SupportsDiagnosticId(context.Compilation))
        {
            return;
        }

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    /// <summary>Returns whether the compilation's obsolete attribute exposes a <c>DiagnosticId</c>.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when the property exists and can be set.</returns>
    private static bool SupportsDiagnosticId(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(ObsoleteAttributeFacts.ObsoleteAttributeFullMetadataName) is not { } obsoleteAttribute)
        {
            return false;
        }

        var candidates = obsoleteAttribute.GetMembers(ObsoleteAttributeFacts.DiagnosticIdPropertyName);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is IPropertySymbol { SetMethod: not null })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports one deprecation a caller cannot address on its own.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!ObsoleteAttributeFacts.IsObsoleteName(attribute.Name)
            || ObsoleteAttributeFacts.HasPropertyInitializer(attribute, ObsoleteAttributeFacts.DiagnosticIdPropertyName))
        {
            return;
        }

        // No message at all is SST2308's diagnostic, not this one: an attribute that explains nothing is a
        // bigger problem than an attribute that explains itself without an id, and one report is enough.
        if (ObsoleteAttributeFacts.HasNoUsableMessage(context.SemanticModel, attribute, context.CancellationToken))
        {
            return;
        }

        var target = ObsoleteAttributeFacts.GetAnnotatedName(attribute.Parent?.Parent);
        if (target.Length == 0
            || !ObsoleteAttributeFacts.IsFrameworkObsoleteAttribute(context.SemanticModel, attribute, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.ObsoleteWithoutDiagnosticId,
            attribute.GetLocation(),
            target));
    }
}
