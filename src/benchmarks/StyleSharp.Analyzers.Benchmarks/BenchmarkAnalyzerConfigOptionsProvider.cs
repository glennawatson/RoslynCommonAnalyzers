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
    /// <summary>The empty analyzer-config options returned for file-scoped lookups.</summary>
    private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

    /// <summary>The analyzer-config options returned for syntax-tree lookups.</summary>
    private readonly AnalyzerConfigOptions _treeOptions;

    /// <summary>Initializes a new instance of the <see cref="BenchmarkAnalyzerConfigOptionsProvider"/> class.</summary>
    /// <param name="globalOptions">The global analyzer-config key/value pairs.</param>
    public BenchmarkAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
        : this(globalOptions, ImmutableDictionary<string, string>.Empty)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BenchmarkAnalyzerConfigOptionsProvider"/> class.</summary>
    /// <param name="globalOptions">The global analyzer-config key/value pairs.</param>
    /// <param name="treeOptions">The syntax-tree analyzer-config key/value pairs.</param>
    public BenchmarkAnalyzerConfigOptionsProvider(
        IReadOnlyDictionary<string, string> globalOptions,
        IReadOnlyDictionary<string, string> treeOptions)
    {
        GlobalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        _treeOptions = new DictionaryAnalyzerConfigOptions(treeOptions);
    }

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GlobalOptions { get; }

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _treeOptions;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;

    /// <summary>Dictionary-backed analyzer config options for one benchmark scenario.</summary>
    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        /// <summary>The stored analyzer-config values.</summary>
        private readonly IReadOnlyDictionary<string, string> _values;

        /// <summary>Initializes a new instance of the <see cref="DictionaryAnalyzerConfigOptions"/> class.</summary>
        /// <param name="values">The analyzer-config key/value pairs.</param>
        public DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
            => _values = values;

        /// <summary>Attempts to read an analyzer-config value by key.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The resolved value when present.</param>
        /// <returns><see langword="true"/> when the key was found; otherwise, <see langword="false"/>.</returns>
        public override bool TryGetValue(string key, out string value)
            => _values.TryGetValue(key, out value!);
    }
}
