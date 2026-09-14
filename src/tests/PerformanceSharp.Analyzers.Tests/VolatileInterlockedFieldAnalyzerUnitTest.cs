// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1307VolatileInterlockedFieldAnalyzer,
    PerformanceSharp.Analyzers.Psh1307VolatileInterlockedFieldCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1307VolatileInterlockedFieldAnalyzer"/> (PSH1307 volatile interlocked fields).</summary>
public class VolatileInterlockedFieldAnalyzerUnitTest
{
    /// <summary>Verifies a field declared volatile is not reported, since its plain accesses already are.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The <c>volatile</c> modifier gives every plain read and write of the field the acquire/release
    /// semantics this rule asks <c>Volatile.Read</c>/<c>Volatile.Write</c> to supply, so there is nothing
    /// left to change and the suggestion would be noise. Reported as issue #51.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VolatileFieldIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public struct RefCount
            {
                private volatile int _count;

                public int Increment()
                {
                    for (var current = _count; ;)
                    {
                        var newCount = current + 1;
                        var oldCount = Interlocked.CompareExchange(ref _count, newCount, current);
                        if (oldCount == current)
                        {
                            return newCount;
                        }

                        current = oldCount;
                    }
                }
            }
            """);

    /// <summary>Verifies a plain read of an interlocked field is flagged and wrapped in Volatile.Read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainReadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  private int _count;

                                  public void Add() => Interlocked.Increment(ref _count);

                                  public int Count => {|PSH1307:_count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class C
                                   {
                                       private int _count;

                                       public void Add() => Interlocked.Increment(ref _count);

                                       public int Count => Volatile.Read(ref _count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies fields declared in another partial declaration retain their diagnostic and fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldInOtherPartialDeclarationIsFlaggedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public partial class C
                              {
                                  private int _count;
                              }

                              public partial class C
                              {
                                  public void Add() => Interlocked.Increment(ref _count);

                                  public int Count => {|PSH1307:_count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public partial class C
                                   {
                                       private int _count;
                                   }

                                   public partial class C
                                   {
                                       public void Add() => Interlocked.Increment(ref _count);

                                       public int Count => Volatile.Read(ref _count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a plain write of an interlocked field is flagged and wrapped in Volatile.Write.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainWriteIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  private long _total;

                                  public void Add(long value) => Interlocked.Add(ref _total, value);

                                  public void Reset()
                                  {
                                      {|PSH1307:_total|} = 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class C
                                   {
                                       private long _total;

                                       public void Add(long value) => Interlocked.Add(ref _total, value);

                                       public void Reset()
                                       {
                                           Volatile.Write(ref _total, 0);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies constructor initialization stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorWriteIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private int _count;

                public C(int seed)
                {
                    _count = seed;
                }

                public void Add() => Interlocked.Increment(ref _count);
            }
            """);

    /// <summary>Verifies fields never touched by Interlocked stay clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedFieldIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private int _count;
                private int _plain;

                public void Add() => Interlocked.Increment(ref _count);

                public int Plain
                {
                    get => _plain;
                    set => _plain = value;
                }
            }
            """);

    /// <summary>Verifies a read through a readonly member stays clean — the Volatile fix can't take a ref there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyMemberReadIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public struct C
            {
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public readonly int Snapshot() => _count;

                public readonly int Count => _count;
            }
            """);

    /// <summary>Verifies a read through a whole-property readonly block accessor stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyPropertyBlockAccessorIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public struct C
            {
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public readonly int Count
                {
                    get => _count;
                }
            }
            """);

    /// <summary>Verifies the reported false positive — readonly equality/hash members reading an interlocked field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyEqualityMembersAreCleanAsync() =>
        VerifyAsync(
            """
            #nullable enable
            using System;
            using System.Threading;

            public struct Cell : IEquatable<Cell>
            {
                private object? _value;

                public void Publish(object v) => Interlocked.CompareExchange(ref _value, v, null);

                public readonly bool Equals(Cell other) => ReferenceEquals(_value, other._value);

                public override readonly int GetHashCode() => _value?.GetHashCode() ?? 0;

                public override readonly bool Equals(object? obj) => obj is Cell c && Equals(c);
            }
            """);

    /// <summary>Verifies a non-readonly struct member is still flagged and fixed — the guard is readonly-only.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonReadonlyStructMemberIsFlaggedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public struct C
                              {
                                  private int _count;

                                  public void Add() => Interlocked.Increment(ref _count);

                                  public int Snapshot() => {|PSH1307:_count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public struct C
                                   {
                                       private int _count;

                                       public void Add() => Interlocked.Increment(ref _count);

                                       public int Snapshot() => Volatile.Read(ref _count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies accesses inside a lock stay clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LockedAccessIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private readonly object _gate = new object();
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public int Drain()
                {
                    lock (_gate)
                    {
                        return _count;
                    }
                }
            }
            """);

    /// <summary>Verifies target-name discovery rejects calls without a direct ref-field argument.</summary>
    /// <param name="expression">The invocation syntax.</param>
    /// <param name="expected">The expected target name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Interlocked.MemoryBarrier()", null)]
    [Arguments("M(ref field)", null)]
    [Arguments("Other.Exchange(ref field, 1)", null)]
    [Arguments("System.Threading.Interlocked.Exchange(ref field, 1)", null)]
    [Arguments("Interlocked.Exchange(field, 1)", null)]
    [Arguments("Interlocked.Exchange(ref array[0], 1)", null)]
    [Arguments("Interlocked.Exchange(ref this.field, 1)", "field")]
    [Arguments("Interlocked.Exchange(ref field, 1)", "field")]
    public async Task TargetNameRequiresSupportedRefArgumentAsync(string expression, string? expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        await Assert.That(Psh1307VolatileInterlockedFieldAnalyzer.TryGetInterlockedTargetName(invocation)).IsEqualTo(expected);
    }

    /// <summary>Verifies increment, decrement, and unary read accesses are classified correctly.</summary>
    /// <param name="expression">The field use.</param>
    /// <param name="write">Whether the field is being written.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("++field", true)]
    [Arguments("--field", true)]
    [Arguments("field++", true)]
    [Arguments("field--", true)]
    [Arguments("-field", false)]
    [Arguments("field = 1", true)]
    [Arguments("other = field", false)]
    public async Task UnaryAccessClassificationMatchesMutationAsync(string expression, bool write)
    {
        var root = SyntaxFactory.ParseExpression(expression);
        var usage = root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(static name => name.Identifier.ValueText == "field");
        await Assert.That(Psh1307VolatileInterlockedFieldAnalyzer.IsWriteAccess(usage)).IsEqualTo(write);
    }

    /// <summary>Verifies non-integer primitive and reference overloads are reportable.</summary>
    /// <param name="type">The interlocked field type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("float")]
    [Arguments("double")]
    [Arguments("System.IntPtr")]
    [Arguments("System.UIntPtr")]
    [Arguments("object")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task VolatileOverloadTypesReportPlainReadsAsync(string type) =>
        VerifyAsync($$"""
            using System.Threading;
            class C
            {
                {{type}} field;
                void Update({{type}} value) { Interlocked.Exchange(ref this.field, value); Interlocked.Exchange(ref field, value); }
                {{type}} Read() => {|PSH1307:field|};
            }
            """);

    /// <summary>Verifies readonly accessors and readonly types keep writable refs away from instance fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReadonlyAccessorAndStaticReadonlyStructFieldAreDistinguishedAsync() =>
        VerifyAsync("""
            using System.Threading;
            struct C
            {
                int field;
                void Update() => Interlocked.Increment(ref field);
                int P { readonly get => this.field; set => Interlocked.Exchange(ref this.field, value); }
            }
            readonly struct D
            {
                static int field;
                static void Update() => Interlocked.Increment(ref field);
                int Read() => {|PSH1307:field|};
            }
            """);

    /// <summary>Verifies semantic field checks reject readonly and volatile fields from another partial declaration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PartialFieldsRetainReadonlyAndVolatileSemanticsAsync() =>
        VerifyAsync("""
            using System.Threading;
            partial struct C { int field; }
            partial struct C
            {
                void Update() => Interlocked.Increment(ref field);
                readonly int Read() => field;
            }
            partial class D { volatile int field; }
            partial class D
            {
                void Update() => Interlocked.Increment(ref field);
                int Read() => field;
            }
            """);

    /// <summary>Verifies field-shaped names must bind to this type's nonvolatile field.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ShadowedAndForeignFieldsAreIgnoredAsync() =>
        VerifyAsync("""
            using System.Threading;
            class Base { protected int field; }
            partial class C : Base
            {
                void Update() => Interlocked.Increment(ref field);
                int Read() => field;
            }
            class D
            {
                int field;
                void Update() => Interlocked.Increment(ref field);
                int Read(int field) => field;
                string Name() => nameof(field);
                int ReadOther(D other) => other.field;
                class Nested { int field; int Read() => field; }
            }
            class Fake { public void Increment(ref int field) { } }
            class E
            {
                Fake Interlocked = new Fake();
                int field;
                void Update() => Interlocked.Increment(ref field);
                int Read() => field;
            }
            """);

    /// <summary>Verifies the volatile overload whitelist handles primitive, generic, and unsupported value fields.</summary>
    /// <param name="type">The field type.</param>
    /// <param name="report">Whether a volatile accessor exists for that type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("bool", true)]
    [Arguments("byte", true)]
    [Arguments("sbyte", true)]
    [Arguments("short", true)]
    [Arguments("ushort", true)]
    [Arguments("int", true)]
    [Arguments("uint", true)]
    [Arguments("long", true)]
    [Arguments("ulong", true)]
    [Arguments("decimal", false)]
    [Arguments("char", false)]
    [Arguments("T", false)]
    [Arguments("Value", false)]
    public async Task FieldTypeMustHaveVolatileOverloadAsync(string type, bool report)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            using System.Threading;
            namespace System.Threading
            {
                public static class Interlocked
                {
                    public static void Exchange<T>(ref T field, T value) { field = value; }
                }
            }
            struct Value { }
            class C<T> where T : struct
            {
                {{type}} field;
                void Update({{type}} value) => Interlocked.Exchange(ref field, value);
                {{type}} Read() => field;
            }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new Psh1307VolatileInterlockedFieldAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(report ? 1 : 0);
        if (!report)
        {
            return;
        }

        await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1307");
        await Assert.That((await tree.GetTextAsync()).ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("field");
    }

    /// <summary>Verifies argument and initializer contexts distinguish construction from later reads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadContextsHonorFieldInitializerExclusionAsync()
    {
        var test = new CSharpAnalyzerVerifier<Psh1307VolatileInterlockedFieldAnalyzer>.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using System.Threading;
                class Box { public Box(int value) { } }
                class C
                {
                    static int _field;
                    static int snapshot = _field;
                    static int Snapshot { get; } = {|PSH1307:_field|};
                    static void Update() => Interlocked.Increment(ref _field);
                    int Index(int[] values) => values[{|PSH1307:_field|}];
                    object Create() => new Box({|PSH1307:_field|});
                    int Call() => System.Math.Abs({|PSH1307:_field|});
                    int Invoke() => Copy({|PSH1307:_field|});
                    int Local() { int value = {|PSH1307:_field|}; return value; }
                    static int Copy(int value) => value;
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent when either threading marker is unavailable.</summary>
    /// <param name="declareInterlocked">Whether the Interlocked marker exists.</param>
    /// <param name="declareVolatile">Whether the Volatile marker exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task MissingThreadingMarkerDisablesAnalysisAsync(bool declareInterlocked, bool declareVolatile)
    {
        var interlocked = declareInterlocked ? "public class Interlocked { public static void Increment(ref int field) { } }" : string.Empty;
        var volatileType = declareVolatile ? "public class Volatile { }" : string.Empty;
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace System { public class Object { } public struct Void { } public struct Int32 { } }
            namespace System.Threading { {{interlocked}} {{volatileType}} }
            class C { int field; void Update() => Interlocked.Increment(ref field); int Read() => field; }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Psh1307VolatileInterlockedFieldAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
