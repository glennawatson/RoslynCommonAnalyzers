// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace TraceFocus;

/// <summary>Entry point for the terminal-focused speedscope summarizer.</summary>
internal static class Program
{
    /// <summary>Parses command-line options, loads a speedscope file, and prints the filtered report.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private static int Main(string[] args)
    {
        if (Options.IsHelpRequested(args))
        {
            PrintUsage();
            return 0;
        }

        if (!Options.TryParse(args, out var options, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }

        try
        {
            var tracePath = options.ResolveInputPath();
            var document = LoadTrace(tracePath);
            var filters = new FrameFilters(options);
            filters.ResolveProfileKind(tracePath);
            var aggregation = TraceProfileProcessor.Process(document, filters);
            var report = new TraceFocusReport(tracePath, aggregation, filters, options);
            report.WriteTo(Console.Out);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Deserializes a speedscope JSON export from disk.</summary>
    /// <param name="tracePath">The speedscope file path.</param>
    /// <returns>The parsed speedscope document.</returns>
    private static SpeedscopeDocument LoadTrace(string tracePath)
    {
        using var stream = File.OpenRead(tracePath);
        return JsonSerializer.Deserialize<SpeedscopeDocument>(stream)
            ?? throw new InvalidOperationException($"Trace file '{tracePath}' did not contain a speedscope document.");
    }

    /// <summary>Prints command-line usage information for the tool.</summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/TraceFocus -- --pattern <substring>");
        Console.WriteLine("  dotnet run --project tools/TraceFocus -- --file <path-to-speedscope-json>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --pattern <substring>         Newest matching *.speedscope.json under BenchmarkDotNet.Artifacts.");
        Console.WriteLine("  --file <path>                 Explicit speedscope file path.");
        Console.WriteLine("  --artifacts <path>            Artifacts root for --pattern (default: BenchmarkDotNet.Artifacts).");
        Console.WriteLine("  --include <substring>         Keep only stacks touching a matching frame. Repeatable.");
        Console.WriteLine("  --exclude <substring>         Remove matching frames from output stacks. Repeatable.");
        Console.WriteLine("  --profile <auto|cpu|alloc>   Select default filter mode (default: auto). ");
        Console.WriteLine("  --top-frames <count>          Number of frame rows to print (default: 15).");
        Console.WriteLine("  --top-stacks <count>          Number of stack rows to print (default: 10).");
        Console.WriteLine("  --analyzer-only               Print shorter analyzer-only frame and stack views.");
        Console.WriteLine("  --json                        Emit the summary as JSON.");
        Console.WriteLine("  --no-default-includes         Disable the built-in StyleSharp analyzer include filter.");
        Console.WriteLine("  --no-default-excludes         Disable the built-in BenchmarkDotNet/Roslyn noise filter.");
        Console.WriteLine("  --help                        Show this help text.");
    }
}
