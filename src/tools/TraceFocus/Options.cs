// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace TraceFocus;

/// <summary>Command-line options for the trace-focus terminal tool.</summary>
internal sealed class Options
{
    /// <summary>The default BenchmarkDotNet artifacts root used for pattern searches.</summary>
    private const string DefaultArtifactsPath = "BenchmarkDotNet.Artifacts";

    /// <summary>The default number of inclusive and leaf frame rows to print.</summary>
    private const int DefaultTopFrames = 15;

    /// <summary>The default number of stack rows to print.</summary>
    private const int DefaultTopStacks = 10;

    /// <summary>Initializes a new instance of the <see cref="Options"/> class with default values.</summary>
    private Options()
    {
        ArtifactsPath = DefaultArtifactsPath;
        TopFrames = DefaultTopFrames;
        TopStacks = DefaultTopStacks;
        UseDefaultIncludes = true;
        UseDefaultExcludes = true;
    }

    /// <summary>Gets the explicit input file path, when the caller passed <c>--file</c>.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Gets the substring used to locate the newest matching speedscope export.</summary>
    public string? Pattern { get; private set; }

    /// <summary>Gets the artifacts root used when resolving <c>--pattern</c>.</summary>
    public string ArtifactsPath { get; private set; }

    /// <summary>Gets a value indicating whether the shortened analyzer-only view is enabled.</summary>
    public bool AnalyzerOnly { get; private set; }

    /// <summary>Gets the number of frame rows printed in the report.</summary>
    public int TopFrames { get; private set; }

    /// <summary>Gets the number of stack rows printed in the report.</summary>
    public int TopStacks { get; private set; }

    /// <summary>Gets a value indicating whether the built-in include filters are enabled.</summary>
    public bool UseDefaultIncludes { get; private set; }

    /// <summary>Gets a value indicating whether the built-in exclude filters are enabled.</summary>
    public bool UseDefaultExcludes { get; private set; }

    /// <summary>Gets the requested output format.</summary>
    public TraceOutputFormat OutputFormat { get; private set; }

    /// <summary>Gets the requested profile kind for default filtering.</summary>
    public TraceProfileKind ProfileKind { get; private set; }

    /// <summary>Gets the caller-specified exclude terms.</summary>
    public List<string> ExcludeTerms { get; } = [];

    /// <summary>Gets the caller-specified include terms.</summary>
    public List<string> IncludeTerms { get; } = [];

    /// <summary>Returns whether the arguments request help output.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns><see langword="true"/> when help should be shown.</returns>
    public static bool IsHelpRequested(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] is "--help" or "-h")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Attempts to parse command-line arguments into an <see cref="Options"/> instance.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="options">The parsed options when successful.</param>
    /// <param name="errorMessage">The validation error when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds.</returns>
    public static bool TryParse(string[] args, out Options options, out string errorMessage)
    {
        options = new();
        errorMessage = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            if (TryApplyFlag(options, args[index]))
            {
                continue;
            }

            if (TryApplyValueOption(options, args, ref index, out errorMessage))
            {
                continue;
            }

            return false;
        }

        return Validate(options, out errorMessage);
    }

    /// <summary>Resolves the final speedscope input path from the configured file or pattern.</summary>
    /// <returns>The absolute input path.</returns>
    public string ResolveInputPath()
    {
        if (FilePath is { } filePath)
        {
            return Path.GetFullPath(filePath);
        }

        var artifactsPath = Path.GetFullPath(ArtifactsPath);
        return !Directory.Exists(artifactsPath)
            ? throw new DirectoryNotFoundException($"Artifacts directory '{artifactsPath}' does not exist.")
            : FindNewestMatch(artifactsPath, Pattern!);
    }

    /// <summary>Finds the newest matching speedscope export under the artifacts directory.</summary>
    /// <param name="artifactsPath">The artifacts directory to search.</param>
    /// <param name="pattern">The substring that must appear in the path.</param>
    /// <returns>The newest matching file path.</returns>
    private static string FindNewestMatch(string artifactsPath, string pattern)
    {
        string? bestPath = null;
        var bestWriteTime = DateTimeOffset.MinValue;

        foreach (var candidate in Directory.EnumerateFiles(artifactsPath, "*.speedscope.json", SearchOption.AllDirectories))
        {
            if (!candidate.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var writeTime = File.GetLastWriteTimeUtc(candidate);
            if (bestPath is not null && writeTime <= bestWriteTime)
            {
                continue;
            }

            bestPath = candidate;
            bestWriteTime = writeTime;
        }

        return bestPath ?? throw new FileNotFoundException($"No *.speedscope.json file under '{artifactsPath}' matched '{pattern}'.");
    }

    /// <summary>Applies flag-style options that do not consume an additional argument.</summary>
    /// <param name="options">The target options object.</param>
    /// <param name="argument">The current command-line argument.</param>
    /// <returns><see langword="true"/> when the flag was recognized.</returns>
    private static bool TryApplyFlag(Options options, string argument)
    {
        switch (argument)
        {
            case "--no-default-includes":
            {
                options.UseDefaultIncludes = false;
                return true;
            }

            case "--no-default-excludes":
            {
                options.UseDefaultExcludes = false;
                return true;
            }

            case "--analyzer-only":
            {
                options.AnalyzerOnly = true;
                return true;
            }

            case "--json":
            {
                options.OutputFormat = TraceOutputFormat.Json;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Applies an option that consumes the following command-line value.</summary>
    /// <param name="options">The target options object.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The current argument index, updated when a value is consumed.</param>
    /// <param name="errorMessage">The validation error when parsing fails.</param>
    /// <returns><see langword="true"/> when the option was recognized and applied.</returns>
    private static bool TryApplyValueOption(Options options, string[] args, ref int index, out string errorMessage)
    {
        var argument = args[index];
        if (!TryReadArgumentValue(args, ref index, out var value, out errorMessage))
        {
            return false;
        }

        switch (argument)
        {
            case "--file":
            {
                options.FilePath = value;
                return true;
            }

            case "--pattern":
            {
                options.Pattern = value;
                return true;
            }

            case "--artifacts":
            {
                options.ArtifactsPath = value;
                return true;
            }

            case "--include":
            {
                options.IncludeTerms.Add(value);
                return true;
            }

            case "--exclude":
            {
                options.ExcludeTerms.Add(value);
                return true;
            }

            case "--top-frames":
            {
                return TryAssignPositiveInt(value, argument, static (target, count) => target.TopFrames = count, options, out errorMessage);
            }

            case "--top-stacks":
            {
                return TryAssignPositiveInt(value, argument, static (target, count) => target.TopStacks = count, options, out errorMessage);
            }

            case "--profile":
            {
                return TryAssignProfileKind(value, options, out errorMessage);
            }

            default:
            {
                errorMessage = $"Unknown argument '{argument}'.";
                return false;
            }
        }
    }

    /// <summary>Parses and assigns a positive integer option value.</summary>
    /// <param name="value">The raw string value.</param>
    /// <param name="argument">The originating argument name.</param>
    /// <param name="assign">The assignment callback.</param>
    /// <param name="options">The target options object.</param>
    /// <param name="errorMessage">The validation error when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds.</returns>
    private static bool TryAssignPositiveInt(string value, string argument, Action<Options, int> assign, Options options, out string errorMessage)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue) || parsedValue <= 0)
        {
            errorMessage = $"Expected a positive integer for '{argument}', but got '{value}'.";
            return false;
        }

        assign(options, parsedValue);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>Parses and assigns the requested profile-kind option value.</summary>
    /// <param name="value">The raw profile-kind value.</param>
    /// <param name="options">The target options object.</param>
    /// <param name="errorMessage">The validation error when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds.</returns>
    private static bool TryAssignProfileKind(string value, Options options, out string errorMessage)
    {
        switch (value)
        {
            case "auto":
            {
                options.ProfileKind = TraceProfileKind.Auto;
                errorMessage = string.Empty;
                return true;
            }

            case "cpu":
            {
                options.ProfileKind = TraceProfileKind.Cpu;
                errorMessage = string.Empty;
                return true;
            }

            case "alloc":
            {
                options.ProfileKind = TraceProfileKind.Alloc;
                errorMessage = string.Empty;
                return true;
            }

            default:
            {
                errorMessage = $"Expected 'auto', 'cpu', or 'alloc' for '--profile', but got '{value}'.";
                return false;
            }
        }
    }

    /// <summary>Reads the next command-line value for an option.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The current argument index, updated to the consumed value.</param>
    /// <param name="value">The consumed value when successful.</param>
    /// <param name="errorMessage">The validation error when the value is missing.</param>
    /// <returns><see langword="true"/> when a value was available.</returns>
    private static bool TryReadArgumentValue(string[] args, ref int index, out string value, out string errorMessage)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            errorMessage = $"Missing value for '{args[index]}'.";
            return false;
        }

        index++;
        value = args[index];
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>Validates the final option set after parsing.</summary>
    /// <param name="options">The parsed options.</param>
    /// <param name="errorMessage">The validation error when the options are invalid.</param>
    /// <returns><see langword="true"/> when the options are valid.</returns>
    private static bool Validate(Options options, out string errorMessage)
    {
        if (options.FilePath is not null && options.Pattern is not null)
        {
            errorMessage = "Specify either --file or --pattern, not both.";
            return false;
        }

        if (options.FilePath is null && options.Pattern is null)
        {
            errorMessage = "Specify --file or --pattern.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
