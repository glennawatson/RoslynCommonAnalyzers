// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyStaticFieldWrite = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2402StaticFieldWrittenInConstructorAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2402 (an instance constructor writing to a static field).</summary>
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
}
