using System.Text.Json.Serialization;

namespace SOCS.Code;

internal static class SocsConstants
{
    public const int Port = 7777;
    public const int MaxFrameBytes = 1024 * 1024;
    public const int SnapshotFrameInterval = 10;
    public const float MinTimeScale = 0.1f;
    public const float MaxTimeScale = 50.0f;
    public const float DefaultTimeScale = 1.0f;
}

internal static class SocsActionStatus
{
    public const string None = "NONE";
    public const string Success = "SUCCESS";
    public const string Fail = "FAIL";
}

internal sealed class SocsInboundCommand
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("payload")]
    public SocsCommandPayload? Payload { get; set; }
}

internal sealed class SocsCommandPayload
{
    [JsonPropertyName("value")]
    public float? Value { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("cardId")]
    public string? CardId { get; set; }
}

internal sealed class SocsSnapshotEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "snapshot";

    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    [JsonPropertyName("frame")]
    public ulong Frame { get; set; }

    [JsonPropertyName("ts")]
    public long TimestampUnixMs { get; set; }

    [JsonPropertyName("data")]
    public GameStateSnapshot Data { get; set; } = new();
}

internal sealed class GameStateSnapshot
{
    [JsonPropertyName("hp")]
    public int? Hp { get; set; }

    [JsonPropertyName("gold")]
    public int? Gold { get; set; }

    [JsonPropertyName("floor")]
    public int? Floor { get; set; }

    [JsonPropertyName("timeScale")]
    public float TimeScale { get; set; }

    [JsonPropertyName("hand")]
    public List<SocsHandCardSnapshot> Hand { get; set; } = new();

    [JsonPropertyName("lastActionStatus")]
    public string LastActionStatus { get; set; } = SocsActionStatus.None;
}

internal sealed class SocsHandCardSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class SocsResponseEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "response";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

internal sealed class SocsErrorEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
