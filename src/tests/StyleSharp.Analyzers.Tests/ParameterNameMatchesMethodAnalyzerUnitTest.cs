// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1320ParameterNameMatchesMethodAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1320 (a parameter name should not match its containing method's name).</summary>
public class ParameterNameMatchesMethodAnalyzerUnitTest
{
    /// <summary>Verifies a parameter whose name equals the method name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterMatchesMethodAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void Foo(int {|SST1320:Foo|}) { _ = Foo; } }");

    /// <summary>Verifies a parameter whose name differs from the method name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterDiffersAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void Bar(int value) { _ = value; } }");

    /// <summary>Verifies a camelCase parameter differing from the method only by case is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseDifferenceIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public int Value(int value) => value; }");

    /// <summary>Verifies a method with no parameters produces no diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoParametersAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void Ping() { } }");

    /// <summary>Verifies only the matching parameter in a list is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyMatchingParameterInListAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void Send(int count, string {|SST1320:Send|}) { _ = count; _ = Send; } }");

    /// <summary>Verifies a generic method whose parameter matches its name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericMethodAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void Map<T>(T {|SST1320:Map|}) { _ = Map; } }");

    /// <summary>Verifies a constructor whose parameter matches the type name is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync("public class Widget { public Widget(int Widget) { _ = Widget; } }");

    /// <summary>Verifies a local function whose parameter matches its name is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync("public class C { public void M() { void Inner(int Inner) { _ = Inner; } Inner(1); } }");
}
