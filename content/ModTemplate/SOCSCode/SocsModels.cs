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
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    [JsonPropertyName("screen")]
    public string Screen { get; set; } = "unknown";

    [JsonPropertyName("lastActionStatus")]
    public string LastActionStatus { get; set; } = SocsActionStatus.None;

    [JsonPropertyName("system")]
    public SocsSystemSnapshot System { get; set; } = new();

    [JsonPropertyName("runMeta")]
    public SocsRunMetaSnapshot RunMeta { get; set; } = new();

    [JsonPropertyName("combat")]
    public SocsCombatSnapshot? Combat { get; set; }

    [JsonPropertyName("map")]
    public SocsMapSnapshot? Map { get; set; }

    [JsonPropertyName("rewards")]
    public SocsRewardsSnapshot? Rewards { get; set; }

    [JsonPropertyName("rest")]
    public SocsRestSnapshot? Rest { get; set; }

    [JsonPropertyName("shop")]
    public SocsShopSnapshot? Shop { get; set; }

    [JsonPropertyName("event")]
    public SocsEventSnapshot? Event { get; set; }

    [JsonPropertyName("selection")]
    public SocsSelectionSnapshot? Selection { get; set; }

    [JsonPropertyName("pending")]
    public SocsPendingSnapshot? Pending { get; set; }

    [JsonPropertyName("actionability")]
    public SocsActionabilitySnapshot? Actionability { get; set; }
}

internal sealed class SocsSystemSnapshot
{
    [JsonPropertyName("timeScale")]
    public float TimeScale { get; set; } = SocsConstants.DefaultTimeScale;
}

internal sealed class SocsRunMetaSnapshot
{
    [JsonPropertyName("hp")]
    public int? Hp { get; set; }

    [JsonPropertyName("gold")]
    public int? Gold { get; set; }

    [JsonPropertyName("floor")]
    public int? Floor { get; set; }

    [JsonPropertyName("relics")]
    public List<SocsRelicSnapshot> Relics { get; set; } = new();
}

internal sealed class SocsRelicSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class SocsCombatSnapshot
{
    [JsonPropertyName("playerEnergy")]
    public int? PlayerEnergy { get; set; }

    [JsonPropertyName("playerBlock")]
    public int? PlayerBlock { get; set; }

    [JsonPropertyName("playerPowers")]
    public List<SocsPowerSnapshot> PlayerPowers { get; set; } = new();

    [JsonPropertyName("hand")]
    public List<SocsHandCardSnapshot> Hand { get; set; } = new();

    [JsonPropertyName("drawPile")]
    public List<SocsHandCardSnapshot> DrawPile { get; set; } = new();

    [JsonPropertyName("discardPile")]
    public List<SocsHandCardSnapshot> DiscardPile { get; set; } = new();

    [JsonPropertyName("exhaustPile")]
    public List<SocsHandCardSnapshot> ExhaustPile { get; set; } = new();

    [JsonPropertyName("enemies")]
    public List<SocsEnemySnapshot> Enemies { get; set; } = new();
}

internal sealed class SocsMapSnapshot
{
    [JsonPropertyName("nodes")]
    public List<SocsMapNodeSnapshot> Nodes { get; set; } = new();

    [JsonPropertyName("currentNode")]
    public SocsMapNodeSnapshot? CurrentNode { get; set; }
}

internal sealed class SocsMapNodeSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

internal sealed class SocsRewardsSnapshot
{
    [JsonPropertyName("items")]
    public List<SocsRewardItemSnapshot> Items { get; set; } = new();
}

internal sealed class SocsRewardItemSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

internal sealed class SocsRestSnapshot
{
    [JsonPropertyName("options")]
    public List<SocsOptionSnapshot> Options { get; set; } = new();
}

internal sealed class SocsSelectionSnapshot
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("requiresTarget")]
    public bool? RequiresTarget { get; set; }

    [JsonPropertyName("options")]
    public List<SocsOptionSnapshot> Options { get; set; } = new();
}

internal sealed class SocsPendingSnapshot
{
    [JsonPropertyName("waiting")]
    public bool Waiting { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

internal sealed class SocsActionabilitySnapshot
{
    [JsonPropertyName("canPlayCard")]
    public bool CanPlayCard { get; set; }

    [JsonPropertyName("canChooseOption")]
    public bool CanChooseOption { get; set; }

    [JsonPropertyName("canEndTurn")]
    public bool CanEndTurn { get; set; }

    [JsonPropertyName("requiresTarget")]
    public bool RequiresTarget { get; set; }

    [JsonPropertyName("availableCommands")]
    public List<string> AvailableCommands { get; set; } = new();
}

internal sealed class SocsHandCardSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playable")]
    public bool? Playable { get; set; }

    [JsonPropertyName("energyCost")]
    public int? EnergyCost { get; set; }

    [JsonPropertyName("targeting")]
    public string? Targeting { get; set; }

    [JsonPropertyName("upgraded")]
    public bool? Upgraded { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("baseDamage")]
    public int? BaseDamage { get; set; }

    [JsonPropertyName("baseBlock")]
    public int? BaseBlock { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal sealed class SocsPowerSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}

internal sealed class SocsEnemySnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hp")]
    public int? Hp { get; set; }

    [JsonPropertyName("block")]
    public int? Block { get; set; }

    [JsonPropertyName("alive")]
    public bool? Alive { get; set; }

    [JsonPropertyName("intentType")]
    public string? IntentType { get; set; }

    [JsonPropertyName("intentDamage")]
    public int? IntentDamage { get; set; }

    [JsonPropertyName("intentMulti")]
    public int? IntentMulti { get; set; }

    [JsonPropertyName("powers")]
    public List<SocsPowerSnapshot> Powers { get; set; } = new();
}

internal sealed class SocsShopSnapshot
{
    [JsonPropertyName("items")]
    public List<SocsShopItemSnapshot> Items { get; set; } = new();

    [JsonPropertyName("leaveOption")]
    public SocsOptionSnapshot? LeaveOption { get; set; }
}

internal sealed class SocsShopItemSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("price")]
    public int? Price { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

internal sealed class SocsEventSnapshot
{
    [JsonPropertyName("eventName")]
    public string? EventName { get; set; }

    [JsonPropertyName("options")]
    public List<SocsOptionSnapshot> Options { get; set; } = new();
}

internal sealed class SocsOptionSnapshot
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
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
