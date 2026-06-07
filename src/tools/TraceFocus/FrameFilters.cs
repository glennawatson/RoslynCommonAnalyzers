// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace TraceFocus;

/// <summary>Active include and exclude filters applied while summarizing frames.</summary>
internal sealed class FrameFilters
{
    /// <summary>Substring that identifies frames in the StyleSharp analyzer codebase.</summary>
    private const string AnalyzerNamespace = "StyleSharp.Analyzers.";

    /// <summary>The default analyzer-visible include terms.</summary>
    private static readonly string[] DefaultIncludeTerms =
    [
        "StyleSharp.Analyzers!StyleSharp.Analyzers."
    ];

    /// <summary>The default infrastructure frames that are filtered from reports.</summary>
    private static readonly string[] DefaultExcludeTerms =
    [
        "(Non-Activities)",
        "Process64 dotnet",
        "Threads",
        "Thread (",
        "BenchmarkDotNet!",
        "CPU_TIME",
        "System.Private.CoreLib!Interop",
        "System.Private.CoreLib!System.Console",
        "System.Private.CoreLib!System.Diagnostics.Tracing",
        "System.Private.CoreLib!System.IO.",
        "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start",
        "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder",
        "System.Private.CoreLib!System.Runtime.CompilerServices.TaskAwaiter",
        "System.Private.CoreLib!System.Threading",
        "System.Private.CoreLib!System.Threading.",
        "System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder",
        "UNMANAGED_CODE_TIME",
        "Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers",
        "Microsoft.CodeAnalysis.Diagnostics.AnalyzerDriver",
        "Microsoft.CodeAnalysis.Diagnostics.AnalyzerExecutor",
        "Microsoft.CodeAnalysis.Diagnostics.CompilationEventQueue",
        "Microsoft.CodeAnalysis.Diagnostics.AsyncQueue"
    ];

    /// <summary>The default allocation-trace infrastructure frames to exclude.</summary>
    private static readonly string[] AllocationExcludeTerms =
    [
        "GC_TIME",
        "ALLOCATION",
        "AllocationTick",
        "MALLOC"
    ];

    /// <summary>Initializes a new instance of the <see cref="FrameFilters"/> class from command-line options.</summary>
    /// <param name="options">The parsed command-line options.</param>
    public FrameFilters(Options options)
    {
        EffectiveProfileKind = options.ProfileKind;

        if (options.UseDefaultIncludes)
        {
            IncludeTerms.AddRange(DefaultIncludeTerms);
        }

        IncludeTerms.AddRange(options.IncludeTerms);

        if (options.UseDefaultExcludes)
        {
            ExcludeTerms.AddRange(DefaultExcludeTerms);
        }

        if (EffectiveProfileKind == TraceProfileKind.Alloc)
        {
            ExcludeTerms.AddRange(AllocationExcludeTerms);
        }

        ExcludeTerms.AddRange(options.ExcludeTerms);
    }

    /// <summary>Gets the active exclude terms, including defaults when enabled.</summary>
    public List<string> ExcludeTerms { get; }

        = [];

    /// <summary>Gets the effective profile kind applied to default filtering.</summary>
    public TraceProfileKind EffectiveProfileKind { get; private set; }

    /// <summary>Gets the active include terms, including defaults when enabled.</summary>
    public List<string> IncludeTerms { get; }

        = [];

    /// <summary>Gets a value indicating whether at least one include term is active.</summary>
    public bool HasIncludeTerms => IncludeTerms.Count > 0;

    /// <summary>Returns whether a normalized or raw frame name is analyzer-owned.</summary>
    /// <param name="frameName">The frame name to test.</param>
    /// <returns><see langword="true"/> when the frame belongs to the StyleSharp analyzer codebase.</returns>
    public static bool IsAnalyzerFrame(string frameName)
        => frameName.Contains(AnalyzerNamespace, StringComparison.Ordinal);

    /// <summary>Updates the effective profile kind from a resolved trace path when auto mode was requested.</summary>
    /// <param name="tracePath">The resolved trace path.</param>
    public void ResolveProfileKind(string tracePath)
    {
        if (EffectiveProfileKind != TraceProfileKind.Auto)
        {
            return;
        }

        EffectiveProfileKind = tracePath.Contains("ProfiledAlloc", StringComparison.OrdinalIgnoreCase)
            ? TraceProfileKind.Alloc
            : TraceProfileKind.Cpu;

        if (EffectiveProfileKind != TraceProfileKind.Alloc)
        {
            return;
        }

        ExcludeTerms.AddRange(AllocationExcludeTerms);
    }

    /// <summary>Returns whether a frame name matches an active exclude term.</summary>
    /// <param name="frameName">The frame name to test.</param>
    /// <returns><see langword="true"/> when the frame should be excluded.</returns>
    public bool MatchesExclude(string frameName) => Matches(frameName, ExcludeTerms);

    /// <summary>Returns whether a frame name matches an active include term.</summary>
    /// <param name="frameName">The frame name to test.</param>
    /// <returns><see langword="true"/> when the frame should be treated as analyzer-visible.</returns>
    public bool MatchesInclude(string frameName) => Matches(frameName, IncludeTerms);

    /// <summary>Returns whether a frame name contains any of the supplied terms.</summary>
    /// <param name="frameName">The frame name to test.</param>
    /// <param name="terms">The substring terms to search for.</param>
    /// <returns><see langword="true"/> when a term matches.</returns>
    private static bool Matches(string frameName, List<string> terms)
    {
        for (var index = 0; index < terms.Count; index++)
        {
            if (frameName.Contains(terms[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
