// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1405 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1405 — a MessagePack typeless deserializer reconstructs arbitrary types from untrusted input.</summary>
    public static readonly DiagnosticDescriptor TypelessDeserialization = Create(
        "SES1405",
        "A MessagePack typeless deserializer must not reconstruct arbitrary types from untrusted input",
        TypelessDeserializationMessage,
        Serialization,
        TypelessDeserializationDescription);

    /// <summary>The SES1405 message format.</summary>
    private const string TypelessDeserializationMessage =
        "'{0}' reconstructs whatever .NET type the payload names; deserializing untrusted MessagePack data through the typeless API "
        + "lets an attacker instantiate arbitrary types and run their construction logic";

    /// <summary>The SES1405 rule description.</summary>
    private const string TypelessDeserializationDescription =
        "MessagePack's typeless serialization embeds the full .NET type name of every value in the payload and reconstructs that "
        + "exact type on read, the same open type binding that makes BinaryFormatter dangerous. "
        + "'MessagePackSerializer.Typeless.Deserialize' and any serializer built on the 'TypelessObjectResolver' or "
        + "'TypelessContractlessStandardResolver' resolvers therefore instantiate whatever type the incoming bytes name -- an "
        + "attacker who controls the payload picks a gadget type whose constructor, deserialization callback, or property setter "
        + "touches the file system, opens a connection, or starts a process, turning crafted input into code execution. "
        + "Deserialize untrusted MessagePack into a known contract type with the default (typed) resolver instead, or validate the "
        + "incoming type against a fixed allow-list before constructing it. The rule reports the local shapes only: a "
        + "'Typeless.Deserialize'/'DeserializeAsync' call, and an '.Instance' reference to a typeless resolver that opens the type "
        + "set. A resolver first stored in a field or passed through a variable is out of scope because confirming it would "
        + "require data-flow tracking.";
}
