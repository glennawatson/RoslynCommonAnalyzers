// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a Windows Forms program entry point that declares no COM apartment state (SST2706): the
/// compilation's <c>Main</c> carries neither <c>[System.STAThread]</c> nor <c>[System.MTAThread]</c>. Windows
/// Forms relies on the single-threaded apartment for its COM-backed features — the clipboard, drag-and-drop,
/// and the common dialogs — and those misbehave at runtime when the entry-point thread starts in a
/// multithreaded apartment.
/// </summary>
/// <remarks>
/// The rule is gated at compilation start on <c>System.Windows.Forms.Application</c> and
/// <c>System.STAThreadAttribute</c> resolving, so a non-Windows-Forms project registers nothing, and the
/// suggested attribute is never offered against a target framework that lacks it. The entry point is resolved
/// once through <see cref="Compilation.GetEntryPoint(System.Threading.CancellationToken)"/>; when it already
/// declares an apartment attribute no syntax callback is registered at all. Otherwise a single
/// <c>MethodDeclaration</c> action fires, pre-filtered on the <c>Main</c> name before it binds, and reports the
/// one declaration whose symbol is the entry point.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2706StaThreadEntryPointAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the Windows Forms application type the rule gates on.</summary>
    private const string ApplicationMetadataName = "System.Windows.Forms.Application";

    /// <summary>The metadata name of the single-threaded apartment attribute the fix would add.</summary>
    private const string StaThreadMetadataName = "System.STAThreadAttribute";

    /// <summary>The metadata name of the multithreaded apartment attribute that also states an apartment.</summary>
    private const string MtaThreadMetadataName = "System.MTAThreadAttribute";

    /// <summary>The name a program entry point method always carries.</summary>
    private const string EntryPointName = "Main";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.StaThreadEntryPoint);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only for a Windows Forms compilation whose entry point states no apartment.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        if (compilation.GetTypeByMetadataName(ApplicationMetadataName) is null
            || compilation.GetTypeByMetadataName(StaThreadMetadataName) is not { } staThreadType)
        {
            return;
        }

        var entryPoint = compilation.GetEntryPoint(context.CancellationToken);
        if (entryPoint is null)
        {
            return;
        }

        var mtaThreadType = compilation.GetTypeByMetadataName(MtaThreadMetadataName);
        if (DeclaresApartment(entryPoint, staThreadType, mtaThreadType))
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, entryPoint), SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports the entry-point declaration that lacks an apartment attribute.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="entryPoint">The resolved entry-point symbol the rule reports.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, IMethodSymbol entryPoint)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Cheap name prefilter: only the entry point can be named 'Main', so nothing else is bound.
        if (!string.Equals(method.Identifier.ValueText, EntryPointName, StringComparison.Ordinal))
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        if (!SymbolEqualityComparer.Default.Equals(symbol, entryPoint))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            FrameworksRules.StaThreadEntryPoint,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }

    /// <summary>Returns whether the entry point already declares an STA or MTA apartment attribute.</summary>
    /// <param name="entryPoint">The entry-point symbol.</param>
    /// <param name="staThreadType">The resolved <c>System.STAThreadAttribute</c> type.</param>
    /// <param name="mtaThreadType">The resolved <c>System.MTAThreadAttribute</c> type, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when either apartment attribute is present.</returns>
    private static bool DeclaresApartment(IMethodSymbol entryPoint, INamedTypeSymbol staThreadType, INamedTypeSymbol? mtaThreadType)
    {
        var attributes = entryPoint.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(attributeClass, staThreadType)
                || (mtaThreadType is not null && SymbolEqualityComparer.Default.Equals(attributeClass, mtaThreadType)))
            {
                return true;
            }
        }

        return false;
    }
}
