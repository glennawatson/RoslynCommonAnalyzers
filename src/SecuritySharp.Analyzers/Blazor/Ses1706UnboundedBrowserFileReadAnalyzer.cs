// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Blazor <c>IBrowserFile.OpenReadStream</c> call whose <c>maxAllowedSize</c> is unbounded or
/// client-derived (SES1706). The rule reports the <c>maxAllowedSize</c> argument when the invoked method
/// binds to <c>Microsoft.AspNetCore.Components.Forms.IBrowserFile</c> (or a type implementing it) and the
/// argument is <c>long.MaxValue</c> (no cap), <c>IBrowserFile.Size</c> (the browser-reported, untrusted
/// file length), or a compile-time constant above the configured threshold
/// (<c>securitysharp.SES1706.max_bytes</c>, falling back to <c>securitysharp.max_bytes</c>; default
/// 10 MB). With no real cap a single upload can fill the server's memory or disk and take the process down
/// (CWE-770). The no-argument <c>OpenReadStream()</c> keeps the safe ~500 KB default and is never reported,
/// and a bounded constant at or below the threshold, or any other non-constant size, is left alone. The
/// whole rule is gated on <c>IBrowserFile</c> resolving, so a non-Blazor project registers nothing and pays
/// nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1706UnboundedBrowserFileReadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default byte ceiling (10 MB) a constant <c>maxAllowedSize</c> may reach before it is reported.</summary>
    internal const long DefaultMaxBytes = 10L * 1024 * 1024;

    /// <summary>The metadata name of the uploaded-file abstraction the rule gates on.</summary>
    private const string BrowserFileMetadataName = "Microsoft.AspNetCore.Components.Forms.IBrowserFile";

    /// <summary>The name of the stream-opening method whose size limit is guarded.</summary>
    private const string OpenReadStreamMethodName = "OpenReadStream";

    /// <summary>The name of the size-limit parameter on every <c>OpenReadStream</c> overload.</summary>
    private const string MaxAllowedSizeParameterName = "maxAllowedSize";

    /// <summary>The zero-based position of the size-limit parameter on every <c>OpenReadStream</c> overload.</summary>
    private const int MaxAllowedSizePosition = 0;

    /// <summary>The name of the client-reported file-length property, which is untrusted client metadata.</summary>
    private const string SizePropertyName = "Size";

    /// <summary>The message argument used when the size limit is the unbounded <c>long.MaxValue</c>.</summary>
    private const string UnboundedDisplay = "long.MaxValue";

    /// <summary>The message argument used when the size limit is the client-reported file length.</summary>
    private const string ClientSizeDisplay = "the client-reported file size";

    /// <summary>The suffix appended to a constant byte count in the diagnostic message.</summary>
    private const string ByteCountSuffix = " bytes";

    /// <summary>The rule-specific byte-ceiling key.</summary>
    private const string MaxBytesRuleKey = "securitysharp.SES1706.max_bytes";

    /// <summary>The project-wide byte-ceiling key.</summary>
    private const string MaxBytesGeneralKey = "securitysharp.max_bytes";

    /// <summary>The smallest ceiling that means anything: a ceiling below 1 would flag every positive size.</summary>
    private const long SmallestCeiling = 1;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.UnboundedBrowserFileRead);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var browserFile = start.Compilation.GetTypeByMetadataName(BrowserFileMetadataName);
            if (browserFile is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, browserFile), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1706 for an <c>OpenReadStream</c> call whose size limit is unbounded or client-derived.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="browserFile">The resolved <c>IBrowserFile</c> type the rule gates on.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol browserFile)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to 'OpenReadStream' with an explicit argument. The no-argument
        // overload keeps the safe ~500 KB default, so it is never a candidate.
        if (invocation.ArgumentList.Arguments.Count == 0
            || !string.Equals(BlazorInvocation.GetInvokedName(invocation.Expression), OpenReadStreamMethodName, StringComparison.Ordinal))
        {
            return;
        }

        // The size limit is defaulted (and therefore safe) unless an explicit 'maxAllowedSize' argument is present.
        if (BlazorInvocation.GetArgument(invocation.ArgumentList, MaxAllowedSizeParameterName, MaxAllowedSizePosition) is not { } sizeArgument)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: OpenReadStreamMethodName } method
            || !IsOrImplements(method.ContainingType, browserFile))
        {
            return;
        }

        var display = ClassifyUnsafeSize(context, sizeArgument, browserFile);
        if (display is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnboundedBrowserFileRead,
            sizeArgument.SyntaxTree,
            sizeArgument.Span,
            display));
    }

    /// <summary>Returns a message display for an unsafe size limit, or <see langword="null"/> when the limit is safe.</summary>
    /// <param name="context">The syntax node analysis context, used to read the byte-ceiling option.</param>
    /// <param name="sizeArgument">The <c>maxAllowedSize</c> argument expression.</param>
    /// <param name="browserFile">The resolved <c>IBrowserFile</c> type used to recognise its <c>Size</c> property.</param>
    /// <returns>The message display for the offending size, or <see langword="null"/> when the size is bounded and server-chosen.</returns>
    private static string? ClassifyUnsafeSize(SyntaxNodeAnalysisContext context, ExpressionSyntax sizeArgument, INamedTypeSymbol browserFile)
    {
        var constant = context.SemanticModel.GetConstantValue(sizeArgument, context.CancellationToken);
        if (constant.HasValue && TryGetLong(constant.Value, out var size))
        {
            if (size == long.MaxValue)
            {
                return UnboundedDisplay;
            }

            var ceiling = ReadMaxBytes(context.Options.AnalyzerConfigOptionsProvider.GetOptions(sizeArgument.SyntaxTree));
            return size > ceiling ? size.ToString(CultureInfo.InvariantCulture) + ByteCountSuffix : null;
        }

        // A non-constant size is unknowable except for the one shape that is always unsafe: the
        // client-reported 'IBrowserFile.Size', which lets the browser declare its own limit.
        return IsClientReportedSize(context, sizeArgument, browserFile) ? ClientSizeDisplay : null;
    }

    /// <summary>Returns whether the argument reads the client-reported <c>IBrowserFile.Size</c> property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="sizeArgument">The <c>maxAllowedSize</c> argument expression.</param>
    /// <param name="browserFile">The resolved <c>IBrowserFile</c> type.</param>
    /// <returns><see langword="true"/> when the argument binds to the <c>Size</c> property of an <c>IBrowserFile</c>.</returns>
    private static bool IsClientReportedSize(SyntaxNodeAnalysisContext context, ExpressionSyntax sizeArgument, INamedTypeSymbol browserFile)
        => context.SemanticModel.GetSymbolInfo(sizeArgument, context.CancellationToken).Symbol is IPropertySymbol { Name: SizePropertyName } property
            && IsOrImplements(property.ContainingType, browserFile);

    /// <summary>Reads the byte ceiling, preferring the rule-specific key over the project-wide key.</summary>
    /// <param name="options">The analyzer config options for the argument's tree.</param>
    /// <returns>The configured ceiling, or <see cref="DefaultMaxBytes"/> when neither key parses to a sensible value.</returns>
    private static long ReadMaxBytes(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(MaxBytesRuleKey, out var value)
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= SmallestCeiling)
        {
            return parsed;
        }

        return options.TryGetValue(MaxBytesGeneralKey, out value)
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
            && parsed >= SmallestCeiling
            ? parsed
            : DefaultMaxBytes;
    }

    /// <summary>Extracts a <see cref="long"/> from a constant <c>int</c> or <c>long</c> size value.</summary>
    /// <param name="value">The constant value.</param>
    /// <param name="size">The extracted size when the value is an integral constant.</param>
    /// <returns><see langword="true"/> when the value is an <c>int</c> or <c>long</c>.</returns>
    private static bool TryGetLong(object? value, out long size)
    {
        if (value is long l)
        {
            size = l;
            return true;
        }

        if (value is int i)
        {
            size = i;
            return true;
        }

        size = 0;
        return false;
    }

    /// <summary>Returns whether a type is, or implements, the gated <c>IBrowserFile</c> interface.</summary>
    /// <param name="type">The bound member's containing type.</param>
    /// <param name="browserFile">The resolved <c>IBrowserFile</c> type.</param>
    /// <returns><see langword="true"/> when the type is <c>IBrowserFile</c> or implements it.</returns>
    private static bool IsOrImplements(INamedTypeSymbol type, INamedTypeSymbol browserFile)
    {
        if (SymbolEqualityComparer.Default.Equals(type, browserFile))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], browserFile))
            {
                return true;
            }
        }

        return false;
    }
}
