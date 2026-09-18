// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerifyAutoProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1420TrivialAutoPropertyAnalyzer,
    StyleSharp.Analyzers.Sst1420TrivialAutoPropertyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1420 (use an auto-property for trivial accessors).</summary>
public class TrivialAutoPropertyAnalyzerUnitTest
{
    /// <summary>The field name used by the accessor checks.</summary>
    private const string BackingFieldName = "_value";

    /// <summary>Verifies cached collection auto-properties satisfy the trivial-property rule.</summary>
    /// <param name="initialization">The snapshot storage and initialization.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public List<int> Items { get; } = Names.Select(static value => value * 2).ToList();")]
    [Arguments("public List<int> Items { get; } public C() => Items = Names.Select(static value => value * 2).ToList();")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CachedAutoPropertyIsCleanAsync(string initialization) =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;
            using System.Linq;
            public class C
            {
                private static readonly int[] Names = { 1, 2 };
                {{initialization}}
            }
            """);

    /// <summary>Verifies semantic accessor checks reject nontrivial bodies and different field symbols.</summary>
    /// <param name="propertyText">The property whose accessor semantics are inspected.</param>
    /// <param name="expected">Whether every accessor directly uses the selected backing field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int Value => _value;", true)]
    [Arguments("int Value => this._value;", true)]
    [Arguments("int Value => _other;", false)]
    [Arguments("int Value => 1;", false)]
    [Arguments("int Value { }", false)]
    [Arguments("int Value { get; set; }", false)]
    [Arguments("int Value { get => _value; set; }", false)]
    [Arguments("int Value { get { return _value; } set { _value = value; } }", true)]
    [Arguments("int Value { get => _value; init => _value = value; }", true)]
    [Arguments("int Value { get => _other; set => _value = value; }", false)]
    [Arguments("int Value { get => _value; set => _other = value; }", false)]
    [Arguments("int Value { get => _value; set => _value = 1; }", false)]
    [Arguments("int Value { get => _value; set => _value = _other; }", false)]
    [Arguments("int Value { get => _value; set { } }", false)]
    [Arguments("int Value { get => _value; set { M(); } }", false)]
    [Arguments("int Value { get => _value; set { M(); _value = value; } }", false)]
    [Arguments("int Value { get { M(); return _value; } }", false)]
    [Arguments("int Value { get { M(); } }", false)]
    [Arguments("int Value { get { return; } }", false)]
    [Arguments("int Value { get => _value; set { return; } }", false)]
    [Arguments("int Value { get => _value; set => this._value = value; }", true)]
    [Arguments("int Value { get => _value; set => other._value = value; }", true)]
    public async Task SemanticAccessorsMatchTheBackingFieldAsync(string propertyText, bool expected)
    {
        var (root, model) = SemanticModelFactory.Create($"class C {{ int _value; int _other; C other; void M() {{ }} {propertyText} }}");
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var variable = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First();
        var field = (IFieldSymbol)model.GetDeclaredSymbol(variable)!;
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(model, property, field, CancellationToken.None)).IsEqualTo(expected);
        var namedExpected = expected && !propertyText.Contains("other._value", StringComparison.Ordinal);
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(model, property, field, BackingFieldName, CancellationToken.None)).IsEqualTo(namedExpected);
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(model, property, field, "missing", CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies matching identifier text cannot substitute for the required field symbol.</summary>
    /// <param name="propertyText">The property referring to a different field from the supplied symbol.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int Value => _value;")]
    [Arguments("int Value { get => _value; }")]
    [Arguments("int Value { set => _value = value; }")]
    public async Task MatchingNameStillRequiresMatchingSymbolAsync(string propertyText)
    {
        var (root, model) = SemanticModelFactory.Create($"class C {{ int _other; int _value; {propertyText} }}");
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var variable = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First();
        var field = (IFieldSymbol)model.GetDeclaredSymbol(variable)!;
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(model, property, field, BackingFieldName, CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies escaped identifiers, block accessors, and nullable generic fields are recognized.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EscapedGenericBackingFieldIsReportedAsync() =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            #nullable enable
            class C<T> where T : class
            {
                private T? @event;
                public T? {|SST1420:Value|} { get { return this.@event; } set { this.@event = value; } }
            }
            """);

    /// <summary>Verifies incomplete and unsupported accessor shapes fail the syntax prepass.</summary>
    /// <param name="propertyText">The property syntax to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int Value { }")]
    [Arguments("int Value { get; set; }")]
    [Arguments("int Value { get => _value; set; }")]
    [Arguments("int Value { get { return; } }")]
    [Arguments("int Value { get => _value; set => _value = other; }")]
    [Arguments("int Value { get => _value; set { _value = 1; } }")]
    [Arguments("int Value { get => _value; set { M(); } }")]
    [Arguments("int Value { get => _value; set { M(); _value = value; } }")]
    [Arguments("int Value => other._value;")]
    [Arguments("int Value => this._value<int>;")]
    [Arguments("int Value => 1;")]
    public async Task UnsupportedPropertySyntaxIsRejectedAsync(string propertyText)
    {
        var property = ParseProperty($"class C {{ {propertyText} }}");
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out _)).IsFalse();
    }

    /// <summary>Verifies an event accessor accidentally supplied as a property accessor is rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnsupportedAccessorKindIsRejectedAsync()
    {
        var property = ParseProperty("class C { int Value { get => _value; } }");
        property = property.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(SyntaxFactory.AccessorDeclaration(SyntaxKind.AddAccessorDeclaration))));
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out _)).IsFalse();
    }

    /// <summary>Verifies init accessors share the same syntactic backing-field check as setters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SyntaxPrepassRecognizesInitAccessorAsync()
    {
        var property = ParseProperty("class C { int Value { get => _value; init => _value = value; } }");
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)).IsTrue();
        await Assert.That(fieldName).IsEqualTo(BackingFieldName);
    }

    /// <summary>Verifies missing identifier tokens yield an empty name for subsequent field resolution.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingIdentifierProducesEmptyFieldNameAsync()
    {
        var property = ParseProperty("class C { int Value => _value; }");
        property = property.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken))));
        await Assert.That(Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)).IsTrue();
        await Assert.That(fieldName).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies a type carrying a region is reported but keeps its backing field.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The backing field is deleted from the member list, so a directive among the members would lose the
    /// half that sits on it.
    /// </remarks>
    [Test]
    public async Task TypeCarryingADirectiveKeepsItsFieldAsync()
    {
        const string Source = """
            public class C
            {
            #region State
                private int _value;
            #endregion

                public int {|SST1420:Value|}
                {
                    get => _value;
                    set => _value = value;
                }
            }
            """;
        await VerifyAutoProperty.VerifyCodeFixAsync(Source, Source);
    }

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonTrivialOrSharedFieldIsCleanAsync() =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WriteOnlyPropertyIsCleanAsync() =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstBackingFieldIsCleanAsync() =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
            """
            public class C
            {
                private const int Limit = 5;

                public static int Value => Limit;
            }
            """);

    /// <summary>Verifies an instance property over a static field is left alone because storage would stop being shared.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstancePropertyOverStaticFieldIsCleanAsync() =>
        VerifyAutoProperty.VerifyAnalyzerAsync(
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
        await Assert.That(fieldName).IsEqualTo(BackingFieldName);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyDeclarationSyntax ParseProperty(string source) =>
        ((TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members.OfType<PropertyDeclarationSyntax>().Single();
}
