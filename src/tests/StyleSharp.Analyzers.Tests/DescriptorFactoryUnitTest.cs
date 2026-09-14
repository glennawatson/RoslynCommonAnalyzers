// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared descriptor construction every Rules class goes through.</summary>
public sealed class DescriptorFactoryUnitTest
{
    /// <summary>The rule id every test builds.</summary>
    private const string Id = "TST0001";

    /// <summary>The docs-page link the id maps to.</summary>
    private const string HelpLink = "https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/TST0001.md";

    /// <summary>The rule title every test passes.</summary>
    private const string Title = "Title";

    /// <summary>The message format every test passes.</summary>
    private const string MessageFormat = "Message {0}";

    /// <summary>The category every test passes.</summary>
    private const string Category = "Category";

    /// <summary>The description every test passes.</summary>
    private const string Description = "Description";

    /// <summary>Verifies the default factory builds an enabled Warning carrying every text and the docs link.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateBuildsAnEnabledWarningAsync()
    {
        var descriptor = DescriptorFactory.Create(Id, Title, MessageFormat, Category, Description);

        await Assert.That(descriptor.Id).IsEqualTo(Id);
        await Assert.That(descriptor.Title.ToString()).IsEqualTo(Title);
        await Assert.That(descriptor.MessageFormat.ToString()).IsEqualTo(MessageFormat);
        await Assert.That(descriptor.Category).IsEqualTo(Category);
        await Assert.That(descriptor.Description.ToString()).IsEqualTo(Description);
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(descriptor.HelpLinkUri).IsEqualTo(HelpLink);
    }

    /// <summary>Verifies the opt-in factory builds a Warning that is off until configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateOptInBuildsADisabledWarningAsync()
    {
        var descriptor = DescriptorFactory.CreateOptIn(Id, Title, MessageFormat, Category, Description);

        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(descriptor.IsEnabledByDefault).IsFalse();
        await Assert.That(descriptor.HelpLinkUri).IsEqualTo(HelpLink);
    }

    /// <summary>Verifies the info factory builds an enabled Info descriptor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateInfoBuildsAnEnabledInfoAsync()
    {
        var descriptor = DescriptorFactory.CreateInfo(Id, Title, MessageFormat, Category, Description);

        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Info);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(descriptor.HelpLinkUri).IsEqualTo(HelpLink);
    }
}
