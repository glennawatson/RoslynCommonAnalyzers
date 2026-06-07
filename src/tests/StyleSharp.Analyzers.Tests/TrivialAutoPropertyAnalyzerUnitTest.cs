// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TUnit.Assertions;

using VerifyAutoProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TrivialAutoPropertyAnalyzer,
    StyleSharp.Analyzers.TrivialAutoPropertyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1420 (use an auto-property for trivial accessors).</summary>
public class TrivialAutoPropertyAnalyzerUnitTest
{
    /// <summary>Verifies a trivial get/set property is converted to an auto-property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TrivialPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public int {|SST1420:Value|} { get => _value; set => _value = value; }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value { get; set; }
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies accessor logic and external field use prevent the diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonTrivialOrSharedFieldIsCleanAsync()
        => await VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value { get => _value; set => _value = value < 0 ? 0 : value; }

                public int Read() => _value;
            }
            """);

    /// <summary>Verifies the syntax prepass recognizes a trivial property that uses <c>this.</c> on both accessors.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SyntaxPrepassRecognizesThisQualifiedBackingFieldAsync()
    {
        var property = ParseProperty(
            "public class C { private int _value; public int Value { get => this._value; set => this._value = value; } }");

        var success = TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName);

        await Assert.That(success).IsTrue();
        await Assert.That(fieldName).IsEqualTo("_value");
    }

    /// <summary>Verifies the syntax prepass rejects accessors that do not consistently target the same field.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SyntaxPrepassRejectsMismatchedAccessorTargetsAsync()
    {
        var property = ParseProperty(
            "public class C { private int _a; private int _b; public int Value { get => _a; set => _b = value; } }");

        var success = TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out _);

        await Assert.That(success).IsFalse();
    }

    /// <summary>Parses the first property declaration from the supplied source.</summary>
    /// <param name="source">The source containing the property declaration.</param>
    /// <returns>The parsed property declaration.</returns>
    private static PropertyDeclarationSyntax ParseProperty(string source)
        => SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
}
