// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonlyField = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1424FieldShouldBeReadonlyAnalyzer,
    StyleSharp.Analyzers.Sst1424FieldShouldBeReadonlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1424 (make never-reassigned fields readonly).</summary>
public class FieldShouldBeReadonlyAnalyzerUnitTest
{
    /// <summary>Verifies a constructor-only assignment is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorOnlyAssignmentIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int {|SST1424:_value|};

                                  public C(int value) => _value = value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;
                                   }
                                   """;
        await VerifyReadonlyField.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method assignment prevents the diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodAssignmentIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public void Set(int value) => _value = value;
            }
            """);

    /// <summary>Verifies several constructor-only fields in one type are each reported independently.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultipleConstructorOnlyFieldsAreEachReportedAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1424:_a|};
                private int {|SST1424:_b|};
                private int {|SST1424:_c|};

                public C(int value)
                {
                    _a = value;
                    _b = value;
                    _c = value;
                }
            }
            """);

    /// <summary>Verifies a same-named local written in another method does not block the report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameNameLocalWriteInOtherMethodDoesNotBlockReportAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1424:_value|};

                public C(int value) => _value = value;

                public int Other()
                {
                    int _value = 3;
                    _value = 5;
                    return _value;
                }
            }
            """);

    /// <summary>Verifies a write inside a constructor lambda counts as outside the constructor and is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WriteInsideConstructorLambdaIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private int _value;

                public C()
                {
                    Action set = () => _value = 5;
                    set();
                }
            }
            """);

    /// <summary>Verifies a mutable struct field with a non-readonly method invoked is not reported (readonly would mutate a copy).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MutableStructFieldWithMutatingMethodIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public void Increment() => _value++;

                public void Set(int value) => _value = value;

                public readonly int Value => _value;
            }

            public sealed class Holder
            {
                private Counter _counter;

                public Holder(Counter counter) => _counter = counter;

                public void Bump() => _counter.Increment();

                public int Read() => _counter.Value;
            }
            """);

    /// <summary>Verifies a mutable struct field passed by ref is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MutableStructFieldPassedByRefIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public void Increment() => _value++;

                public void Set(int value) => _value = value;
            }

            public sealed class Holder
            {
                private Counter _counter;

                public Holder(Counter counter) => _counter = counter;

                public void Bump() => Mutate(ref _counter);

                private static void Mutate(ref Counter counter) => counter.Increment();
            }
            """);

    /// <summary>Verifies a mutable struct field whose non-readonly property setter is used is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MutableStructFieldWithSetterUsedIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public struct Box
            {
                public int Value { get; set; }
            }

            public sealed class Holder
            {
                private Box _box;

                public Holder(Box box) => _box = box;

                public void Bump() => _box.Value = 5;
            }
            """);

    /// <summary>Verifies a readonly-struct field assigned only in the constructor is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ReadonlyStructFieldStillReportedAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public readonly struct Token
            {
                private readonly int _id;

                public Token(int id) => _id = id;

                public int Describe() => _id;
            }

            public sealed class Holder
            {
                private Token {|SST1424:_token|};

                public Holder(Token token) => _token = token;

                public int Read() => _token.Describe();
            }
            """);

    /// <summary>Verifies a reference-type field assigned only in the constructor is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ReferenceTypeFieldStillReportedAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public sealed class Holder
            {
                private string {|SST1424:_name|};

                public Holder(string name) => _name = name;

                public int Length() => _name.Length;
            }
            """);

    /// <summary>Verifies a value-type field only read through a readonly getter is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueTypeFieldReadThroughReadonlyGetterStillReportedAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public void Increment() => _value++;

                public readonly int Value => _value;
            }

            public sealed class Holder
            {
                private Counter {|SST1424:_counter|};

                public Holder(Counter counter) => _counter = counter;

                public int Read() => _counter.Value;
            }
            """);
}
