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
    /// <summary>The highest ASCII character for the specialized PascalCase fast path.</summary>
    private const char LastAsciiChar = '\u007F';

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
            var parameterConventionCache = new ParameterConventionCache();
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeRecordClass(nodeContext, parameterConventionCache), SyntaxKind.RecordDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeRecordStruct(nodeContext, parameterConventionCache), SyntaxKind.RecordStructDeclaration);
        });
    }

    /// <summary>Returns whether a record parameter name matches the specialized PascalCase fast path.</summary>
    /// <param name="name">The candidate parameter name.</param>
    /// <returns><see langword="true"/> when the name begins with an upper-case letter.</returns>
    internal static bool IsPascalCaseFastPathCompliant(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        var first = name[0];
        return (uint)(first - 'A') <= ('Z' - 'A') || (first > LastAsciiChar && char.IsUpper(first));
    }

    /// <summary>Returns whether a positional parameter name should produce SST1801 for the given convention.</summary>
    /// <param name="name">The positional parameter name.</param>
    /// <param name="convention">The configured naming convention.</param>
    /// <returns><see langword="true"/> when the analyzer should report a naming diagnostic.</returns>
    internal static bool ShouldReportPositionalParameterNaming(string name, NamingConvention convention)
    {
        if (convention == NamingConvention.PascalCase && IsPascalCaseFastPathCompliant(name))
        {
            return false;
        }

        return name.Length != 0
               && !NamingHelper.IsAllUnderscores(name)
               && (convention == NamingConvention.PascalCase
                    ? !IsPascalCaseFastPathCompliant(name)
                    : !NamingConventions.Conforms(name, convention));
    }

    /// <summary>Returns the set accessor that should produce SST1802, or <see langword="null"/> for clean shapes.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <returns>The set accessor to report, or <see langword="null"/>.</returns>
    internal static AccessorDeclarationSyntax? TryGetSetAccessorToReport(SyntaxList<AccessorDeclarationSyntax> accessors)
    {
        const int MaxAccessors = 2;
        switch (accessors.Count)
        {
            case MaxAccessors:
                {
                    if (accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration) && accessors[1].IsKind(SyntaxKind.InitAccessorDeclaration))
                    {
                        return null;
                    }

                    if (accessors[0].IsKind(SyntaxKind.SetAccessorDeclaration))
                    {
                        return accessors[0];
                    }

                    return accessors[1].IsKind(SyntaxKind.SetAccessorDeclaration) ? accessors[1] : null;
                }

            case 1:
                return accessors[0].IsKind(SyntaxKind.SetAccessorDeclaration) ? accessors[0] : null;
        }

        for (var i = 0; i < accessors.Count; i++)
        {
            if (accessors[i].IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                return accessors[i];
            }
        }

        return null;
    }

    /// <summary>Applies the record-class rules to a single record declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterConventionCache">The per-tree positional-parameter convention cache.</param>
    private static void AnalyzeRecordClass(SyntaxNodeAnalysisContext context, ParameterConventionCache parameterConventionCache)
    {
        var record = (RecordDeclarationSyntax)context.Node;
        CheckSealedClass(context, record);
        CheckPositionalParameters(context, record, parameterConventionCache);
        CheckProperties(context, record);
    }

    /// <summary>Applies the record-struct rules to a single record-struct declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterConventionCache">The per-tree positional-parameter convention cache.</param>
    private static void AnalyzeRecordStruct(SyntaxNodeAnalysisContext context, ParameterConventionCache parameterConventionCache)
    {
        var record = (RecordDeclarationSyntax)context.Node;
        CheckReadonlyStruct(context, record);
        CheckPositionalParameters(context, record, parameterConventionCache);
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
    /// <param name="parameterConventionCache">The per-tree positional-parameter convention cache.</param>
    private static void CheckPositionalParameters(
        SyntaxNodeAnalysisContext context,
        RecordDeclarationSyntax record,
        ParameterConventionCache parameterConventionCache)
    {
        if (record.ParameterList is not { Parameters.Count: > 0 } list)
        {
            return;
        }

        var convention = parameterConventionCache.Get(context, record.SyntaxTree);

        var parameters = list.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var name = parameter.Identifier.ValueText;
            if (!ShouldReportPositionalParameterNaming(name, convention))
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
        for (var i = 0; i < record.Members.Count; i++)
        {
            if (record.Members[i] is not PropertyDeclarationSyntax { AccessorList: { } accessorList } property
                || ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword))
            {
                continue;
            }

            var setAccessor = TryGetSetAccessorToReport(accessorList.Accessors);
            if (setAccessor is null)
            {
                continue;
            }

            ReportSetAccessor(context, property, setAccessor);
        }
    }

    /// <summary>Reports SST1802 for one set accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="accessor">The set accessor to flag.</param>
    private static void ReportSetAccessor(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property,
        AccessorDeclarationSyntax accessor)
        => context.ReportDiagnostic(DiagnosticHelper.Create(RecordRules.InitOnlyProperty, accessor.SyntaxTree, accessor.Keyword.Span, property.Identifier.ValueText));

    /// <summary>Caches the most recent per-tree parameter convention for one compilation.</summary>
    private sealed class ParameterConventionCache
    {
        /// <summary>The most recently resolved cache entry.</summary>
        private CacheEntry? _last;

        /// <summary>Resolves the positional-parameter naming convention for one syntax tree.</summary>
        /// <param name="context">The syntax node analysis context.</param>
        /// <param name="tree">The syntax tree being analyzed.</param>
        /// <returns>The applicable naming convention.</returns>
        public NamingConvention Get(SyntaxNodeAnalysisContext context, SyntaxTree tree)
        {
            var entry = Volatile.Read(ref _last);
            if (ReferenceEquals(entry?.Tree, tree))
            {
                return entry.Convention;
            }

            var convention = NamingConventions.Read(
                context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree),
                NamingConventions.RecordParameterSpecificKey,
                NamingConventions.RecordParameterGeneralKey,
                DefaultParameterConvention);
            Volatile.Write(ref _last, new(tree, convention));
            return convention;
        }

        /// <summary>Represents one cached tree/convention pair.</summary>
        /// <param name="Tree">The syntax tree the convention was read from.</param>
        /// <param name="Convention">The cached convention value.</param>
        private sealed record CacheEntry(SyntaxTree Tree, NamingConvention Convention);
    }
}
