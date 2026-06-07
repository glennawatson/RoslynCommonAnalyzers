// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Describes one compilation plus any analyzer-config options needed to benchmark it.</summary>
/// <param name="Compilation">The compilation to analyze.</param>
/// <param name="OptionsProvider">Optional analyzer-config options for the compilation.</param>
public readonly record struct AnalyzerBenchmarkScenario(
    CSharpCompilation Compilation,
    AnalyzerConfigOptionsProvider? OptionsProvider = null);
