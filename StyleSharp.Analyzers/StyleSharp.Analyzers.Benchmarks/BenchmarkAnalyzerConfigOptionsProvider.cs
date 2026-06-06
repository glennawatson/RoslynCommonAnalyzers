// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Simple dictionary-backed analyzer config provider for benchmark scenarios.</summary>
internal sealed class BenchmarkAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

    private readonly AnalyzerConfigOptions _globalOptions;

    /// <summary>Initializes a new instance of the <see cref="BenchmarkAnalyzerConfigOptionsProvider"/> class.</summary>
    /// <param name="globalOptions">The global analyzer-config key/value pairs.</param>
    public BenchmarkAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
        => _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;

    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
            => _values = values;

        public override bool TryGetValue(string key, out string value)
            => _values.TryGetValue(key, out value!);
    }
}
