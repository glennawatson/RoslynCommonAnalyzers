// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ElementNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1300 (types and members should be PascalCase).</summary>
public class ElementNamingAnalyzerUnitTest
{
    /// <summary>Verifies a PascalCase class produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidClassAsync() => await Verify.VerifyAnalyzerAsync("public class Widget { }");

    /// <summary>Verifies a lower-case class is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassAsync()
        => await Verify.VerifyCodeFixAsync("public class {|SST1300:widget|} { }", "public class Widget { }");

    /// <summary>Verifies a lower-case method is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public void {|SST1300:doThing|}() { } }",
            "public class C { public void DoThing() { } }");

    /// <summary>Verifies a lower-case property is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyAsync()
        => await Verify.VerifyCodeFixAsync(
            "public class C { public int {|SST1300:myProp|} { get; set; } }",
            "public class C { public int MyProp { get; set; } }");

    /// <summary>Verifies a lower-case enum member is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumMemberAsync()
        => await Verify.VerifyCodeFixAsync("public enum E { {|SST1300:valueOne|} }", "public enum E { ValueOne }");
}
