// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1606 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1606 — a model-weights file is fetched from a cleartext <c>http://</c> URL.</summary>
    public static readonly DiagnosticDescriptor CleartextModelWeightsUrl = Create(
        "SES1606",
        "Do not fetch model weights over cleartext HTTP",
        "The model-weights URL targets cleartext HTTP host '{0}'; http lets a tampered or backdoored model be swapped in transit -- use https and verify a published hash or signature",
        Ai,
        CleartextModelWeightsUrlDescription);

    /// <summary>The SES1606 rule description.</summary>
    private const string CleartextModelWeightsUrlDescription =
        "A URL whose scheme is 'http://' and whose path ends in a model-weights extension (.onnx, .gguf, .safetensors, "
        + ".pt, .pth, .ckpt) crosses the network unencrypted, so a network attacker on the path can replace the file with "
        + "a tampered or backdoored model. Weights fully determine what a model does at inference: a swapped file can leak "
        + "prompts, emit attacker-chosen output, or run an embedded deserialization payload, and nothing at the call site "
        + "would reveal the substitution. The rule fires on the hard-coded literal wherever it appears -- a constant, a "
        + "field, or an argument to any loader -- because the weights extension plus the cleartext scheme is on its own a "
        + "high-confidence signal, and it stays silent for loopback hosts (localhost, 127.0.0.1, ::1, and any *.localhost "
        + "host) where cleartext is expected in local development. Fetch weights over 'https://' and verify the download "
        + "against a hash or signature published by the model's author before loading it; transport encryption alone "
        + "proves the file arrived unaltered from the server, not that the server served the model you trust.";
}
