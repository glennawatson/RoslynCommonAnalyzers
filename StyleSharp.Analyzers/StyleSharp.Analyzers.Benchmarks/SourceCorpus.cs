// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Generates synthetic C# source that exercises the list-layout analyzers.</summary>
internal static class SourceCorpus
{
    /// <summary>
    /// Builds a compilation unit with <paramref name="types"/> classes, each mixing
    /// valid (all-on-one-line and each-on-its-own-line) and jagged parameter/argument
    /// lists so the analyzers hit both the no-diagnostic and diagnostic paths.
    /// </summary>
    /// <param name="types">Number of classes to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types)
    {
        var sb = new StringBuilder(types * 512);
        sb.AppendLine("using System;");
        sb.AppendLine("namespace Bench;");

        for (var i = 0; i < types; i++)
        {
            sb.Append("public class C").Append(i).AppendLine();
            sb.AppendLine("{");

            // Each-parameter-on-its-own-line declaration (valid).
            sb.Append("    public void Wrapped").Append(i).AppendLine("(");
            sb.AppendLine("        string a,");
            sb.AppendLine("        int b,");
            sb.AppendLine("        bool c,");
            sb.AppendLine("        long d)");
            sb.AppendLine("    {");

            // All-on-one-line invocation (valid).
            sb.AppendLine("        Sink(a, b, c, d);");

            // Jagged invocation (reported).
            sb.AppendLine("        Sink(a,");
            sb.AppendLine("            b, c, d);");

            sb.AppendLine("    }");

            // Single-line declaration (valid).
            sb.Append("    public void OneLine").Append(i).AppendLine("(string a, int b, bool c, long d) { }");
            sb.AppendLine("    private void Sink(string a, int b, bool c, long d) { }");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
