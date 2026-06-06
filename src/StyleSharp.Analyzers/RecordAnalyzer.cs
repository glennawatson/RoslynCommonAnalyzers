// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports record-specific convention issues in a single pass over each
/// <c>record</c> / <c>record struct</c> declaration: a record class that is
/// neither sealed nor abstract (SST1800, opt-in), a positional parameter whose
/// casing does not match the configured convention (SST1801), a settable instance
/// property where <c>init</c> is expected (SST1802), and a record struct that is
/// not declared <c>readonly</c> (SST1803). Records are uncommon, so the registered
/// callback fires rarely; the configurable casing is read only when a declaration
/// actually has positional parameters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default naming convention for record positional parameters.</summary>
    private const NamingConvention DefaultParameterConvention = NamingConvention.PascalCase;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        RecordRules.SealRecordClass,
        RecordRules.PositionalParameterNaming,
        RecordRules.InitOnlyProperty,
        RecordRules.ReadonlyRecordStruct);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var parameterConventions = new ConcurrentDictionary<SyntaxTree, NamingConvention>();
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeRecordClass(nodeContext, parameterConventions), SyntaxKind.RecordDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeRecordStruct(nodeContext, parameterConventions), SyntaxKind.RecordStructDeclaration);
        });
    }

    /// <summary>Applies the record-class rules to a single record declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterConventions">The per-tree positional-parameter convention cache.</param>
    private static void AnalyzeRecordClass(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, NamingConvention> parameterConventions)
    {
        var record = (RecordDeclarationSyntax)context.Node;
        CheckSealedClass(context, record);
        CheckPositionalParameters(context, record, parameterConventions);
        CheckProperties(context, record);
    }

    /// <summary>Applies the record-struct rules to a single record-struct declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterConventions">The per-tree positional-parameter convention cache.</param>
    private static void AnalyzeRecordStruct(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, NamingConvention> parameterConventions)
    {
        var record = (RecordDeclarationSyntax)context.Node;
        CheckReadonlyStruct(context, record);
        CheckPositionalParameters(context, record, parameterConventions);
        CheckProperties(context, record);
    }

    /// <summary>Reports SST1803 when a record struct is not declared readonly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record struct declaration.</param>
    private static void CheckReadonlyStruct(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        if (ModifierListHelper.Contains(record.Modifiers, SyntaxKind.ReadOnlyKeyword))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(RecordRules.ReadonlyRecordStruct, record.SyntaxTree, record.Identifier.Span, record.Identifier.ValueText));
    }

    /// <summary>Reports SST1800 when a record class is neither sealed nor abstract.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record class declaration.</param>
    private static void CheckSealedClass(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        if (ModifierListHelper.ContainsEither(record.Modifiers, SyntaxKind.SealedKeyword, SyntaxKind.AbstractKeyword))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(RecordRules.SealRecordClass, record.SyntaxTree, record.Identifier.Span, record.Identifier.ValueText));
    }

    /// <summary>Reports SST1801 for positional parameters that do not match the configured casing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record declaration.</param>
    /// <param name="parameterConventions">The per-tree positional-parameter convention cache.</param>
    private static void CheckPositionalParameters(
        SyntaxNodeAnalysisContext context,
        RecordDeclarationSyntax record,
        ConcurrentDictionary<SyntaxTree, NamingConvention> parameterConventions)
    {
        if (record.ParameterList is not { Parameters.Count: > 0 } list)
        {
            return;
        }

        var convention = GetParameterConvention(context, parameterConventions, record.SyntaxTree);

        foreach (var parameter in list.Parameters)
        {
            var name = parameter.Identifier.ValueText;
            if (name.Length == 0 || NamingHelper.IsAllUnderscores(name) || NamingConventions.Conforms(name, convention))
            {
                continue;
            }

            NamingDiagnostic.Report(context, RecordRules.PositionalParameterNaming, parameter.Identifier, NamingConventions.Suggest(name, convention));
        }
    }

    /// <summary>Reports SST1802 for instance properties on the record that expose a set accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record declaration.</param>
    private static void CheckProperties(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        foreach (var member in record.Members)
        {
            if (member is PropertyDeclarationSyntax { AccessorList: { } accessors } property
                && !ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword))
            {
                ReportSetAccessor(context, property, accessors);
            }
        }
    }

    /// <summary>Reports SST1802 on the first set accessor in a property's accessor list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="accessors">The property's accessor list.</param>
    private static void ReportSetAccessor(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property, AccessorListSyntax accessors)
    {
        foreach (var accessor in accessors.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(RecordRules.InitOnlyProperty, accessor.SyntaxTree, accessor.Keyword.Span, property.Identifier.ValueText));
                return;
            }
        }
    }

    /// <summary>Resolves the positional-parameter naming convention for one syntax tree.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterConventions">The shared per-tree cache.</param>
    /// <param name="tree">The syntax tree being analyzed.</param>
    /// <returns>The applicable naming convention.</returns>
    private static NamingConvention GetParameterConvention(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, NamingConvention> parameterConventions,
        SyntaxTree tree)
    {
        if (parameterConventions.TryGetValue(tree, out var convention))
        {
            return convention;
        }

        convention = NamingConventions.Read(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree),
            NamingConventions.RecordParameterSpecificKey,
            NamingConventions.RecordParameterGeneralKey,
            DefaultParameterConvention);
        parameterConventions.TryAdd(tree, convention);
        return convention;
    }
}
