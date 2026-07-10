// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerifyAutoProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1420TrivialAutoPropertyAnalyzer,
    StyleSharp.Analyzers.Sst1420TrivialAutoPropertyCodeFixProvider>;

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

    /// <summary>Verifies a backing-field initializer moves onto the generated auto-property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldInitializerMovesToAutoPropertyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value = 5;

                                  public int {|SST1420:Value|} { get => _value; set => _value = value; }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value { get; set; } = 5;
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a write-only property is left alone because an auto-property requires a getter.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WriteOnlyPropertyIsCleanAsync()
        => await VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value { set => _value = value; }
            }
            """);

    /// <summary>Verifies an expression-bodied property over a single-use field is converted to a get-only auto-property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpressionBodiedPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly int _value = 5;

                                  public int {|SST1420:Value|} => _value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value { get; } = 5;
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static property over a static single-use field is converted to a static auto-property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static int _value;

                                  public static int {|SST1420:Value|} { get => _value; set => _value = value; }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public static int Value { get; set; }
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static expression-bodied property keeps its initializer and its static modifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticExpressionBodiedPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static readonly int _value = 5;

                                  public static int {|SST1420:Value|} => _value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public static int Value { get; } = 5;
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>this.</c>-qualified expression-bodied property is converted.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisQualifiedExpressionBodiedPropertyIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly int _value;

                                  public int {|SST1420:Value|} => this._value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value { get; }
                                   }
                                   """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a const backing field is left alone because it is compile-time state, not storage.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstBackingFieldIsCleanAsync()
        => await VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private const int Limit = 5;

                public static int Value => Limit;
            }
            """);

    /// <summary>Verifies an instance property over a static field is left alone because storage would stop being shared.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InstancePropertyOverStaticFieldIsCleanAsync()
        => await VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static int _value;

                public int Value { get => _value; set => _value = value; }
            }
            """);

    /// <summary>Verifies the syntax prepass recognizes a trivial property that uses <c>this.</c> on both accessors.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SyntaxPrepassRecognizesThisQualifiedBackingFieldAsync()
    {
        var property = ParseProperty(
            "public class C { private int _value; public int Value { get => this._value; set => this._value = value; } }");

        var success = Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName);

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

        var success = Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out _);

        await Assert.That(success).IsFalse();
    }

    /// <summary>Parses the first property declaration from the supplied source.</summary>
    /// <param name="source">The source containing the property declaration.</param>
    /// <returns>The parsed property declaration.</returns>
    private static PropertyDeclarationSyntax ParseProperty(string source)
        => ((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members.OfType<PropertyDeclarationSyntax>().Single();
}
