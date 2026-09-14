// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the shape-based recognition of <c>System.ReadOnlySpan&lt;T&gt;</c>.</summary>
public class ReadOnlySpanTypeTests
{
    /// <summary>Verifies only the constructed system span is recognized, and its element type is returned.</summary>
    /// <param name="type">The parameter type text.</param>
    /// <param name="expectedElement">The expected element display, or an empty string when the type is not the span.</param>
    /// <param name="isCharSpan">Whether the type is a read-only span of <see cref="char"/>.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("ReadOnlySpan<char>", "char", true)]
    [Arguments("ReadOnlySpan<byte>", "byte", false)]
    [Arguments("System.ReadOnlySpan<int[]>", "int[]", false)]
    [Arguments("Span<char>", "", false)]
    [Arguments("char[]", "", false)]
    [Arguments("string", "", false)]
    [Arguments("N.ReadOnlySpan<char>", "", false)]
    public async Task OnlyTheSystemSpanIsRecognizedAsync(string type, string expectedElement, bool isCharSpan)
    {
        var tree = CSharpSyntaxTree.ParseText($"using System; class C {{ void M({type} value) {{ }} }} namespace N {{ struct ReadOnlySpan<T> {{ }} }}");
        var compilation = CSharpCompilation.Create(nameof(OnlyTheSystemSpanIsRecognizedAsync), [tree], RuntimeMetadataReferences.Platform);
        var parameter = (await tree.GetRootAsync()).DescendantNodes().OfType<ParameterSyntax>().Single();
        var symbol = compilation.GetSemanticModel(tree).GetTypeInfo(parameter.Type!).Type;

        await Assert.That(ReadOnlySpanType.TryGetElementType(symbol, out var element)).IsEqualTo(expectedElement.Length > 0);
        await Assert.That(element?.ToDisplayString() ?? string.Empty).IsEqualTo(expectedElement);
        await Assert.That(ReadOnlySpanType.IsSpanOf(symbol, SpecialType.System_Char)).IsEqualTo(isCharSpan);
    }

    /// <summary>Verifies a missing type is not a span.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingTypeIsNotASpanAsync()
    {
        await Assert.That(ReadOnlySpanType.TryGetElementType(null, out var element)).IsFalse();
        await Assert.That(element).IsNull();
        await Assert.That(ReadOnlySpanType.IsSpanOf(null, SpecialType.System_Char)).IsFalse();
    }
}
