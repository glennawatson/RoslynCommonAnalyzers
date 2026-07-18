// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyStaticFieldWrite = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2402StaticFieldWrittenInConstructorAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2402 (an instance member writing to a static field).</summary>
public class StaticFieldWrittenInConstructorAnalyzerUnitTest
{
    /// <summary>Verifies an instance constructor overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public C(int value) => {|SST2402:Latest|} = value;
            }
            """);

    /// <summary>Verifies the write is found wherever in the constructor it hides.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static string Name = string.Empty;

                public C(string name)
                {
                    foreach (var part in name.Split(','))
                    {
                        {|SST2402:Name|} = part;
                    }
                }
            }
            """);

    /// <summary>Verifies a qualified write to the type's own static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public C(int value) => {|SST2402:C.Latest|} = value;
            }
            """);

    /// <summary>Verifies a static constructor is the right place to set static state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticConstructorIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                static C() => Latest = 1;
            }
            """);

    /// <summary>Verifies writing the object's own instance state is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceFieldWriteIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly int _value;

                public C(int value) => _value = value;

                public int Value => _value;
            }
            """);

    /// <summary>Verifies a first-one-wins lazy initializer is clean, in both of the shapes it is written.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LazyInitializationGuardIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            #nullable enable

            public sealed class C
            {
                public static C? Instance;

                public static C? Fallback;

                public C(bool primary)
                {
                    if (Instance is null)
                    {
                        Instance = this;
                    }

                    Fallback ??= this;
                    Primary = primary;
                }

                public bool Primary { get; }
            }
            """);

    /// <summary>Verifies an accumulating counter is clean: it adds to the field rather than redefining it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceCounterIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Created;

                public C()
                {
                    Created++;
                    Created += 1;
                }
            }
            """);

    /// <summary>Verifies per-thread state is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadStaticFieldIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                [ThreadStatic]
                public static int Current;

                public C(int value) => Current = value;
            }
            """);

    /// <summary>Verifies a write that only runs when a delegate is invoked is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteInsideLambdaIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public static int Latest;

                public C(int value) => Reset = () => Latest = value;

                public Action Reset { get; }
            }
            """);

    /// <summary>Verifies a local that shadows nothing but shares a static field's name is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedLocalIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class Other
            {
                public static int Latest;
            }

            public sealed class C
            {
                public C()
                {
                    var latest = 0;
                    latest = 1;
                    Value = latest;
                }

                public int Value { get; }
            }
            """);

    /// <summary>Verifies an instance method overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceMethodStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static string Name = string.Empty;

                public void Rename(string name)
                {
                    {|SST2402:Name|} = name;
                }
            }
            """);

    /// <summary>Verifies an expression-bodied instance method overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedMethodStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public void Update(int value) => {|SST2402:Latest|} = value;
            }
            """);

    /// <summary>Verifies a property setter overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertySetterStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static string Endpoint = string.Empty;

                public string Address
                {
                    get => Endpoint;
                    set => {|SST2402:Endpoint|} = value;
                }
            }
            """);

    /// <summary>Verifies an expression-bodied getter overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedPropertyStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Reads;

                public int Value => {|SST2402:Reads|} = Reads + 1;
            }
            """);

    /// <summary>Verifies an indexer setter overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexerSetterStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public int this[int index]
                {
                    get => index;
                    set => {|SST2402:Latest|} = value;
                }
            }
            """);

    /// <summary>Verifies an expression-bodied indexer overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedIndexerStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public int this[int index] => {|SST2402:Latest|} = index;
            }
            """);

    /// <summary>Verifies event accessors overwriting a static field are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventAccessorStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public static int Subscribers;

                private Action? _handlers;

                public event Action Changed
                {
                    add
                    {
                        _handlers += value;
                        {|SST2402:Subscribers|} = 1;
                    }
                    remove
                    {
                        _handlers -= value;
                        {|SST2402:Subscribers|} = 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a finalizer overwriting a static field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DestructorStaticFieldWriteIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            #nullable enable

            public sealed class C
            {
                public static C? LastCollected;

                ~C() => {|SST2402:LastCollected|} = this;
            }
            """);

    /// <summary>Verifies a write inside a lambda created by an instance method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteInsideLambdaInMethodIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public static int Latest;

                public Action MakeReset(int value) => () => {|SST2402:Latest|} = value;
            }
            """);

    /// <summary>Verifies a write inside a local function declared in an instance method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteInsideLocalFunctionInMethodIsReportedAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public void Update(int value)
                {
                    Apply();

                    void Apply() => {|SST2402:Latest|} = value;
                }
            }
            """);

    /// <summary>Verifies a static method writing a static field is clean: static state belongs to static code.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticMethodStaticFieldWriteIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Latest;

                public static void Update(int value) => Latest = value;
            }
            """);

    /// <summary>Verifies a static property setter writing a static field is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticPropertySetterStaticFieldWriteIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static string Endpoint = string.Empty;

                public static string Address
                {
                    get => Endpoint;
                    set => Endpoint = value;
                }
            }
            """);

    /// <summary>Verifies an instance method writing the object's own instance state is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceMethodInstanceFieldWriteIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void Update(int value) => _value = value;

                public int Value => _value;
            }
            """);

    /// <summary>Verifies a first-one-wins lazy initializer in an instance method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LazyInitializationGuardInMethodIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            #nullable enable

            public sealed class C
            {
                public static C? Instance;

                public static C? Fallback;

                public C Activate()
                {
                    if (Instance is null)
                    {
                        Instance = this;
                    }

                    Fallback ??= this;
                    return this;
                }
            }
            """);

    /// <summary>Verifies an accumulating counter in an instance method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceCounterInMethodIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static int Operations;

                public void Track()
                {
                    Operations++;
                    Operations += 1;
                }
            }
            """);

    /// <summary>Verifies per-thread state written from an instance method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadStaticWriteInMethodIsCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                [ThreadStatic]
                public static int Current;

                public void Enter(int value) => Current = value;
            }
            """);

    /// <summary>Verifies members without bodies are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodylessMembersAreCleanAsync()
        => await VerifyStaticFieldWrite.VerifyAnalyzerAsync(
            """
            public interface IWorker
            {
                int Count { get; set; }

                void Work();
            }

            public abstract class Worker
            {
                public static int Total;

                public abstract void Run();
            }
            """);
}
