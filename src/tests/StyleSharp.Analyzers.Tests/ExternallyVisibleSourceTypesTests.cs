// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the selection of instantiable, externally visible source types.</summary>
public class ExternallyVisibleSourceTypesTests
{
    /// <summary>The source every selection test binds against.</summary>
    private const string Source = """
        public class PublicClass
        {
            public class NestedPublic { }
            private class NestedPrivate { }
        }

        public struct PublicStruct { }
        internal class InternalClass { }
        public static class StaticClass { }
        public interface IPublic { }
        public enum PublicEnum { None }
        """;

    /// <summary>Verifies a public class or struct qualifies, and hidden, static, interface and enum types do not.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <param name="expected">Whether the type is selected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("PublicClass", true)]
    [Arguments("PublicClass+NestedPublic", true)]
    [Arguments("PublicClass+NestedPrivate", false)]
    [Arguments("PublicStruct", true)]
    [Arguments("InternalClass", false)]
    [Arguments("StaticClass", false)]
    [Arguments("IPublic", false)]
    [Arguments("PublicEnum", false)]
    public async Task SelectsInstantiableVisibleTypesAsync(string metadataName, bool expected)
    {
        var type = SemanticModelFactory.Create(Source).Model.Compilation.GetTypeByMetadataName(metadataName)!;

        await Assert.That(ExternallyVisibleSourceTypes.IsInstanceClassOrStruct(type)).IsEqualTo(expected);
    }

    /// <summary>Verifies a metadata type, which has no source location, is never selected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MetadataTypeIsNotSelectedAsync()
    {
        var type = SemanticModelFactory.Create(Source).Model.Compilation.GetTypeByMetadataName("System.Uri")!;

        await Assert.That(ExternallyVisibleSourceTypes.IsInstanceClassOrStruct(type)).IsFalse();
    }
}
