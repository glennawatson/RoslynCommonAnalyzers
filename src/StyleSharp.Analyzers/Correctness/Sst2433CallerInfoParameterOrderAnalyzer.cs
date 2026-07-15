// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a caller-info parameter that cannot do its job (SST2433): one followed by an ordinary parameter,
/// so callers pass the caller-info argument positionally and their value lands in it silently, or one with no
/// default, so every caller must pass it explicitly.
/// </summary>
/// <remarks>
/// The four caller-info attributes are resolved once at compilation start; the last of them,
/// <c>CallerArgumentExpressionAttribute</c>, arrived in .NET 6, so that clause only runs where the attribute
/// exists and the other three keep working when it does not. The analyzer binds nothing per keystroke: it is a
/// symbol action gated on a method that actually declares a parameter carrying an attribute, and reports on
/// the offending parameter.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2433CallerInfoParameterOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the caller-member-name attribute.</summary>
    private const string CallerMemberNameMetadataName = "System.Runtime.CompilerServices.CallerMemberNameAttribute";

    /// <summary>The metadata name of the caller-file-path attribute.</summary>
    private const string CallerFilePathMetadataName = "System.Runtime.CompilerServices.CallerFilePathAttribute";

    /// <summary>The metadata name of the caller-line-number attribute.</summary>
    private const string CallerLineNumberMetadataName = "System.Runtime.CompilerServices.CallerLineNumberAttribute";

    /// <summary>The metadata name of the caller-argument-expression attribute (.NET 6+).</summary>
    private const string CallerArgumentExpressionMetadataName = "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.CallerInfoParameterOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var attributes = CallerInfoAttributeSet.Resolve(start.Compilation);
            if (attributes is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attributes), SymbolKind.Method);
        });
    }

    /// <summary>Analyzes one method's parameter list for misplaced caller-info parameters.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attributes">The compilation's caller-info attribute symbols.</param>
    private static void Analyze(SymbolAnalysisContext context, CallerInfoAttributeSet attributes)
    {
        var method = (IMethodSymbol)context.Symbol;
        var parameters = method.Parameters;
        if (parameters.Length == 0 || !AnyParameterHasAttributes(parameters))
        {
            return;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (!attributes.IsCallerInfo(parameter))
            {
                continue;
            }

            if (FindFollowingOrdinaryParameter(parameters, attributes, i) is { } follower)
            {
                Report(context, parameter, "must come last, but is followed by '" + follower.Name + "'");
                continue;
            }

            if (!parameter.IsOptional)
            {
                Report(context, parameter, "has no default value, so every call site must pass it explicitly");
            }
        }
    }

    /// <summary>Reports one caller-info parameter defect.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="parameter">The offending parameter.</param>
    /// <param name="clause">The message clause describing the defect.</param>
    private static void Report(SymbolAnalysisContext context, IParameterSymbol parameter, string clause)
    {
        var locations = parameter.Locations;
        if (locations.Length == 0)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CallerInfoParameterOrder, locations[0], clause));
    }

    /// <summary>Finds the first parameter after <paramref name="index"/> that is not itself caller-info.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="attributes">The compilation's caller-info attribute symbols.</param>
    /// <param name="index">The caller-info parameter's position.</param>
    /// <returns>The first following ordinary parameter, or <see langword="null"/> when only caller-info parameters follow.</returns>
    private static IParameterSymbol? FindFollowingOrdinaryParameter(
        ImmutableArray<IParameterSymbol> parameters,
        CallerInfoAttributeSet attributes,
        int index)
    {
        for (var j = index + 1; j < parameters.Length; j++)
        {
            if (!attributes.IsCallerInfo(parameters[j]))
            {
                return parameters[j];
            }
        }

        return null;
    }

    /// <summary>Returns whether any parameter carries an attribute at all, the cheap gate before classification.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when at least one parameter has an attribute.</returns>
    private static bool AnyParameterHasAttributes(ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].GetAttributes().Length != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The compilation's resolved caller-info attribute symbols.</summary>
    private sealed class CallerInfoAttributeSet
    {
        /// <summary>The resolved caller-info attribute symbols; the array is never empty.</summary>
        private readonly INamedTypeSymbol[] _attributes;

        /// <summary>Initializes a new instance of the <see cref="CallerInfoAttributeSet"/> class.</summary>
        /// <param name="attributes">The resolved caller-info attribute symbols.</param>
        private CallerInfoAttributeSet(INamedTypeSymbol[] attributes) => _attributes = attributes;

        /// <summary>Resolves the caller-info attributes present in a compilation.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <returns>The resolved set, or <see langword="null"/> when none of the attributes exist.</returns>
        public static CallerInfoAttributeSet? Resolve(Compilation compilation)
        {
            var buffer = new INamedTypeSymbol[4];
            var count = 0;
            Add(compilation, CallerMemberNameMetadataName, buffer, ref count);
            Add(compilation, CallerFilePathMetadataName, buffer, ref count);
            Add(compilation, CallerLineNumberMetadataName, buffer, ref count);
            Add(compilation, CallerArgumentExpressionMetadataName, buffer, ref count);
            if (count == 0)
            {
                return null;
            }

            var resolved = new INamedTypeSymbol[count];
            Array.Copy(buffer, resolved, count);
            return new CallerInfoAttributeSet(resolved);
        }

        /// <summary>Returns whether a parameter carries one of the resolved caller-info attributes.</summary>
        /// <param name="parameter">The parameter to classify.</param>
        /// <returns><see langword="true"/> when the parameter is a caller-info parameter.</returns>
        public bool IsCallerInfo(IParameterSymbol parameter)
        {
            var parameterAttributes = parameter.GetAttributes();
            for (var i = 0; i < parameterAttributes.Length; i++)
            {
                if (IsCallerInfoAttribute(parameterAttributes[i].AttributeClass))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Appends a resolvable attribute symbol to the buffer.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <param name="metadataName">The attribute's metadata name.</param>
        /// <param name="buffer">The buffer receiving resolved symbols.</param>
        /// <param name="count">The running count of resolved symbols.</param>
        private static void Add(Compilation compilation, string metadataName, INamedTypeSymbol[] buffer, ref int count)
        {
            if (compilation.GetTypeByMetadataName(metadataName) is not { } symbol)
            {
                return;
            }

            buffer[count++] = symbol;
        }

        /// <summary>Returns whether an attribute class is one of the resolved caller-info attributes.</summary>
        /// <param name="attributeClass">The bound attribute class, if any.</param>
        /// <returns><see langword="true"/> when the attribute is caller-info.</returns>
        private bool IsCallerInfoAttribute(INamedTypeSymbol? attributeClass)
        {
            for (var i = 0; i < _attributes.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributeClass, _attributes[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
