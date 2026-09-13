// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a Windows Forms program entry point that declares no COM apartment state (SST2706): the
/// compilation's <c>Main</c> carries neither <c>[System.STAThread]</c> nor <c>[System.MTAThread]</c>. Windows
/// Forms relies on the single-threaded apartment for its COM-backed features — the clipboard, drag-and-drop,
/// and the common dialogs — and those misbehave at runtime when the entry-point thread starts in a
/// multithreaded apartment.
/// </summary>
/// <remarks>
/// A <c>MethodDeclaration</c> action filters on the <c>Main</c> name before resolving
/// <c>System.Windows.Forms.Application</c> and <c>System.STAThreadAttribute</c>. The suggested attribute is
/// never offered against a target framework that lacks it. The entry point is resolved through
/// <see cref="Compilation.GetEntryPoint(CancellationToken)"/>, and only its declaration is reported when
/// it carries neither apartment attribute.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2706StaThreadEntryPointAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name a program entry point method always carries.</summary>
    private const string EntryPointName = "Main";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(FrameworksRules.StaThreadEntryPoint);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new ApartmentMarkers(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, markers), SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>Reports the entry-point declaration that lacks an apartment attribute.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's lazily resolved apartment types.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, ApartmentMarkers markers)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Cheap name prefilter: only a declaration named 'Main' can be reported.
        if (!string.Equals(method.Identifier.ValueText, EntryPointName, StringComparison.Ordinal))
        {
            return;
        }

        if (markers.GetStaThread() is not { } staThreadType)
        {
            return;
        }

        var entryPoint = context.Compilation.GetEntryPoint(context.CancellationToken);
        if (entryPoint is null)
        {
            return;
        }

        var mtaThreadType = markers.GetMtaThread();
        if (DeclaresApartment(entryPoint, staThreadType, mtaThreadType))
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

    /// <summary>Resolves apartment types on demand and caches missing framework types too.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    private sealed class ApartmentMarkers(Compilation compilation)
    {
        /// <summary>The metadata name of the Windows Forms application type the rule gates on.</summary>
        private const string ApplicationMetadataName = "System.Windows.Forms.Application";

        /// <summary>The metadata name of the single-threaded apartment attribute the fix would add.</summary>
        private const string StaThreadMetadataName = "System.STAThreadAttribute";

        /// <summary>The metadata name of the multithreaded apartment attribute that also states an apartment.</summary>
        private const string MtaThreadMetadataName = "System.MTAThreadAttribute";

        /// <summary>The gated STA result, or null before the first Main declaration.</summary>
        private INamedTypeSymbol?[]? _staThread;

        /// <summary>The MTA result, or null before an entry point needs its attributes checked.</summary>
        private INamedTypeSymbol?[]? _mtaThread;

        /// <summary>Gets the STA attribute only when the Windows Forms application type exists.</summary>
        /// <returns>The STA attribute type, or null when either required type is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetStaThread() => (_staThread ??= [ResolveStaThread(compilation)])[0];

        /// <summary>Gets the MTA attribute, resolving it on first demand.</summary>
        /// <returns>The MTA attribute type, or null when absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetMtaThread() => (_mtaThread ??= [compilation.GetTypeByMetadataName(MtaThreadMetadataName)])[0];

        /// <summary>Resolves the STA attribute after checking for Windows Forms.</summary>
        /// <param name="compilation">The compilation whose types are resolved.</param>
        /// <returns>The STA attribute type, or null when either required type is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static INamedTypeSymbol? ResolveStaThread(Compilation compilation) =>
            compilation.GetTypeByMetadataName(ApplicationMetadataName) is null
                ? null
                : compilation.GetTypeByMetadataName(StaThreadMetadataName);
    }
}
