// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared per-tree settings cache.</summary>
public sealed class TreeOptionsCacheUnitTest
{
    /// <summary>The key the example settings read.</summary>
    private const string Key = "stylesharp.example";

    /// <summary>The value configured under <see cref="Key"/>.</summary>
    private const string ConfiguredValue = "configured";

    /// <summary>One read, for the tree seen first.</summary>
    private const int OneRead = 1;

    /// <summary>Two reads, one for each tree.</summary>
    private const int TwoReads = 2;

    /// <summary>Verifies a tree's settings are read on first demand and served from the cache afterwards.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsOnceThenServesTheCachedSettingsAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");
        var provider = new CountingOptionsProvider();
        var options = new AnalyzerOptions([], provider);
        var cache = new ConcurrentDictionary<SyntaxTree, ExampleOptions>();

        var first = TreeOptionsCache.GetOrRead(cache, tree, options);
        var second = TreeOptionsCache.GetOrRead(cache, tree, options);

        await Assert.That(first.Value).IsEqualTo(ConfiguredValue);
        await Assert.That(second).IsEqualTo(first);
        await Assert.That(provider.TreeReads).IsEqualTo(OneRead);
    }

    /// <summary>Verifies each tree is read and cached on its own.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EachTreeIsReadOnItsOwnAsync()
    {
        var provider = new CountingOptionsProvider();
        var options = new AnalyzerOptions([], provider);
        var cache = new ConcurrentDictionary<SyntaxTree, ExampleOptions>();

        _ = TreeOptionsCache.GetOrRead(cache, CSharpSyntaxTree.ParseText("class A { }"), options);
        _ = TreeOptionsCache.GetOrRead(cache, CSharpSyntaxTree.ParseText("class B { }"), options);

        await Assert.That(provider.TreeReads).IsEqualTo(TwoReads);
        await Assert.That(cache.Count).IsEqualTo(TwoReads);
    }

    /// <summary>Example settings that capture the configured value.</summary>
    /// <param name="Value">The value under <see cref="Key"/>, or <see langword="null"/> when unset.</param>
    private readonly record struct ExampleOptions(string? Value) : ITreeOptions<ExampleOptions>
    {
        /// <inheritdoc/>
        ExampleOptions ITreeOptions<ExampleOptions>.ReadFrom(AnalyzerConfigOptions options) =>
            new(options.TryGetValue(Key, out var value) ? value : null);
    }

    /// <summary>Options that hold one configured key, counting how often a tree's options are requested.</summary>
    private sealed class CountingOptions : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            value = key == Key ? ConfiguredValue : null!;
            return key == Key;
        }
    }

    /// <summary>A provider that counts per-tree option requests.</summary>
    private sealed class CountingOptionsProvider : AnalyzerConfigOptionsProvider
    {
        /// <summary>The options every tree receives.</summary>
        private readonly CountingOptions _options = new();

        /// <summary>Gets how many times a tree's options were requested.</summary>
        public int TreeReads { get; private set; }

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GlobalOptions => _options;

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            TreeReads++;
            return _options;
        }

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }
}
