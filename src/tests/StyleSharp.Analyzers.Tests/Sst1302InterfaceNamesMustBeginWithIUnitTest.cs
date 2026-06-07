// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst1302 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1302InterfaceNamesMustBeginWithIAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1302 analyzer that requires interface names to begin with 'I'.</summary>
public class Sst1302InterfaceNamesMustBeginWithIUnitTest
{
    /// <summary>Verifies an empty document produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAsync() => await Verifysst1302.VerifyAnalyzerAsync(string.Empty);

    /// <summary>Verifies an interface already prefixed with 'I' produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidInterfaceWithPrefixAsync()
        => await Verifysst1302.VerifyAnalyzerAsync("public interface IWidget { }");

    /// <summary>Verifies a class without an 'I' prefix is ignored (the rule is interface-only).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassIsIgnoredAsync()
        => await Verifysst1302.VerifyAnalyzerAsync("public class Widget { }");

    /// <summary>Verifies an interface without an 'I' prefix reports the diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingPrefixReportsAsync()
        => await Verifysst1302.VerifyAnalyzerAsync("public interface {|SST1302:Widget|} { }");

    /// <summary>Verifies the code fix renames the interface to add the 'I' prefix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAddsPrefixAsync()
        => await Verifysst1302.VerifyCodeFixAsync(
            "public interface {|SST1302:Widget|} { }",
            "public interface IWidget { }");

    /// <summary>Verifies the code fix updates references to the renamed interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixUpdatesReferencesAsync()
    {
        const string source = """
                              public interface {|SST1302:Widget|} { }
                              public class C : Widget { }
                              """;
        const string fixedSource = """
                                   public interface IWidget { }
                                   public class C : IWidget { }
                                   """;

        await Verifysst1302.VerifyCodeFixAsync(source, fixedSource);
    }
}
