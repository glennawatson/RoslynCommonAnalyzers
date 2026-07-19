// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2505 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2505 — a parameterized test declares no data source, so the runner cannot supply its arguments.</summary>
    public static readonly DiagnosticDescriptor ParameterizedTestWithoutDataSource = Create(
        "SST2505",
        "A parameterized test declares no data source",
        ParameterizedTestWithoutDataSourceMessage,
        ParameterizedTestWithoutDataSourceDescription);

    /// <summary>The ParameterizedTestWithoutDataSource message format.</summary>
    private const string ParameterizedTestWithoutDataSourceMessage =
        "The test method '{0}' declares parameters but has no data source, so the runner cannot supply arguments and the test "
        + "never runs; add a data source (such as [InlineData], [TestCase], or [DataRow]) or remove the parameters";

    /// <summary>The ParameterizedTestWithoutDataSource rule description.</summary>
    private const string ParameterizedTestWithoutDataSourceDescription =
        "A test method with parameters is data-driven: the runner needs a data source to fill those parameters for each case. "
        + "When a test method carries a test attribute and declares one or more parameters but no data source, there is nothing "
        + "to supply the arguments, so depending on the framework the case is skipped, errors, or silently never executes — the "
        + "assertions inside it are never reached and the test appears to pass while checking nothing. The data source each "
        + "framework expects differs: xUnit needs a data attribute such as [InlineData], [MemberData], or [ClassData] (or any "
        + "attribute deriving from its data-attribute base); NUnit needs [TestCase] or [TestCaseSource] on the method, or a "
        + "per-parameter source such as [Values], [Range], or [Random]; MSTest needs [DataRow] or [DynamicData]. The rule reports "
        + "only when the method carries a recognized test attribute, declares at least one parameter, and no recognized data "
        + "source is present on the method or any of its parameters. A parameterless test and a parameterized test that already "
        + "has any data source are never reported.";
}
