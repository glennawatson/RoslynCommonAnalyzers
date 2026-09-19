// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1440PrivateMemberUsageAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests private-member candidate selection and read/write classification.</summary>
public class Sst1440PrivateMemberUsageAnalyzerTests
{
    /// <summary>Verifies read and read/write expressions keep a private field.</summary>
    /// <param name="statement">The field usage.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("_value += 1;")]
    [Arguments("this._value += 1;")]
    [Arguments("++_value;")]
    [Arguments("--_value;")]
    [Arguments("_value++;")]
    [Arguments("_value--;")]
    [Arguments("_ = -_value;")]
    [Arguments("Read(_value);")]
    [Arguments("Read(in _value);")]
    [Arguments("Change(ref _value);")]
    [Arguments("_ = this._value;")]
    public Task ReadAndReadWriteUsesKeepFieldAsync(string statement) =>
        Verify.VerifyAnalyzerAsync($$"""
            class C
            {
                private int _value;
                public void M() { {{statement}} }
                public void Read(in int value) { }
                public void Change(ref int value) { }
            }
            """);

    /// <summary>Verifies out arguments and simple assignments are writes without reads.</summary>
    /// <param name="statement">The write-only field usage.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Set(out _value);")]
    [Arguments("this._value = 1;")]
    public Task WriteOnlyUsesReportUnreadFieldAsync(string statement) =>
        Verify.VerifyAnalyzerAsync($$"""
            class C
            {
                private int {|SST1441:_value|};
                public void M() { {{statement}} }
                public void Set(out int value) { value = 1; }
            }
            """);

    /// <summary>Verifies properties and every variable of an event or field declaration are tracked.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnusedPropertiesFieldsAndEventsAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            class C
            {
                private int {|SST1440:_first|}, {|SST1440:_second|};
                private int {|SST1440:Unused|} { get; set; }
                private int {|SST1441:Written|} { get; set; }
                private int Read { get; set; }
                private event System.Action {|SST1440:UnusedEvent|}, UsedEvent;
                public int M()
                {
                    Written = 1;
                    UsedEvent += () => { };
                    return Read;
                }
            }
            """);

    /// <summary>Verifies attributes, constants, and externally accessible declarations are excluded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IneligibleDeclarationsAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            class C
            {
                private const int Constant = 1;
                [System.Obsolete] private int _marked;
                [System.Obsolete] private int MarkedProperty { get; set; }
                [System.Obsolete] private void MarkedMethod() { }
                [System.Obsolete] private event System.Action MarkedEvent;
                public event System.Action PublicEvent;
                private protected int ProtectedField;
                public int PublicProperty { get; set; }
                int ImplicitField;
            }
            partial class Partial
            {
                private partial void Hook();
                [System.Runtime.InteropServices.DllImport("native")]
                private static extern void External();
            }
            partial class Partial
            {
                private partial void Hook() { }
                public void M() { }
            }
            """);

    /// <summary>Verifies overloads, shadowing, and self-references do not count as another member's use.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SymbolIdentityAndSelfReferencesControlUsageAsync() =>
        Verify.VerifyAnalyzerAsync("""
            class C
            {
                private int {|SST1440:_value|};
                private void Used() { }
                private void {|SST1440:Used|}(int value) { }
                private void {|SST1440:Recursive|}() { Recursive(); }
                public void M()
                {
                    int _value = 0;
                    _ = _value;
                    Used();
                    M();
                }
            }
            """);

    /// <summary>Verifies extension calls preserve the underlying private method.</summary>
    /// <param name="invocation">The extension method invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("input.Decorate()")]
    [Arguments("input?.Decorate()")]
    [Arguments("Decorate(input)")]
    public Task ExtensionInvocationKeepsMethodAsync(string invocation) =>
        Verify.VerifyAnalyzerAsync($$"""
            internal static class Demo
            {
                internal static string Use(string input) => {{invocation}};
                private static string Decorate(this string value) => $"<{value}>";
                private static string {|SST1440:Decorate|}(this string value, int count) => value;
            }
            """);

    /// <summary>Verifies constructed reduced methods preserve their generic declaration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericExtensionInvocationKeepsMethodAsync() =>
        Verify.VerifyAnalyzerAsync("""
            internal static class Demo
            {
                internal static int Use(int input) => input.Identity();
                private static T Identity<T>(this T value) => value;
            }
            """);

    /// <summary>Verifies TestCaseSource keeps its named source method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TestCaseSourceNameofKeepsMethodAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using NUnit.Framework;

            public class Demo
            {
                private static IEnumerable<int> CaseSource() => new[] { 1, 2, 3 };

                [TestCaseSource(nameof(CaseSource))]
                public void Case(int value) { }
            }

            namespace NUnit.Framework
            {
                [AttributeUsage(AttributeTargets.Method)]
                public sealed class TestCaseSourceAttribute : Attribute
                {
                    public TestCaseSourceAttribute(string name) { }
                }
            }
            """);

    /// <summary>Verifies a qualified nameof source reference keeps its provider.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QualifiedTestCaseSourceNameofKeepsMethodAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using NUnit.Framework;

            public class Demo
            {
                private static IEnumerable<int> QualifiedSource() => new[] { 1, 2, 3 };

                [TestCaseSource(nameof(Demo.QualifiedSource))]
                public void Case(int value) { }
            }

            namespace NUnit.Framework
            {
                [AttributeUsage(AttributeTargets.Method)]
                public sealed class TestCaseSourceAttribute : Attribute
                {
                    public TestCaseSourceAttribute(string name) { }
                }
            }
            """);

    /// <summary>Verifies every overload in a nameof source group is retained.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverloadedTestCaseSourceNameofKeepsMethodsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using NUnit.Framework;

            public class Demo
            {
                private static IEnumerable<int> OverloadedSource() => new[] { 1, 2, 3 };
                private static IEnumerable<int> OverloadedSource(int count) => new[] { count };

                [TestCaseSource(nameof(OverloadedSource))]
                public void Case(int value) { }
            }

            namespace NUnit.Framework
            {
                [AttributeUsage(AttributeTargets.Method)]
                public sealed class TestCaseSourceAttribute : Attribute
                {
                    public TestCaseSourceAttribute(string name) { }
                }
            }
            """);

    /// <summary>Verifies ValueSource keeps its named parameter source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValueSourceNameofKeepsMethodAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using NUnit.Framework;

            public class Demo
            {
                private static IEnumerable<int> ValueSource() => new[] { 1, 2, 3 };

                public void Value([ValueSource(nameof(ValueSource))] int value) { }
            }

            namespace NUnit.Framework
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class ValueSourceAttribute : Attribute
                {
                    public ValueSourceAttribute(string name) { }
                }
            }
            """);

    /// <summary>Verifies TestFixtureSource keeps its named fixture source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TestFixtureSourceNameofKeepsMethodAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using NUnit.Framework;

            [TestFixtureSource(nameof(FixtureSource))]
            public class Demo
            {
                private static IEnumerable<int> FixtureSource() => new[] { 1, 2, 3 };
            }

            namespace NUnit.Framework
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class TestFixtureSourceAttribute : Attribute
                {
                    public TestFixtureSourceAttribute(string name) { }
                }
            }
            """);

    /// <summary>Verifies an entry point is preserved while unrelated private members are still reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EntryPointDoesNotKeepUnrelatedPrivateMembersAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                class Program
                {
                    private static void Main() { }
                    private static void {|SST1440:Unused|}() { }
                }
                """,
        };
        test.TestState.OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies failed overload binding does not keep an otherwise unused method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedReferenceDoesNotKeepMethodAsync() =>
        new Verify.Test
        { TestCode = "class C { private void {|SST1440:Helper|}() { } public void M() { Helper(1); } }", CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies invalid accessibility combinations are not considered private candidates.</summary>
    /// <param name="accessibility">The conflicting accessibility modifiers.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("private internal")]
    [Arguments("private public")]
    public Task ConflictingAccessibilityIsCleanAsync(string accessibility) =>
        new Verify.Test { TestCode = $$"""class C { {{accessibility}} int _value; }""", CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);
}
