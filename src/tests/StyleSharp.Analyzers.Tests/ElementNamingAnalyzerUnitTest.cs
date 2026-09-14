// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1300ElementNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1300 (types and members should be PascalCase).</summary>
public class ElementNamingAnalyzerUnitTest
{
    /// <summary>Verifies a PascalCase class produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidClassAsync() => Verify.VerifyAnalyzerAsync("public class Widget { }");

    /// <summary>Verifies a lower-case class is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassAsync() =>
        Verify.VerifyCodeFixAsync("public class {|SST1300:widget|} { }", "public class Widget { }");

    /// <summary>Verifies a lower-case method is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { public void {|SST1300:doThing|}() { } }",
            "public class C { public void DoThing() { } }");

    /// <summary>Verifies a lower-case property is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyAsync() =>
        Verify.VerifyCodeFixAsync(
            "public class C { public int {|SST1300:myProp|} { get; set; } }",
            "public class C { public int MyProp { get; set; } }");

    /// <summary>Verifies a lower-case enum member is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumMemberAsync() =>
        Verify.VerifyCodeFixAsync("public enum E { {|SST1300:valueOne|} }", "public enum E { ValueOne }");

    /// <summary>Verifies every type declaration kind checks the first letter of its name.</summary>
    /// <param name="declaration">The declaration with a lower-case identifier.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public struct {|SST1300:widget|} { }")]
    [Arguments("public record {|SST1300:widget|};")]
    [Arguments("public record struct {|SST1300:widget|};")]
    [Arguments("public enum {|SST1300:widget|} { Value }")]
    [Arguments("public delegate void {|SST1300:widget|}();")]
    public Task LowerCaseTypeDeclarationIsReportedAsync(string declaration) => Verify.VerifyAnalyzerAsync(declaration);

    /// <summary>Verifies event fields check each variable and custom events check their identifier.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LowerCaseEventsAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            public class C
            {
                public event System.Action {|SST1300:first|}, Second, {|SST1300:third|};
                public event System.Action {|SST1300:changed|} { add { } remove { } }
            }
            """);

    /// <summary>Verifies PascalCase and discard names are accepted across declaration kinds.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PascalCaseAndUnderscoreNamesAreSilentAsync() =>
        Verify.VerifyAnalyzerAsync("""
            public struct Widget { }
            public record Record;
            public record struct RecordStruct;
            public delegate void Callback();
            public enum Choice { First, __ }
            public class _
            {
                public void __() { }
                public int Value { get; set; }
                public event System.Action Changed, ___;
                public event System.Action Updated { add { } remove { } }
            }
            """);

    /// <summary>Verifies inherited method, property and custom-event names are not checked again.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverridesAndExplicitImplementationsAreSilentAsync() =>
        Verify.VerifyAnalyzerAsync("""
            public interface IContract
            {
                void {|SST1300:run|}();
                int {|SST1300:value|} { get; }
                event System.Action {|SST1300:changed|};
            }
            public abstract class Base
            {
                public abstract void {|SST1300:run|}();
                public abstract int {|SST1300:value|} { get; }
                public abstract event System.Action {|SST1300:changed|};
            }
            public class Derived : Base, IContract
            {
                public override void run() { }
                public override int value => 0;
                public override event System.Action changed { add { } remove { } }
                void IContract.run() { }
                int IContract.value => 0;
                event System.Action IContract.changed { add { } remove { } }
            }
            """);

    /// <summary>Verifies verbatim identifiers are checked using their unescaped name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VerbatimIdentifiersUseTheirValueTextAsync() =>
        Verify.VerifyAnalyzerAsync("public class @Widget { public void {|SST1300:@event|}() { } }");

    /// <summary>Verifies the current naming diagnostic on a parser-generated missing identifier.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingTypeIdentifierIsReportedAsync() =>
        new Verify.Test { TestCode = "public class {|SST1300:|}{ }", CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);
}
