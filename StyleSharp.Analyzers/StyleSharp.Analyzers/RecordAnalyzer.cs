// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Applies the record rules to a single record declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var record = (RecordDeclarationSyntax)context.Node;

        if (record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword))
        {
            CheckReadonlyStruct(context, record);
        }
        else
        {
            CheckSealedClass(context, record);
        }

        CheckPositionalParameters(context, record);
        CheckProperties(context, record);
    }

    /// <summary>Reports SST1803 when a record struct is not declared readonly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record struct declaration.</param>
    private static void CheckReadonlyStruct(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        if (record.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(RecordRules.ReadonlyRecordStruct, record.Identifier.GetLocation(), record.Identifier.ValueText));
    }

    /// <summary>Reports SST1800 when a record class is neither sealed nor abstract.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record class declaration.</param>
    private static void CheckSealedClass(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        if (record.Modifiers.Any(SyntaxKind.SealedKeyword) || record.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(RecordRules.SealRecordClass, record.Identifier.GetLocation(), record.Identifier.ValueText));
    }

    /// <summary>Reports SST1801 for positional parameters that do not match the configured casing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="record">The record declaration.</param>
    private static void CheckPositionalParameters(SyntaxNodeAnalysisContext context, RecordDeclarationSyntax record)
    {
        if (record.ParameterList is not { Parameters.Count: > 0 } list)
        {
            return;
        }

        var convention = NamingConventions.Read(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree),
            NamingConventions.RecordParameterSpecificKey,
            NamingConventions.RecordParameterGeneralKey,
            NamingConvention.PascalCase);

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
                && !property.Modifiers.Any(SyntaxKind.StaticKeyword))
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
                context.ReportDiagnostic(Diagnostic.Create(RecordRules.InitOnlyProperty, accessor.Keyword.GetLocation(), property.Identifier.ValueText));
                return;
            }
        }
    }
}
