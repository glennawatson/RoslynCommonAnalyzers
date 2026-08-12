// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a publicly visible class, struct, or record that has state worth showing and carries no
/// <c>[DebuggerDisplay]</c> attribute (SST2334). In the debugger such a type shows as its bare type name, so a
/// collection of instances is opaque until each is expanded; a display string surfaces the one or two members
/// that identify an instance.
/// </summary>
/// <remarks>
/// <para>
/// This is an opinionated, heavy nudge that would otherwise fire on nearly every public type, so it is
/// disabled by default and opt-in through <c>.editorconfig</c>. The rule resolves the attribute type in the
/// compilation and stays silent when it is absent, so the fix it offers always has something to add.
/// </para>
/// <para>
/// A type with nothing to display is left alone, because there the attribute cannot say more than the debugger
/// already does: a display string built for an empty type can only name <c>ToString()</c>, and a type that does
/// not override it renders as the type name either way. So the rule asks for one declared instance field or
/// property — of any accessibility, since a display string is evaluated in the type's own context and may name a
/// private field — or a <c>ToString()</c> override, which is what makes that fallback mean something. Statics
/// and constants identify no instance, and compiler-generated members are not the author's state, so neither
/// counts: a marker class, a class of constants, and an empty positional record all stay clean.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2334MissingDebuggerDisplayAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the debugger-display attribute the rule and its fix depend on.</summary>
    private const string DebuggerDisplayMetadataName = "System.Diagnostics.DebuggerDisplayAttribute";

    /// <summary>The name of the method whose override makes a <c>ToString</c>-based display string meaningful.</summary>
    private const string ToStringName = "ToString";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.MissingDebuggerDisplay);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DebuggerDisplayMetadataName) is not { } attribute)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports a publicly visible type with no debugger-display attribute.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attributeType">The resolved debugger-display attribute type.</param>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || type.IsStatic
            || !SymbolVisibility.IsExternallyVisible(type)
            || type.Locations.Length == 0
            || !type.Locations[0].IsInSource
            || HasDebuggerDisplay(type, attributeType)
            || !HasDisplayableState(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DesignRules.MissingDebuggerDisplay, type.Locations[0], type.Name));
    }

    /// <summary>Returns whether a type has anything a display string could name.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type declares instance state, or overrides <c>ToString</c>.</returns>
    /// <remarks>
    /// Accessibility is not part of the test: a display string is evaluated in the type's own context, so
    /// <c>[DebuggerDisplay("{_value}")]</c> over a private field is as valid as one naming a public property.
    /// What is excluded is everything that cannot describe an instance — statics and constants — and everything
    /// the author did not write: a record's <c>EqualityContract</c> and synthesized <c>ToString</c>, and an
    /// auto-property's backing field, are all implicitly declared and none of them makes a type worth a display
    /// string on its own.
    /// <para>
    /// An overriding property is excluded for the same reason. It answers a base class's contract rather than
    /// describing this instance, and it is usually the same for every instance of the type — a behavioural class
    /// whose only property is an override has nothing to put in a watch window, and naming that property is
    /// exactly the useless suggestion this gate exists to prevent. An overridden <c>ToString</c> is the one
    /// exception, and it qualifies on its own terms: it is what the fallback display string calls.
    /// </para>
    /// </remarks>
    private static bool HasDisplayableState(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.IsImplicitlyDeclared || member.IsStatic)
            {
                continue;
            }

            if (member is IFieldSymbol
                or IPropertySymbol { IsIndexer: false, IsOverride: false, GetMethod: not null }
                or IMethodSymbol { IsOverride: true, Name: ToStringName, Parameters.Length: 0 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type already carries the debugger-display attribute.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="attributeType">The debugger-display attribute type.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    private static bool HasDebuggerDisplay(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        var attributes = type.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
