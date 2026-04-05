using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

namespace SOCS.Code;

internal static class SocsRuntime
{
    private const int DiscoveryMaxDepth = 8;
    private const int DiscoveryMaxNodes = 4096;
    private const int DiscoveryPathMaxDepth = 8;
    private const int DiscoveryPathMaxNodes = 4096;
    private static readonly string ConfigPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        $"{MainFile.ModId.ToLowerInvariant()}_config.txt"
    );

    private static readonly ConcurrentQueue<SocsPendingCommand> PendingCommands = new();
    private static readonly BindingFlags ReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] RunMemberNames = ["Run", "CurrentRun", "_run", "run", "SerializableRun", "CurrentGameRun"];
    private static readonly string[] PlayerManagerMemberNames = ["PlayerManager", "_playerManager", "playerManager", "Party", "Players", "PlayersManager"];
    private static readonly string[] CombatMemberNames = ["Combat", "CurrentCombat", "Battle", "CurrentBattle", "Encounter", "Room", "CurrentRoom"];
    private static readonly string[] RootMemberNames = ["Run", "Game", "Dungeon", "AbstractDungeon", "Battle", "Combat"];
    private static readonly string[] DungeonSingletonKeywords = ["Dungeon", "Game", "Battle", "Combat", "Run"];
    private static readonly string[] DiscoveryMemberKeywords = ["Run", "Player", "Dungeon", "State", "Combat", "Screen", "Room", "Battle", "Hand", "Card"];
    private static readonly string[] StaticSingletonMemberNames = ["Instance", "Current", "CurrentRun", "CurrentState", "CurrentDungeon", "SharedInstance", "_instance", "s_instance"];
    private static readonly string[] HandParentNames = ["Hand", "hand", "HandGroup", "CardsInHand", "CardsInHandGroup", "HandCards", "cardsInHand"];
    private static readonly string[] DrawPileNames = ["DrawPile", "drawPile", "Draw", "draw", "Deck", "deck", "DrawDeck", "drawDeck"];
    private static readonly string[] DiscardPileNames = ["DiscardPile", "discardPile", "Discard", "discard", "DiscardDeck", "discardDeck"];
    private static readonly string[] ExhaustPileNames = ["ExhaustPile", "exhaustPile", "Exhaust", "exhaust", "Exhausted", "exhausted", "RemovedFromCombat", "removedFromCombat"];
    private static readonly string[] PlayerParentNames = ["Player", "player", "Hero", "hero", "Character", "character"];
    private static readonly string[] CardNameMembers = ["Name", "name", "CardName", "DisplayName", "Title", "title"];
    private static readonly string[] CardIdMembers = ["Uuid", "UUID", "Guid", "guid", "Id", "id", "InstanceId", "instanceId"];
    private static readonly string[] CollectionCountMembers = ["Count", "count", "Length", "length", "Size", "size"];
    private static readonly string[] CardTypeKeywords = ["Card"];
    private static readonly string[] EnemyTypeKeywords = ["Enemy", "Monster", "Creature", "Opponent"];
    private static readonly string[] ZoneMemberKeywords = ["Hand", "Draw", "Deck", "Discard", "Exhaust", "Pile"];
    private static readonly string[] EnergyMemberNames = ["Energy", "energy", "CurrentEnergy", "currentEnergy", "EnergyCount", "energyCount"];
    private static readonly string[] EnergyContainerNames = ["EnergyManager", "energyManager", "EnergyPanel", "energyPanel", "Mana", "mana"];
    private static readonly string[] BlockMemberNames = ["Block", "block", "CurrentBlock", "currentBlock", "Armor", "armor"];
    private static readonly string[] EnemyCollectionMemberNames = ["Enemies", "enemies", "Monsters", "monsters", "Creatures", "creatures", "Combatants", "combatants", "Opponents", "opponents"];
    private static readonly string[] AliveMemberNames = ["Alive", "alive", "IsAlive", "isAlive", "Dead", "dead", "IsDead", "isDead"];
    private static readonly string[] PlayableMemberNames = ["Playable", "playable", "IsPlayable", "isPlayable", "CanPlay", "canPlay", "CanUse", "canUse", "CanCast", "canCast"];
    private static readonly string[] CostMemberNames = ["Cost", "cost", "EnergyCost", "energyCost", "ManaCost", "manaCost", "BaseCost", "baseCost"];
    private static readonly string[] UpgradeMemberNames = ["Upgraded", "upgraded", "IsUpgraded", "isUpgraded", "UpgradedThisCombat", "upgradedThisCombat"];
    private static readonly string[] CardTypeMemberNames = ["Type", "type", "CardType", "cardType"];
    private static readonly string[] DamageMemberNames = ["BaseDamage", "baseDamage", "Damage", "damage", "DisplayedDamage", "displayedDamage"];
    private static readonly string[] DescriptionMemberNames = ["Description", "description", "RawDescription", "rawDescription", "Text", "text", "Body", "body"];
    private static readonly string[] PowerCollectionMemberNames = ["Powers", "powers", "Statuses", "statuses", "Buffs", "buffs", "Debuffs", "debuffs", "Effects", "effects"];
    private static readonly string[] PowerIdMemberNames = ["Id", "id", "PowerId", "powerId", "Name", "name", "Key", "key"];
    private static readonly string[] PowerAmountMemberNames = ["Amount", "amount", "Stacks", "stacks", "Count", "count", "Value", "value"];
    private static readonly string[] IntentTypeMemberNames = ["Intent", "intent", "IntentType", "intentType", "MoveType", "moveType", "MoveIntent", "moveIntent"];
    private static readonly string[] IntentDamageMemberNames = ["IntentDamage", "intentDamage", "MoveDamage", "moveDamage", "Damage", "damage", "IntentDmg", "intentDmg"];
    private static readonly string[] IntentMultiMemberNames = ["IntentMulti", "intentMulti", "MultiCount", "multiCount", "HitCount", "hitCount", "Multiplier", "multiplier"];
    private static readonly string[] TargetTypeMemberNames = ["Target", "target", "TargetType", "targetType", "RequiresTarget", "requiresTarget", "NeedTarget", "needTarget"];
    private static readonly string[] ShopScreenNames = ["Shop", "ShopScreen", "Merchant", "MerchantScreen", "Store", "StoreScreen"];
    private static readonly string[] EventScreenNames = ["Event", "EventScreen", "CurrentEvent", "MapEvent"];
    private static readonly string[] ShopItemsMemberNames = ["Items", "items", "ShopItems", "shopItems", "Cards", "cards", "Relics", "relics", "Potions", "potions", "Products", "products"];
    private static readonly string[] OptionCollectionMemberNames = ["Options", "options", "Choices", "choices", "Buttons", "buttons", "Responses", "responses"];
    private static readonly string[] OptionLabelMemberNames = ["Label", "label", "Text", "text", "Title", "title", "Name", "name", "Description", "description"];
    private static readonly string[] LeaveOptionMemberNames = ["Leave", "leave", "LeaveOption", "leaveOption", "Cancel", "cancel", "Exit", "exit"];
    private static readonly string[] PriceMemberNames = ["Price", "price", "Cost", "cost", "GoldCost", "goldCost"];
    private static readonly string[] BoolStateMemberNames = ["Enabled", "enabled", "IsEnabled", "isEnabled", "Interactable", "interactable", "Selectable", "selectable", "Available", "available"];
    private static readonly string[] RelicCollectionMemberNames = ["Relics", "relics", "PassiveItems", "passiveItems", "Artifacts", "artifacts"];
    private const int MaxPlausibleHandCards = 20;
    private const int MaxPlausibleZoneCards = 200;
    private const int MaxPlausibleShopItems = 64;
    private const int MaxPlausibleOptions = 16;

    private static SocsServer? _server;
    private static bool _initialized;
    private static long _sequence;
    private static ulong _lastSnapshotFrame;
    private static float _targetTimeScale = SocsConstants.DefaultTimeScale;
    private static string _lastActionStatus = SocsActionStatus.None;
    private static object? _cachedSingleton;
    private static int _singletonRescanCounter;
    private const int SingletonRescanInterval = 60; // Rescan every 60 frames if null
    private static int _framesSinceInit;
    private const int SnapshotStartDelayFrames = 300; // ~5 seconds at 60fps
    private static readonly Dictionary<string, SocsMemberPath> CachedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static bool _discoveryTopologyLogged;
    private static int _nextDiscoveryFrame;
    private const int DiscoveryRetryFrameInterval = 300;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _targetTimeScale = LoadSavedSpeed();
        _server = new SocsServer(EnqueueCommand);
        _server.Start();
        _initialized = true;
    }

    public static void Tick()
    {
        if (!_initialized || _server == null)
        {
            return;
        }

        _framesSinceInit++;
        EnsureStickyTimeScale();
        DrainCommands();

        // Delay snapshot broadcast until game is fully loaded
        if (_framesSinceInit > SnapshotStartDelayFrames)
        {
            BroadcastSnapshotIfNeeded();
        }
    }

    public static void ApplyStickyTimeScale()
    {
        Engine.TimeScale = _targetTimeScale;
    }

    private static void EnsureStickyTimeScale()
    {
        if (Mathf.Abs(Engine.TimeScale - _targetTimeScale) > 0.0001f)
        {
            Callable.From(ApplyStickyTimeScale).CallDeferred();
        }
    }

    private static void EnqueueCommand(SocsInboundCommand command, SocsClientConnection client)
    {
        PendingCommands.Enqueue(new SocsPendingCommand(command, client));
    }

    private static void DrainCommands()
    {
        while (PendingCommands.TryDequeue(out SocsPendingCommand pending))
        {
            HandleCommand(pending.Command, pending.Client);
        }
    }

    private static void HandleCommand(SocsInboundCommand command, SocsClientConnection client)
    {
        string commandName = command.Name?.Trim().ToLowerInvariant() ?? string.Empty;
        switch (commandName)
        {
            case "ping":
                MarkActionStatus(SocsActionStatus.Success);
                _server?.SendResponse(client, new SocsResponseEnvelope
                {
                    Id = command.Id,
                    Name = "ping",
                    Ok = true,
                    Payload = new { message = "pong", lastActionStatus = _lastActionStatus }
                });
                break;

            case "set_time_scale":
                float requestedValue = command.Payload?.Value ?? SocsConstants.DefaultTimeScale;
                float clampedValue = Mathf.Clamp(requestedValue, SocsConstants.MinTimeScale, SocsConstants.MaxTimeScale);
                _targetTimeScale = clampedValue;
                Callable.From(ApplyStickyTimeScale).CallDeferred();
                SaveSpeed(_targetTimeScale);
                InvalidateDiscoveryCache("set_time_scale");
                MarkActionStatus(SocsActionStatus.Success);
                _server?.SendResponse(client, new SocsResponseEnvelope
                {
                    Id = command.Id,
                    Name = "set_time_scale",
                    Ok = true,
                    Payload = new { value = _targetTimeScale, lastActionStatus = _lastActionStatus }
                });
                break;

            case "play_card":
                HandlePlayCard(command, client);
                break;

            default:
                MarkActionStatus(SocsActionStatus.Fail);
                _server?.SendResponse(client, new SocsErrorEnvelope
                {
                    Id = command.Id,
                    Message = $"Unknown SOCS command: {command.Name ?? "<null>"}."
                });
                break;
        }
    }

    private static void HandlePlayCard(SocsInboundCommand command, SocsClientConnection client)
    {
        int? index = command.Payload?.Index;
        string? requestedCardId = command.Payload?.CardId;
        bool matched = TryMatchHandCard(index, requestedCardId, out SocsHandCardSnapshot? matchedCard);

        string status = matched ? SocsActionStatus.Success : SocsActionStatus.Fail;
        if (matched)
        {
            InvalidateDiscoveryCache("play_card");
        }

        MarkActionStatus(status);

        Callable.From(() => ConfirmPlayCard(matchedCard)).CallDeferred();

        _server?.SendResponse(client, new SocsResponseEnvelope
        {
            Id = command.Id,
            Name = "play_card",
            Ok = matched,
            Payload = new
            {
                requestedIndex = index,
                requestedCardId,
                matched,
                matchedCard,
                lastActionStatus = _lastActionStatus
            }
        });
    }

    private static void ConfirmPlayCard(SocsHandCardSnapshot? matchedCard)
    {
        if (matchedCard == null)
        {
            GD.PushWarning("SOCS play_card failed: no matching card found in current hand snapshot.");
            return;
        }

        GD.Print($"SOCS play_card matched hand card {matchedCard.Index}:{matchedCard.Id}:{matchedCard.Name ?? "<unnamed>"}.");
    }

    private static void MarkActionStatus(string status)
    {
        _lastActionStatus = status;
    }

    private static void InvalidateDiscoveryCache(string reason)
    {
        GD.Print($"[SOCS] [DISCOVERY] Invalidating discovery state after {reason}.");
        CachedPaths.Clear();
        _cachedSingleton = null;
        _singletonRescanCounter = 0;
        _discoveryTopologyLogged = false;
        _nextDiscoveryFrame = 0;
    }

    private static void BroadcastSnapshotIfNeeded()
    {
        ulong currentFrame = (ulong)Engine.GetFramesDrawn();
        if (_lastSnapshotFrame != 0 && currentFrame - _lastSnapshotFrame < SocsConstants.SnapshotFrameInterval)
        {
            return;
        }

        _lastSnapshotFrame = currentFrame;
        var snapshot = new SocsSnapshotEnvelope
        {
            Seq = Interlocked.Increment(ref _sequence),
            Frame = currentFrame,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = BuildSnapshot()
        };

        byte[] payload = SocsProtocol.Serialize(snapshot);
        _server?.Broadcast(payload);
    }

    private static GameStateSnapshot BuildSnapshot()
    {
        var snapshot = new GameStateSnapshot();
        SnapshotProbeContext context = BuildProbeContext();
        string screen = DetectScreen(context);

        TryAssignField("screen", () => snapshot.Screen = screen);
        TryAssignField("lastActionStatus", () => snapshot.LastActionStatus = _lastActionStatus);
        TryAssignField("system", () => snapshot.System = BuildSystemSnapshot());
        TryAssignField("runMeta", () => snapshot.RunMeta = BuildRunMetaSnapshot(context));
        TryAssignField("combat", () => snapshot.Combat = BuildCombatSnapshot(context, screen));
        TryAssignField("map", () => snapshot.Map = BuildMapSnapshot(context, screen));
        TryAssignField("rewards", () => snapshot.Rewards = BuildRewardsSnapshot(context, screen));
        TryAssignField("rest", () => snapshot.Rest = BuildRestSnapshot(context, screen));
        TryAssignField("shop", () => snapshot.Shop = BuildShopSnapshot(context, screen));
        TryAssignField("event", () => snapshot.Event = BuildEventSnapshot(context, screen));
        TryAssignField("selection", () => snapshot.Selection = BuildSelectionSnapshot(snapshot));
        TryAssignField("pending", () => snapshot.Pending = BuildPendingSnapshot(snapshot));
        TryAssignField("actionability", () => snapshot.Actionability = BuildActionabilitySnapshot(snapshot));

        return snapshot;
    }

    private static SocsSystemSnapshot BuildSystemSnapshot()
    {
        return new SocsSystemSnapshot
        {
            TimeScale = (float)Engine.TimeScale
        };
    }

    private static SocsRunMetaSnapshot BuildRunMetaSnapshot(SnapshotProbeContext context)
    {
        var runMeta = new SocsRunMetaSnapshot();
        TryAssignField("runMeta.hp", () => runMeta.Hp = ProbeSnapshotInt(context, "hp", ["CurrentHealth", "CurrentHp", "Health", "Hp"]));
        TryAssignField("runMeta.gold", () => runMeta.Gold = ProbeSnapshotInt(context, "gold", ["Gold", "gold"]));
        TryAssignField("runMeta.floor", () => runMeta.Floor = ProbeSnapshotInt(context, "floor", ["Floor", "floor", "CurrentFloor", "ActFloor"]));
        TryAssignField("runMeta.relics", () => runMeta.Relics = BuildRelicsSnapshot(context));
        return runMeta;
    }

    private static List<SocsRelicSnapshot> BuildRelicsSnapshot(SnapshotProbeContext context)
    {
        List<SocsRelicSnapshot> relics = [];
        int index = 0;
        foreach (object relic in EnumerateCompositeItems(GetRelicProbeSource(context), context.Roots, RelicCollectionMemberNames, MaxPlausibleOptions))
        {
            string? name = TryReadStringByNames(relic, CardNameMembers);
            if (string.IsNullOrWhiteSpace(name) && !ContainsAnyKeyword(relic.GetType().Name, ["Relic", "Artifact"]))
            {
                continue;
            }

            relics.Add(new SocsRelicSnapshot
            {
                Index = index++,
                Id = BuildCardIdentity(relic),
                Name = name ?? relic.GetType().Name
            });
        }

        return relics;
    }

    private static object GetRelicProbeSource(SnapshotProbeContext context)
    {
        return context.Player ?? context.Run ?? context.PlayerManager ?? context.Roots.First();
    }

    private static SocsCombatSnapshot? BuildCombatSnapshot(SnapshotProbeContext context, string screen)
    {
        if (!string.Equals(screen, "combat", StringComparison.OrdinalIgnoreCase) && context.Combat == null)
        {
            return null;
        }

        var combat = new SocsCombatSnapshot();
        TryAssignField("combat.playerEnergy", () => combat.PlayerEnergy = ProbePlayerEnergy(context));
        TryAssignField("combat.playerBlock", () => combat.PlayerBlock = ProbeSnapshotInt(context, "playerBlock", BlockMemberNames));
        TryAssignField("combat.playerPowers", () => combat.PlayerPowers = BuildPowersSnapshot(context.Player ?? context.Combat));
        TryAssignField("combat.hand", () => combat.Hand = BuildHandSnapshot(context));
        TryAssignField("combat.drawPile", () => combat.DrawPile = BuildZoneSnapshot(context, "drawPile", DrawPileNames, MaxPlausibleZoneCards));
        TryAssignField("combat.discardPile", () => combat.DiscardPile = BuildZoneSnapshot(context, "discardPile", DiscardPileNames, MaxPlausibleZoneCards));
        TryAssignField("combat.exhaustPile", () => combat.ExhaustPile = BuildZoneSnapshot(context, "exhaustPile", ExhaustPileNames, MaxPlausibleZoneCards));
        TryAssignField("combat.enemies", () => combat.Enemies = BuildEnemiesSnapshot(context));
        return combat;
    }

    private static SocsMapSnapshot? BuildMapSnapshot(SnapshotProbeContext context, string screen)
    {
        return null;
    }

    private static SocsRewardsSnapshot? BuildRewardsSnapshot(SnapshotProbeContext context, string screen)
    {
        return null;
    }

    private static SocsRestSnapshot? BuildRestSnapshot(SnapshotProbeContext context, string screen)
    {
        return null;
    }

    private static SocsSelectionSnapshot? BuildSelectionSnapshot(GameStateSnapshot snapshot)
    {
        if (snapshot.Shop?.LeaveOption != null)
        {
            return new SocsSelectionSnapshot
            {
                Kind = "shop",
                RequiresTarget = false,
                Options = BuildSelectionOptions(snapshot.Shop.Items.Count, snapshot.Shop.LeaveOption)
            };
        }

        if (snapshot.Event?.Options.Count > 0)
        {
            return new SocsSelectionSnapshot
            {
                Kind = "event",
                RequiresTarget = false,
                Options = snapshot.Event.Options.ToList()
            };
        }

        if (snapshot.Rest?.Options.Count > 0)
        {
            return new SocsSelectionSnapshot
            {
                Kind = "rest",
                RequiresTarget = false,
                Options = snapshot.Rest.Options.ToList()
            };
        }

        return null;
    }

    private static SocsPendingSnapshot? BuildPendingSnapshot(GameStateSnapshot snapshot)
    {
        if (snapshot.Selection?.RequiresTarget == true)
        {
            return new SocsPendingSnapshot
            {
                Waiting = true,
                Reason = "target_selection"
            };
        }

        return null;
    }

    private static SocsActionabilitySnapshot BuildActionabilitySnapshot(GameStateSnapshot snapshot)
    {
        var actionability = new SocsActionabilitySnapshot();
        actionability.AvailableCommands.Add("ping");
        actionability.AvailableCommands.Add("set_time_scale");
        actionability.AvailableCommands.Add("play_card");

        actionability.CanPlayCard = snapshot.Combat?.Hand.Any(card => card.Playable == true) == true;
        actionability.CanChooseOption = snapshot.Selection?.Options.Any(option => option.Enabled) == true;
        actionability.CanEndTurn = snapshot.Combat != null;
        actionability.RequiresTarget = snapshot.Selection?.RequiresTarget == true;
        return actionability;
    }

    private static List<SocsOptionSnapshot> BuildSelectionOptions(int itemCount, SocsOptionSnapshot leaveOption)
    {
        List<SocsOptionSnapshot> options = [];
        for (int i = 0; i < itemCount; i++)
        {
            options.Add(new SocsOptionSnapshot
            {
                Index = i,
                Label = $"item_{i}",
                Enabled = true
            });
        }

        options.Add(new SocsOptionSnapshot
        {
            Index = itemCount,
            Label = leaveOption.Label,
            Enabled = leaveOption.Enabled
        });

        return options;
    }

    private static SnapshotProbeContext BuildProbeContext()
    {
        object[] roots = EnumerateDiscoveryRoots().ToArray();
        object? run = FindPriorityObject(roots, RunMemberNames);
        object? playerManager = FindPriorityObject(roots, PlayerManagerMemberNames);
        object? player = FindPlayerProbe(run, playerManager, roots);
        object? combat = FindPriorityObject(roots, CombatMemberNames);
        object? shop = FindPriorityObject(roots, ShopScreenNames);
        object? eventState = FindPriorityObject(roots, EventScreenNames);

        return new SnapshotProbeContext(roots, run, playerManager, player, combat, shop, eventState);
    }

    private static int? ProbeSnapshotInt(SnapshotProbeContext context, string cacheKey, string[] memberNames)
    {
        if (TryReadIntFromContext(context, memberNames, out int directValue))
        {
            return directValue;
        }

        int? cachedValue = TryGetCachedInt(cacheKey);
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        EnsureDiscoveryCache();
        return TryGetCachedInt(cacheKey);
    }

    private static int? TryReadNestedIntByNames(object source, string[] containerNames, string[] memberNames)
    {
        foreach (string containerName in containerNames)
        {
            object? container = TryReadMember(source, containerName);
            if (container == null)
            {
                continue;
            }

            int? nestedValue = TryReadIntByNames(container, memberNames);
            if (nestedValue.HasValue)
            {
                return nestedValue;
            }
        }

        return null;
    }

    private static bool TryReadIntFromContext(SnapshotProbeContext context, string[] memberNames, out int value)
    {
        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            foreach (string memberName in memberNames)
            {
                if (TryReadInt(candidate, memberName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static int? ProbePlayerEnergy(SnapshotProbeContext context)
    {
        if (TryReadIntFromContext(context, EnergyMemberNames, out int directValue))
        {
            return directValue;
        }

        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            int? nestedValue = TryReadNestedIntByNames(candidate, EnergyContainerNames, EnergyMemberNames);
            if (nestedValue.HasValue)
            {
                return nestedValue.Value;
            }
        }

        int? cachedValue = TryGetCachedInt("playerEnergy");
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        EnsureDiscoveryCache();
        return TryGetCachedInt("playerEnergy");
    }

    private static IEnumerable<object> EnumerateProbeCandidates(SnapshotProbeContext context)
    {
        var seen = new HashSet<int>();

        foreach (object? candidate in new[] { context.Player, context.Run, context.PlayerManager, context.Combat })
        {
            if (candidate == null)
            {
                continue;
            }

            int identity = RuntimeHelpers.GetHashCode(candidate);
            if (seen.Add(identity))
            {
                yield return candidate;
            }
        }

        foreach (object root in context.Roots)
        {
            int identity = RuntimeHelpers.GetHashCode(root);
            if (seen.Add(identity))
            {
                yield return root;
            }
        }
    }

    private static object? FindPriorityObject(IEnumerable<object> roots, string[] memberNames)
    {
        foreach (object root in roots)
        {
            object? direct = TryFindMember(root, memberNames);
            if (direct != null)
            {
                return direct;
            }

            foreach (object? candidate in EnumerateRootMembers(root))
            {
                if (candidate == null)
                {
                    continue;
                }

                object? nested = TryFindMember(candidate, memberNames);
                if (nested != null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static object? FindPlayerProbe(object? run, object? playerManager, IEnumerable<object> roots)
    {
        object? player = FindPlayerFromSource(run);
        if (player != null)
        {
            return player;
        }

        player = FindPlayerFromSource(playerManager);
        if (player != null)
        {
            return player;
        }

        foreach (object root in roots)
        {
            player = FindPlayerFromSource(root);
            if (player != null)
            {
                return player;
            }
        }

        return null;
    }

    private static object? FindPlayerFromSource(object? source)
    {
        if (source == null)
        {
            return null;
        }

        object? direct = TryFindMember(source, PlayerParentNames);
        if (direct != null)
        {
            return direct;
        }

        object? nestedRun = TryFindNestedMember(source, RunMemberNames, PlayerParentNames);
        if (nestedRun != null)
        {
            return nestedRun;
        }

        object? nestedManager = TryFindNestedMember(source, PlayerManagerMemberNames, PlayerParentNames);
        if (nestedManager != null)
        {
            return nestedManager;
        }

        return null;
    }

    private static object? FindZoneSource(SnapshotProbeContext context, string cacheKey, string[] zoneNames, int maxCards)
    {
        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            foreach (object zoneCandidate in EnumerateZoneCandidates(candidate, cacheKey, zoneNames, maxCards))
            {
                return zoneCandidate;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateZoneCandidates(object source, string cacheKey, string[] zoneNames, int maxCards)
    {
        foreach (object candidate in EnumerateNamedZoneCandidates(source, zoneNames))
        {
            if (TryBuildCardSnapshot(candidate, maxCards, out _, out int totalCount) && IsAcceptedZoneSource(cacheKey, totalCount))
            {
                yield return candidate;
            }
        }

        foreach (string playerName in PlayerParentNames)
        {
            object? player = TryReadMember(source, playerName);
            if (player == null)
            {
                continue;
            }

            foreach (object candidate in EnumerateNamedZoneCandidates(player, zoneNames))
            {
                if (TryBuildCardSnapshot(candidate, maxCards, out _, out int totalCount) && IsAcceptedZoneSource(cacheKey, totalCount))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<object> EnumerateNamedZoneCandidates(object source, string[] zoneNames)
    {
        object? direct = TryFindMember(source, zoneNames);
        if (direct != null)
        {
            yield return direct;
        }
    }

    private static bool IsAcceptedZoneSource(string cacheKey, int totalCount)
    {
        if (totalCount < 0)
        {
            return false;
        }

        return cacheKey switch
        {
            "hand" => totalCount <= MaxPlausibleHandCards,
            _ => totalCount >= 0
        };
    }

    private static bool IsPlausibleZoneSource(object zoneSource, int maxCards)
    {
        if (!TryBuildCardSnapshot(zoneSource, maxCards, out List<SocsHandCardSnapshot> cards, out int totalCount))
        {
            return false;
        }

        return totalCount >= 0 && totalCount <= maxCards && cards.Count <= maxCards;
    }

    private static bool IsMatchingZoneLeaf(string cacheKey, string leafName)
    {
        return GetZoneNames(cacheKey).Any(name => string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetZoneNames(string cacheKey)
    {
        return cacheKey switch
        {
            "hand" => HandParentNames,
            "drawPile" => DrawPileNames,
            "discardPile" => DiscardPileNames,
            "exhaustPile" => ExhaustPileNames,
            _ => []
        };
    }

    private static int GetZoneCardLimit(string cacheKey)
    {
        return string.Equals(cacheKey, "hand", StringComparison.OrdinalIgnoreCase)
            ? MaxPlausibleHandCards
            : MaxPlausibleZoneCards;
    }

    private static bool TryResolveZoneFromPath(SocsMemberPath path, string cacheKey, out object? value)
    {
        value = null;
        if (!IsMatchingZoneLeaf(cacheKey, path.LeafName))
        {
            return false;
        }

        if (!TryReadPathValue(path, out object? zoneSource) || zoneSource == null)
        {
            return false;
        }

        int maxCards = GetZoneCardLimit(cacheKey);
        if (!TryBuildCardSnapshot(zoneSource, maxCards, out _, out int totalCount) || !IsAcceptedZoneSource(cacheKey, totalCount))
        {
            return false;
        }

        value = zoneSource;
        return true;
    }

    private static bool PathsReferenceSameSource(SocsMemberPath left, SocsMemberPath right)
    {
        if (!ReferenceEquals(left.Root, right.Root))
        {
            return false;
        }

        if (left.Members.Count != right.Members.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Members.Count; i++)
        {
            if (!string.Equals(left.Members[i].Name, right.Members[i].Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void RemoveZoneAliasConflicts(string cacheKey, SocsMemberPath path)
    {
        foreach (string otherKey in new[] { "hand", "drawPile", "discardPile", "exhaustPile" })
        {
            if (string.Equals(otherKey, cacheKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (CachedPaths.TryGetValue(otherKey, out SocsMemberPath? existingPath) && PathsReferenceSameSource(existingPath, path))
            {
                CachedPaths.Remove(otherKey);
                LogDiscovery($"[SOCS] [DISCOVERY] Removed aliased zone cache {otherKey}: {existingPath.DisplayPath}");
            }
        }
    }

    private static bool TryGetCachedZoneSnapshot(string cacheKey, out List<SocsHandCardSnapshot> snapshot)
    {
        snapshot = [];
        if (!CachedPaths.TryGetValue(cacheKey, out SocsMemberPath? zonePath))
        {
            return false;
        }

        if (!TryResolveZoneFromPath(zonePath, cacheKey, out object? cachedZoneSource) || cachedZoneSource == null)
        {
            LogDiscoveryWarning($"[SOCS] [DISCOVERY] Cached zone path invalidated for {cacheKey}: {zonePath.DisplayPath}");
            CachedPaths.Remove(cacheKey);
            _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
            return false;
        }

        int maxCards = GetZoneCardLimit(cacheKey);
        if (!TryBuildCardSnapshot(cachedZoneSource, maxCards, out snapshot, out _))
        {
            CachedPaths.Remove(cacheKey);
            _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
            return false;
        }

        return true;
    }

    private static bool TryGetZoneSnapshotFromDiscovery(string cacheKey, out List<SocsHandCardSnapshot> snapshot)
    {
        snapshot = [];
        EnsureDiscoveryCache();
        return TryGetCachedZoneSnapshot(cacheKey, out snapshot);
    }

    private static bool IsAcceptedZonePath(string cacheKey, SocsMemberPath path, object candidateValue)
    {
        if (!IsMatchingZoneLeaf(cacheKey, path.LeafName))
        {
            return false;
        }

        int maxCards = GetZoneCardLimit(cacheKey);
        return TryBuildCardSnapshot(candidateValue, maxCards, out _, out int totalCount)
            && IsAcceptedZoneSource(cacheKey, totalCount);
    }

    private static bool HasZoneAliasConflict(string cacheKey, SocsMemberPath path)
    {
        foreach ((string otherKey, SocsMemberPath existingPath) in CachedPaths)
        {
            if (string.Equals(otherKey, cacheKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (PathsReferenceSameSource(existingPath, path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindDistinctZonePath(object gameRoot, string cacheKey, out SocsMemberPath? path, out object? value)
    {
        foreach ((SocsMemberPath candidatePath, object candidateValue) in EnumerateDiscoveryPaths(gameRoot))
        {
            if (!IsAcceptedZonePath(cacheKey, candidatePath, candidateValue))
            {
                continue;
            }

            if (HasZoneAliasConflict(cacheKey, candidatePath))
            {
                continue;
            }

            path = candidatePath;
            value = candidateValue;
            return true;
        }

        path = null;
        value = null;
        return false;
    }

    private static bool TryFindAnyAcceptedZonePath(object gameRoot, string cacheKey, out SocsMemberPath? path, out object? value)
    {
        foreach ((SocsMemberPath candidatePath, object candidateValue) in EnumerateDiscoveryPaths(gameRoot))
        {
            if (!IsAcceptedZonePath(cacheKey, candidatePath, candidateValue))
            {
                continue;
            }

            path = candidatePath;
            value = candidateValue;
            return true;
        }

        path = null;
        value = null;
        return false;
    }

    private static void LogZoneAliasFallback(string cacheKey, SocsMemberPath path)
    {
        if (HasZoneAliasConflict(cacheKey, path))
        {
            LogDiscoveryWarning($"[SOCS] [DISCOVERY] {cacheKey} still aliases another zone at {path.DisplayPath}");
        }
    }

    private static bool TryBuildCardSnapshot(object zoneSource, int maxCards, out List<SocsHandCardSnapshot> snapshot, out int totalCount)
    {
        snapshot = [];
        totalCount = -1;

        IEnumerable<object?>? cards = TryEnumerateCardItems(zoneSource, maxCards, out int discoveredCount);
        if (cards == null)
        {
            return false;
        }

        totalCount = discoveredCount;
        if (totalCount > maxCards)
        {
            return false;
        }

        int index = 0;
        foreach (object? card in cards)
        {
            if (card == null || !LooksLikeCard(card))
            {
                return false;
            }

            snapshot.Add(BuildCardSnapshot(card, index++));
        }

        return true;
    }

    private static SocsHandCardSnapshot BuildCardSnapshot(object card, int index)
    {
        var snapshot = new SocsHandCardSnapshot
        {
            Index = index,
            Id = BuildCardIdentity(card),
            Name = TryReadStringByNames(card, CardNameMembers),
            Playable = TryReadBoolByNames(card, out bool playable, PlayableMemberNames) ? playable : null,
            EnergyCost = TryReadIntByNames(card, CostMemberNames),
            Targeting = NormalizeTargeting(card)
        };

        TryAssignField($"combat.hand[{index}].upgraded", () => snapshot.Upgraded = TryReadBoolByNames(card, out bool upgraded, UpgradeMemberNames) ? upgraded : null);
        TryAssignField($"combat.hand[{index}].type", () => snapshot.Type = NormalizeCardType(card));
        TryAssignField($"combat.hand[{index}].baseDamage", () => snapshot.BaseDamage = TryReadIntByNames(card, DamageMemberNames));
        TryAssignField($"combat.hand[{index}].baseBlock", () => snapshot.BaseBlock = TryReadIntByNames(card, BlockMemberNames));
        TryAssignField($"combat.hand[{index}].description", () => snapshot.Description = TryReadStringByNames(card, DescriptionMemberNames));
        return snapshot;
    }

    private static string? NormalizeTargeting(object card)
    {
        object? rawTarget = TryFindMember(card, TargetTypeMemberNames);
        if (rawTarget == null)
        {
            return null;
        }

        if (rawTarget is bool requiresTarget)
        {
            return requiresTarget ? "enemy" : "none";
        }

        string text = rawTarget.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Contains("enemy", StringComparison.OrdinalIgnoreCase) || text.Contains("monster", StringComparison.OrdinalIgnoreCase))
        {
            return "enemy";
        }

        if (text.Contains("self", StringComparison.OrdinalIgnoreCase))
        {
            return "self";
        }

        if (text.Contains("all", StringComparison.OrdinalIgnoreCase) || text.Contains("multi", StringComparison.OrdinalIgnoreCase) || text.Contains("area", StringComparison.OrdinalIgnoreCase))
        {
            return "any";
        }

        if (bool.TryParse(text, out bool parsedRequiresTarget))
        {
            return parsedRequiresTarget ? "enemy" : "none";
        }

        return "unknown";
    }

    private static string? NormalizeCardType(object card)
    {
        object? rawType = TryFindMember(card, CardTypeMemberNames);
        if (rawType == null)
        {
            return null;
        }

        string text = rawType.ToString()?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static List<SocsPowerSnapshot> BuildPowersSnapshot(object? source)
    {
        if (source == null)
        {
            return [];
        }

        List<SocsPowerSnapshot> powers = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (object power in EnumerateCompositeItems(source, [], PowerCollectionMemberNames, MaxPlausibleOptions * 4))
        {
            SocsPowerSnapshot? snapshot = TryBuildPowerSnapshot(power);
            if (snapshot == null)
            {
                continue;
            }

            string dedupeKey = $"{snapshot.Id}:{snapshot.Amount?.ToString(CultureInfo.InvariantCulture) ?? "null"}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            powers.Add(snapshot);
        }

        return powers;
    }

    private static SocsPowerSnapshot? TryBuildPowerSnapshot(object power)
    {
        try
        {
            string? id = TryReadStringByNames(power, PowerIdMemberNames);
            int? amount = TryReadIntByNames(power, PowerAmountMemberNames);
            if (string.IsNullOrWhiteSpace(id) && amount == null)
            {
                return null;
            }

            return new SocsPowerSnapshot
            {
                Id = id ?? BuildCardIdentity(power),
                Amount = amount
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeIntentType(object enemy)
    {
        object? rawIntent = TryFindMember(enemy, IntentTypeMemberNames);
        if (rawIntent == null)
        {
            return null;
        }

        string text = rawIntent.ToString()?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? TryReadIntentDamage(object enemy)
    {
        try
        {
            return TryReadIntByNames(enemy, IntentDamageMemberNames);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryReadIntentMulti(object enemy)
    {
        try
        {
            return TryReadIntByNames(enemy, IntentMultiMemberNames);
        }
        catch
        {
            return null;
        }
    }

    private static List<SocsEnemySnapshot> BuildEnemiesSnapshot(SnapshotProbeContext context)
    {
        object? source = context.Combat;
        if (source == null)
        {
            return [];
        }

        List<SocsEnemySnapshot> enemies = [];
        int index = 0;
        foreach (object enemy in EnumerateCompositeItems(source, context.Roots, EnemyCollectionMemberNames, MaxPlausibleOptions * 2))
        {
            if (!LooksLikeEnemy(enemy))
            {
                continue;
            }

            int enemyIndex = index++;
            int? hp = TryReadIntByNames(enemy, "CurrentHealth", "CurrentHp", "Health", "Hp");
            bool? alive = TryReadAliveState(enemy, hp);
            var snapshot = new SocsEnemySnapshot
            {
                Index = enemyIndex,
                Id = BuildCardIdentity(enemy),
                Name = TryReadStringByNames(enemy, CardNameMembers) ?? enemy.GetType().Name,
                Hp = hp,
                Block = TryReadIntByNames(enemy, BlockMemberNames),
                Alive = alive
            };

            TryAssignField($"combat.enemies[{enemyIndex}].intentType", () => snapshot.IntentType = NormalizeIntentType(enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].intentDamage", () => snapshot.IntentDamage = TryReadIntentDamage(enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].intentMulti", () => snapshot.IntentMulti = TryReadIntentMulti(enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].powers", () => snapshot.Powers = BuildPowersSnapshot(enemy));
            enemies.Add(snapshot);
        }

        return enemies;
    }

    private static bool? TryReadAliveState(object enemy, int? hp)
    {
        if (TryReadBoolByNames(enemy, out bool aliveValue, "Alive", "alive", "IsAlive", "isAlive"))
        {
            return aliveValue;
        }

        if (TryReadBoolByNames(enemy, out bool deadValue, "Dead", "dead", "IsDead", "isDead"))
        {
            return !deadValue;
        }

        if (hp.HasValue)
        {
            return hp.Value > 0;
        }

        return null;
    }

    private static List<SocsShopItemSnapshot> BuildShopItems(object source, IEnumerable<object> roots)
    {
        List<SocsShopItemSnapshot> items = [];
        int index = 0;
        foreach (object item in EnumerateCompositeItems(source, roots, ShopItemsMemberNames, MaxPlausibleShopItems))
        {
            string? name = TryReadStringByNames(item, CardNameMembers)
                ?? TryReadStringByNames(item, OptionLabelMemberNames)
                ?? item.GetType().Name;

            items.Add(new SocsShopItemSnapshot
            {
                Index = index++,
                Id = BuildCardIdentity(item),
                Name = name,
                Price = TryReadIntByNames(item, PriceMemberNames),
                Kind = InferShopItemKind(item),
                Enabled = TryReadBoolByNames(item, out bool enabled, BoolStateMemberNames) ? enabled : true
            });
        }

        return items;
    }

    private static IEnumerable<object?>? TryEnumerateCardItems(object source, int maxCards, out int count)
    {
        count = TryReadCollectionCount(source);
        if (source is IEnumerable enumerable && source is not string)
        {
            return MaterializeEnumerable(enumerable, maxCards, ref count);
        }

        foreach (string memberName in ZoneMemberKeywords.Concat(["Items", "items", "List", "list", "Cards", "cards"]))
        {
            object? nested = TryReadMember(source, memberName);
            if (nested is not IEnumerable nestedEnumerable || nested is string)
            {
                continue;
            }

            if (count < 0)
            {
                count = TryReadCollectionCount(nested);
            }

            return MaterializeEnumerable(nestedEnumerable, maxCards, ref count);
        }

        return null;
    }

    private static IEnumerable<object?> MaterializeEnumerable(IEnumerable enumerable, int maxCards, ref int count)
    {
        List<object?> items = [];
        foreach (object? item in enumerable)
        {
            items.Add(item);
            if (count < 0 && items.Count > maxCards)
            {
                count = items.Count;
                return items;
            }
        }

        if (count < 0)
        {
            count = items.Count;
        }

        return items;
    }

    private static int TryReadCollectionCount(object source)
    {
        foreach (string memberName in CollectionCountMembers)
        {
            if (TryReadInt(source, memberName, out int value))
            {
                return value;
            }
        }

        return -1;
    }

    private static bool LooksLikeCard(object value)
    {
        return ContainsAnyKeyword(value.GetType().Name, CardTypeKeywords)
            || ContainsAnyKeyword(value.GetType().FullName, CardTypeKeywords)
            || TryReadStringByNames(value, CardIdMembers) != null
            || TryReadStringByNames(value, CardNameMembers) != null;
    }

    private static bool LooksLikeEnemy(object value)
    {
        return ContainsAnyKeyword(value.GetType().Name, EnemyTypeKeywords)
            || ContainsAnyKeyword(value.GetType().FullName, EnemyTypeKeywords)
            || TryReadStringByNames(value, CardNameMembers) != null && TryReadIntByNames(value, "CurrentHealth", "CurrentHp", "Health", "Hp") != null;
    }

    private static List<SocsHandCardSnapshot> BuildZoneSnapshot(SnapshotProbeContext context, string cacheKey, string[] zoneNames, int maxCards)
    {
        object? zoneSource = FindZoneSource(context, cacheKey, zoneNames, maxCards);
        if (zoneSource != null && TryBuildCardSnapshot(zoneSource, maxCards, out List<SocsHandCardSnapshot> directSnapshot, out _))
        {
            return directSnapshot;
        }

        if (TryGetCachedZoneSnapshot(cacheKey, out List<SocsHandCardSnapshot> cachedSnapshot))
        {
            return cachedSnapshot;
        }

        if (TryGetZoneSnapshotFromDiscovery(cacheKey, out List<SocsHandCardSnapshot> refreshedSnapshot))
        {
            return refreshedSnapshot;
        }

        return [];
    }

    private static List<SocsHandCardSnapshot> BuildHandSnapshotFromSource(object handSource)
    {
        return TryBuildCardSnapshot(handSource, MaxPlausibleHandCards, out List<SocsHandCardSnapshot> snapshot, out _)
            ? snapshot
            : [];
    }

    private static List<SocsHandCardSnapshot> BuildHandSnapshot(SnapshotProbeContext context)
    {
        return BuildZoneSnapshot(context, "hand", HandParentNames, MaxPlausibleHandCards);
    }

    private static bool TryFindZonePath(object gameRoot, string cacheKey, string[] zoneNames, int maxCards, out SocsMemberPath? path, out object? value)
    {
        if (TryFindDistinctZonePath(gameRoot, cacheKey, out path, out value))
        {
            return true;
        }

        if (TryFindAnyAcceptedZonePath(gameRoot, cacheKey, out path, out value))
        {
            LogZoneAliasFallback(cacheKey, path!);
            return true;
        }

        path = null;
        value = null;
        return false;
    }

    private static void TryCacheZonePath(string cacheKey, object gameRoot, string[] zoneNames, int maxCards)
    {
        if (CachedPaths.ContainsKey(cacheKey) || !TryFindZonePath(gameRoot, cacheKey, zoneNames, maxCards, out SocsMemberPath? path, out object? zoneSource) || path == null)
        {
            return;
        }

        RemoveZoneAliasConflicts(cacheKey, path);
        CachedPaths[cacheKey] = path;
        LogDiscovery($"[SOCS] [DISCOVERY] Cached {cacheKey}: {path.DisplayPath} => {zoneSource?.GetType().FullName ?? "<null>"}");
    }

    private static bool IsZoneLeafName(string leafName)
    {
        return HandParentNames.Any(name => string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase))
            || DrawPileNames.Any(name => string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase))
            || DiscardPileNames.Any(name => string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase))
            || ExhaustPileNames.Any(name => string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsZoneCandidatePath(SocsMemberPath path)
    {
        return path.Members.Any(member => ContainsAnyKeyword(member.Name, ZoneMemberKeywords))
            || IsZoneLeafName(path.LeafName);
    }

    private static bool IsLikelyPlayerScopePath(SocsMemberPath path)
    {
        return path.Members.Any(member => PlayerParentNames.Any(name => string.Equals(name, member.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsBattleZonePath(SocsMemberPath path)
    {
        return IsZoneCandidatePath(path) && IsLikelyPlayerScopePath(path);
    }

    private static IEnumerable<(SocsMemberPath Path, object Value)> EnumerateBattleZonePaths(object root)
    {
        foreach ((SocsMemberPath path, object value) in EnumerateDiscoveryPaths(root))
        {
            if (IsBattleZonePath(path))
            {
                yield return (path, value);
            }
        }
    }

    private static void LogBattleZoneTopology(object root)
    {
        if (!IsDiscoveryLoggingEnabled())
        {
            return;
        }

        int logged = 0;
        foreach ((SocsMemberPath path, object value) in EnumerateBattleZonePaths(root))
        {
            LogDiscovery($"[SOCS] [ZONE] {path.DisplayPath} ({value.GetType().FullName}) => {FormatDiscoveryValue(value)}");
            logged++;
            if (logged >= 64)
            {
                break;
            }
        }
    }

    private static void LogResolvedZoneState(object root)
    {
        if (!IsDiscoveryLoggingEnabled())
        {
            return;
        }

        foreach ((string key, string[] names, int maxCards) in new[]
                 {
                     ("hand", HandParentNames, MaxPlausibleHandCards),
                     ("drawPile", DrawPileNames, MaxPlausibleZoneCards),
                     ("discardPile", DiscardPileNames, MaxPlausibleZoneCards),
                     ("exhaustPile", ExhaustPileNames, MaxPlausibleZoneCards)
                 })
        {
            if (TryFindZonePath(root, key, names, maxCards, out SocsMemberPath? path, out object? value) && path != null)
            {
                LogDiscovery($"[SOCS] [ZONE] resolved {key}: {path.DisplayPath} => {value?.GetType().FullName ?? "<null>"}");
            }
        }
    }

    private static void TryCacheBattleZonePaths(object root)
    {
        TryCacheZonePath("hand", root, HandParentNames, MaxPlausibleHandCards);
        TryCacheZonePath("drawPile", root, DrawPileNames, MaxPlausibleZoneCards);
        TryCacheZonePath("discardPile", root, DiscardPileNames, MaxPlausibleZoneCards);
        TryCacheZonePath("exhaustPile", root, ExhaustPileNames, MaxPlausibleZoneCards);
    }

    private static bool HasCompleteBattleZoneCache()
    {
        return CachedPaths.ContainsKey("hand")
            && CachedPaths.ContainsKey("drawPile")
            && CachedPaths.ContainsKey("discardPile")
            && CachedPaths.ContainsKey("exhaustPile");
    }

    private static bool HasCompleteDiscoveryCache()
    {
        return CachedPaths.ContainsKey("hp")
            && CachedPaths.ContainsKey("gold")
            && CachedPaths.ContainsKey("floor")
            && HasCompleteBattleZoneCache();
    }

    private static bool TryFindAnyCardZonePath(object gameRoot, out SocsMemberPath? path, out object? value)
    {
        return TryFindZonePath(gameRoot, "hand", HandParentNames, MaxPlausibleHandCards, out path, out value)
            || TryFindZonePath(gameRoot, "drawPile", DrawPileNames, MaxPlausibleZoneCards, out path, out value)
            || TryFindZonePath(gameRoot, "discardPile", DiscardPileNames, MaxPlausibleZoneCards, out path, out value)
            || TryFindZonePath(gameRoot, "exhaustPile", ExhaustPileNames, MaxPlausibleZoneCards, out path, out value);
    }

    private static void CacheBattleZoneDiagnostics(object root)
    {
        LogBattleZoneTopology(root);
        LogResolvedZoneState(root);
    }

    private static void EnsureDiscoveryCache()
    {
        if (_framesSinceInit < SnapshotStartDelayFrames || _framesSinceInit < _nextDiscoveryFrame)
        {
            return;
        }

        bool foundRoot = false;
        bool cacheComplete = HasCompleteDiscoveryCache();

        foreach (object root in EnumerateDiscoveryRoots())
        {
            foundRoot = true;

            if (!_discoveryTopologyLogged)
            {
                LogDiscoveryTopology(root);
            }

            TryCacheIntPath("hp", root, "CurrentHealth", "CurrentHp", "Health", "Hp");
            TryCacheIntPath("gold", root, "Gold", "gold");
            TryCacheIntPath("floor", root, "Floor", "floor", "CurrentFloor", "ActFloor");
            TryCacheBattleZonePaths(root);

            if (!_discoveryTopologyLogged)
            {
                CacheBattleZoneDiagnostics(root);
            }

            if (HasCompleteDiscoveryCache())
            {
                cacheComplete = true;
                break;
            }
        }

        if (!foundRoot)
        {
            _nextDiscoveryFrame = _framesSinceInit + DiscoveryRetryFrameInterval;
            return;
        }

        _discoveryTopologyLogged = true;
        _nextDiscoveryFrame = cacheComplete
            ? int.MaxValue
            : _framesSinceInit + DiscoveryRetryFrameInterval;
    }

    private static int? TryGetCachedInt(string key)
    {
        if (!CachedPaths.TryGetValue(key, out SocsMemberPath? path))
        {
            return null;
        }

        if (TryReadPathValue(path, out object? value) && TryConvertToInt(value, out int converted))
        {
            return converted;
        }

        LogDiscoveryWarning($"[SOCS] [DISCOVERY] Cached path invalidated for {key}: {path.DisplayPath}");
        CachedPaths.Remove(key);
        _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
        return null;
    }

    private static void TryCacheIntPath(string key, object gameRoot, params string[] terminalNames)
    {
        if (CachedPaths.ContainsKey(key))
        {
            return;
        }

        if (!TryFindMemberPath(gameRoot, terminalNames, out SocsMemberPath? path, out object? value) || path == null)
        {
            return;
        }

        CachedPaths[key] = path;
        LogDiscovery($"[SOCS] [DISCOVERY] Cached {key}: {path.DisplayPath} => {FormatDiscoveryValue(value)}");
    }

    private static bool TryFindMemberPath(object gameRoot, string[] terminalNames, out SocsMemberPath? path, out object? value)
    {
        foreach ((SocsMemberPath candidatePath, object candidateValue) in EnumerateDiscoveryPaths(gameRoot))
        {
            if (!terminalNames.Any(name => string.Equals(name, candidatePath.LeafName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!TryConvertToInt(candidateValue, out _))
            {
                continue;
            }

            path = candidatePath;
            value = candidateValue;
            return true;
        }

        path = null;
        value = null;
        return false;
    }

    private static bool IsDiscoveryLoggingEnabled()
    {
        return !_discoveryTopologyLogged;
    }

    private static void LogDiscovery(string message)
    {
        if (IsDiscoveryLoggingEnabled())
        {
            GD.Print(message);
        }
    }

    private static void LogDiscoveryWarning(string message)
    {
        if (IsDiscoveryLoggingEnabled())
        {
            GD.PushWarning(message);
        }
    }

    private static IEnumerable<(SocsMemberPath Path, object Value)> EnumerateDiscoveryPaths(object root)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<(SocsMemberPath Path, object Value, int Depth)>();
        queue.Enqueue((SocsMemberPath.CreateRoot(root), root, 0));

        int processed = 0;
        while (queue.Count > 0 && processed < DiscoveryPathMaxNodes)
        {
            (SocsMemberPath path, object value, int depth) = queue.Dequeue();
            int identity = RuntimeHelpers.GetHashCode(value);
            if (!visited.Add(identity))
            {
                continue;
            }

            processed++;
            if (depth >= DiscoveryPathMaxDepth)
            {
                continue;
            }

            foreach ((MemberInfo member, object memberValue) in EnumerateMemberValues(value))
            {
                var nextPath = path.Append(member);
                yield return (nextPath, memberValue);

                if (!ShouldTraverse(memberValue))
                {
                    continue;
                }

                bool shouldExpand = depth < 2 || ShouldExpandDiscoveryMember(member, memberValue);
                if (shouldExpand)
                {
                    queue.Enqueue((nextPath, memberValue, depth + 1));
                }
            }
        }
    }

    private static bool TryReadPathValue(SocsMemberPath path, out object? value)
    {
        object? current = path.Root;
        foreach (MemberInfo member in path.Members)
        {
            if (current == null)
            {
                value = null;
                return false;
            }

            current = member switch
            {
                PropertyInfo property => property.GetValue(current),
                FieldInfo field => field.GetValue(current),
                _ => null
            };
        }

        value = current;
        return current != null;
    }

    private static void LogDiscoveryTopology(object gameRoot)
    {
        string rootName = gameRoot.GetType().Name;
        GD.Print($"[SOCS] [DISCOVERY] Root {rootName} => {gameRoot.GetType().FullName}");
        int loggedMembers = 0;
        foreach ((SocsMemberPath path, object value) in EnumerateDiscoveryMembers(gameRoot))
        {
            GD.Print($"[SOCS] [DISCOVERY] {path.DisplayPath} ({value.GetType().FullName}) => {FormatDiscoveryValue(value)}");
            loggedMembers++;
            if (loggedMembers >= 128)
            {
                GD.Print($"[SOCS] [DISCOVERY] Topology log truncated for {rootName} after {loggedMembers} members");
                break;
            }
        }
    }

    private static IEnumerable<(SocsMemberPath Path, object Value)> EnumerateDiscoveryMembers(object root)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<(SocsMemberPath Path, object Value, int Depth)>();
        queue.Enqueue((SocsMemberPath.CreateRoot(root), root, 0));

        while (queue.Count > 0)
        {
            (SocsMemberPath path, object value, int depth) = queue.Dequeue();
            int identity = RuntimeHelpers.GetHashCode(value);
            if (!visited.Add(identity))
            {
                continue;
            }

            if (depth >= 2)
            {
                continue;
            }

            foreach ((MemberInfo member, object memberValue) in EnumerateMemberValues(value))
            {
                var nextPath = path.Append(member);
                yield return (nextPath, memberValue);

                if (depth == 0 && ShouldExpandDiscoveryMember(member, memberValue))
                {
                    queue.Enqueue((nextPath, memberValue, depth + 1));
                }
            }
        }
    }

    private static IEnumerable<(MemberInfo Member, object Value)> EnumerateMemberValues(object source)
    {
        foreach (PropertyInfo property in source.GetType().GetProperties(ReflectionFlags))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(source);
            }
            catch
            {
                continue;
            }

            if (value != null)
            {
                yield return (property, value);
            }
        }

        foreach (FieldInfo field in source.GetType().GetFields(ReflectionFlags))
        {
            object? value;
            try
            {
                value = field.GetValue(source);
            }
            catch
            {
                continue;
            }

            if (value != null)
            {
                yield return (field, value);
            }
        }
    }

    private static bool ShouldExpandDiscoveryMember(MemberInfo member, object value)
    {
        return ContainsAnyKeyword(member.Name, DiscoveryMemberKeywords)
            || ContainsAnyKeyword(value.GetType().Name, DiscoveryMemberKeywords)
            || ContainsAnyKeyword(value.GetType().FullName, DiscoveryMemberKeywords);
    }

    private static string FormatDiscoveryValue(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        return value switch
        {
            string text => text,
            bool boolean => boolean ? "true" : "false",
            int or long or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? "<value>",
            _ => value.GetType().FullName ?? value.ToString() ?? "<object>"
        };
    }

    private static bool TryConvertToInt(object? rawValue, out int value)
    {
        value = default;
        if (rawValue == null)
        {
            return false;
        }

        switch (rawValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = (int)longValue;
                return true;
            case float floatValue:
                value = (int)floatValue;
                return true;
            case double doubleValue:
                value = (int)doubleValue;
                return true;
            default:
                return int.TryParse(rawValue.ToString(), out value);
        }
    }

    private static string DetectScreen(SnapshotProbeContext context)
    {
        if (LooksLikeActiveScreen(context.Shop))
        {
            return "shop";
        }

        if (LooksLikeActiveScreen(context.Event))
        {
            return "event";
        }

        if (context.Combat != null)
        {
            return "combat";
        }

        return "unknown";
    }

    private static bool LooksLikeActiveScreen(object? source)
    {
        if (source == null)
        {
            return false;
        }

        if (TryReadBoolByNames(source, out bool visible, "Visible", "visible", "IsVisible", "isVisible", "Open", "open", "Active", "active"))
        {
            return visible;
        }

        return true;
    }

    private static SocsShopSnapshot? BuildShopSnapshot(SnapshotProbeContext context, string screen)
    {
        if (!string.Equals(screen, "shop", StringComparison.OrdinalIgnoreCase) && context.Shop == null)
        {
            return null;
        }

        object? source = context.Shop;
        if (source == null)
        {
            return null;
        }

        List<SocsShopItemSnapshot> items = BuildShopItems(source, context.Roots);
        SocsOptionSnapshot? leaveOption = BuildLeaveOption(source, context.Roots);
        if (items.Count == 0 && leaveOption == null)
        {
            return null;
        }

        return new SocsShopSnapshot
        {
            Items = items,
            LeaveOption = leaveOption
        };
    }

    private static SocsEventSnapshot? BuildEventSnapshot(SnapshotProbeContext context, string screen)
    {
        if (!string.Equals(screen, "event", StringComparison.OrdinalIgnoreCase) && context.Event == null)
        {
            return null;
        }

        object? source = context.Event;
        if (source == null)
        {
            return null;
        }

        List<SocsOptionSnapshot> options = BuildOptionsSnapshot(source, context.Roots);
        string? eventName = TryReadStringByNames(source, "EventName", "eventName", "Name", "name", "Title", "title")
            ?? source.GetType().Name;

        if (string.IsNullOrWhiteSpace(eventName) && options.Count == 0)
        {
            return null;
        }

        return new SocsEventSnapshot
        {
            EventName = eventName,
            Options = options
        };
    }


    private static SocsOptionSnapshot? BuildLeaveOption(object source, IEnumerable<object> roots)
    {
        foreach (object candidate in EnumerateKnownObjects(source, roots))
        {
            foreach (string memberName in LeaveOptionMemberNames)
            {
                object? leave = TryReadMember(candidate, memberName);
                if (leave == null)
                {
                    continue;
                }

                return new SocsOptionSnapshot
                {
                    Index = 0,
                    Label = TryReadStringByNames(leave, OptionLabelMemberNames) ?? memberName,
                    Enabled = TryReadBoolByNames(leave, out bool enabled, BoolStateMemberNames) ? enabled : true
                };
            }
        }

        foreach (SocsOptionSnapshot option in BuildOptionsSnapshot(source, roots))
        {
            if (ContainsAnyKeyword(option.Label, LeaveOptionMemberNames))
            {
                return option;
            }
        }

        return null;
    }

    private static List<SocsOptionSnapshot> BuildOptionsSnapshot(object source, IEnumerable<object> roots)
    {
        List<SocsOptionSnapshot> options = [];
        int index = 0;
        foreach (object option in EnumerateCompositeItems(source, roots, OptionCollectionMemberNames, MaxPlausibleOptions))
        {
            options.Add(new SocsOptionSnapshot
            {
                Index = index++,
                Label = TryReadStringByNames(option, OptionLabelMemberNames) ?? option.GetType().Name,
                Enabled = TryReadBoolByNames(option, out bool enabled, BoolStateMemberNames) ? enabled : true
            });
        }

        return options;
    }

    private static IEnumerable<object> EnumerateCompositeItems(object source, IEnumerable<object> roots, string[] memberNames, int maxItems)
    {
        var seen = new HashSet<int>();
        int yielded = 0;

        foreach (object candidate in EnumerateKnownObjects(source, roots))
        {
            foreach (string memberName in memberNames)
            {
                object? memberValue = TryReadMember(candidate, memberName);
                if (memberValue == null)
                {
                    continue;
                }

                foreach (object? item in EnumerateObjects(memberValue))
                {
                    if (item == null)
                    {
                        continue;
                    }

                    int identity = RuntimeHelpers.GetHashCode(item);
                    if (!seen.Add(identity))
                    {
                        continue;
                    }

                    yield return item;
                    yielded++;
                    if (yielded >= maxItems)
                    {
                        yield break;
                    }
                }
            }
        }
    }

    private static IEnumerable<object> EnumerateKnownObjects(object source, IEnumerable<object> roots)
    {
        var seen = new HashSet<int>();

        foreach (object candidate in new[] { source }.Concat(roots))
        {
            int identity = RuntimeHelpers.GetHashCode(candidate);
            if (seen.Add(identity))
            {
                yield return candidate;
            }
        }
    }

    private static string InferShopItemKind(object item)
    {
        string typeName = item.GetType().Name;
        if (ContainsAnyKeyword(typeName, ["Relic"]))
        {
            return "relic";
        }

        if (ContainsAnyKeyword(typeName, ["Potion"]))
        {
            return "potion";
        }

        if (ContainsAnyKeyword(typeName, ["Card"]))
        {
            return "card";
        }

        return typeName;
    }

    private static int? TryReadIntByNames(object source, params string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            if (TryReadInt(source, memberName, out int value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryReadBoolByNames(object source, out bool value, params string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            object? rawValue = TryReadMember(source, memberName);
            if (rawValue == null)
            {
                continue;
            }

            switch (rawValue)
            {
                case bool boolValue:
                    value = boolValue;
                    return true;
                default:
                    if (bool.TryParse(rawValue.ToString(), out bool parsed))
                    {
                        value = parsed;
                        return true;
                    }
                    break;
            }
        }

        value = default;
        return false;
    }

    private static void TryAssignField(string fieldName, Action assign)
    {
        try
        {
            assign();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SOCS snapshot field '{fieldName}' failed: {ex.Message}");
        }
    }

    private static int? TryGetIntByNames(params string[] memberNames)
    {
        foreach (object root in EnumerateDiscoveryRoots())
        {
            int foundCount = 0;
            foreach (object node in EnumerateObjectGraph(root))
            {
                foundCount++;
                foreach (string memberName in memberNames)
                {
                    if (TryReadInt(node, memberName, out int value))
                    {
                        GD.Print($"[SOCS] Found {memberName}={value} in {node.GetType().FullName}");
                        return value;
                    }
                }
            }
            GD.Print($"[SOCS] Searched {foundCount} nodes in root for {string.Join(",", memberNames)}");
        }

        return null;
    }

    private static bool TryFindIntInGraph(object root, string[] memberNames, out int value)
    {
        value = default;
        foreach (object node in EnumerateObjectGraph(root))
        {
            foreach (string memberName in memberNames)
            {
                if (TryReadInt(node, memberName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryMatchHandCard(int? requestedIndex, string? requestedCardId, out SocsHandCardSnapshot? matchedCard)
    {
        matchedCard = null;
        List<SocsHandCardSnapshot> hand = BuildHandSnapshot(BuildProbeContext());

        if (!string.IsNullOrWhiteSpace(requestedCardId))
        {
            matchedCard = hand.FirstOrDefault(card => string.Equals(card.Id, requestedCardId, StringComparison.OrdinalIgnoreCase));
            if (matchedCard != null)
            {
                return true;
            }
        }

        if (requestedIndex is >= 0 && requestedIndex < hand.Count)
        {
            matchedCard = hand[requestedIndex.Value];
            return true;
        }

        return false;
    }

    private static IEnumerable<object> EnumerateDiscoveryRoots()
    {
        var seen = new HashSet<int>();

        if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
        {
            foreach (Node child in tree.Root.GetChildren())
            {
                if (!ContainsAnyKeyword(child.Name, RootMemberNames) && !ContainsAnyKeyword(child.GetType().FullName, RootMemberNames))
                {
                    continue;
                }

                foreach (object root in EnumerateSceneCandidates(child))
                {
                    foreach (object candidate in YieldDiscoveryRoot(root, $"scene:{child.Name}", seen))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        if (_cachedSingleton != null)
        {
            foreach (object root in YieldDiscoveryRoot(_cachedSingleton, "cached singleton", seen))
            {
                yield return root;
            }
        }

        _singletonRescanCounter++;
        if (_cachedSingleton == null || _singletonRescanCounter >= SingletonRescanInterval)
        {
            _singletonRescanCounter = 0;
            object? singleton = FindStaticSingletonCandidate();
            if (singleton != null)
            {
                _cachedSingleton = singleton;
                foreach (object root in YieldDiscoveryRoot(singleton, "static singleton", seen))
                {
                    yield return root;
                }
            }
        }
    }


    private static IEnumerable<object> YieldDiscoveryRoot(object root, string source, HashSet<int> seen)
    {
        if (!ShouldTraverse(root))
        {
            yield break;
        }

        int identity = RuntimeHelpers.GetHashCode(root);
        if (!seen.Add(identity))
        {
            yield break;
        }

        LogDiscovery($"[SOCS] [DISCOVERY] Root candidate ({source}): {root.GetType().FullName}");
        yield return root;
    }

    private static IEnumerable<object> EnumerateSceneCandidates(Node root)
    {
        yield return root;

        foreach (object? direct in EnumerateRootMembers(root))
        {
            if (direct != null)
            {
                yield return direct;
            }
        }

        foreach (Node child in root.GetChildren())
        {
            yield return child;

            foreach (object? direct in EnumerateRootMembers(child))
            {
                if (direct != null)
                {
                    yield return direct;
                }
            }
        }
    }

    private static IEnumerable<object?> EnumerateRootMembers(object source)
    {
        foreach (string memberName in RootMemberNames)
        {
            yield return TryReadMember(source, memberName);
        }

        object? playerLike = TryFindMember(source, PlayerParentNames);
        if (playerLike != null)
        {
            foreach (string memberName in RootMemberNames)
            {
                yield return TryReadMember(playerLike, memberName);
            }
        }
    }

    private static IEnumerable<object> EnumerateObjectGraph(object root)
    {
        var queue = new Queue<(object Node, int Depth)>();
        var visited = new HashSet<int>();
        queue.Enqueue((root, 0));

        int processed = 0;
        while (queue.Count > 0 && processed < DiscoveryMaxNodes)
        {
            (object node, int depth) = queue.Dequeue();
            if (!ShouldTraverse(node))
            {
                continue;
            }

            int identity = RuntimeHelpers.GetHashCode(node);
            if (!visited.Add(identity))
            {
                continue;
            }

            processed++;
            yield return node;

            if (depth >= DiscoveryMaxDepth)
            {
                continue;
            }

            foreach (object child in EnumerateChildObjects(node))
            {
                queue.Enqueue((child, depth + 1));
            }
        }
    }

    private static IEnumerable<object> EnumerateChildObjects(object source)
    {
        if (source is Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                yield return child;
            }
        }

        foreach (PropertyInfo property in source.GetType().GetProperties(ReflectionFlags))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(source);
            }
            catch
            {
                continue;
            }

            foreach (object child in ExpandChildValue(value))
            {
                yield return child;
            }
        }

        foreach (FieldInfo field in source.GetType().GetFields(ReflectionFlags))
        {
            object? value;
            try
            {
                value = field.GetValue(source);
            }
            catch
            {
                continue;
            }

            foreach (object child in ExpandChildValue(value))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<object> ExpandChildValue(object? value)
    {
        if (value == null || value is string)
        {
            yield break;
        }

        if (value is IEnumerable enumerable && value is not IDictionary)
        {
            foreach (object? item in enumerable)
            {
                if (item != null && ShouldTraverse(item))
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (ShouldTraverse(value))
        {
            yield return value;
        }
    }

    private static bool ShouldTraverse(object value)
    {
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return false;
        }

        string? fullName = type.FullName;
        if (!string.IsNullOrEmpty(fullName) && (fullName.StartsWith("System.Reflection", StringComparison.Ordinal) || fullName.StartsWith("System.Runtime", StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static object? FindStaticSingletonCandidate()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
            {
                if (!type.IsClass)
                {
                    continue;
                }

                if (!ContainsAnyKeyword(type.Name, DungeonSingletonKeywords) && !ContainsAnyKeyword(type.FullName, DungeonSingletonKeywords))
                {
                    continue;
                }

                object? singleton = TryReadStaticSingleton(type);
                if (singleton != null && ShouldTraverse(singleton))
                {
                    LogDiscovery($"[SOCS] [DISCOVERY] Found singleton candidate: {type.FullName} -> {singleton.GetType().FullName}");
                    return singleton;
                }
            }
        }

        LogDiscovery("[SOCS] [DISCOVERY] No singleton candidate found");
        return null;
    }

    private static object? TryReadStaticSingleton(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(ReflectionFlags))
        {
            if (!property.GetMethod?.IsStatic ?? true)
            {
                continue;
            }

            if (!ContainsAnyKeyword(property.Name, StaticSingletonMemberNames) && !ContainsAnyKeyword(property.Name, DungeonSingletonKeywords))
            {
                continue;
            }

            try
            {
                if (property.GetValue(null) is { } value)
                {
                    return value;
                }
            }
            catch
            {
            }
        }

        foreach (FieldInfo field in type.GetFields(ReflectionFlags))
        {
            if (!field.IsStatic)
            {
                continue;
            }

            if (!ContainsAnyKeyword(field.Name, StaticSingletonMemberNames) && !ContainsAnyKeyword(field.Name, DungeonSingletonKeywords))
            {
                continue;
            }

            try
            {
                if (field.GetValue(null) is { } value)
                {
                    return value;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool ContainsAnyKeyword(string? source, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        foreach (string keyword in keywords)
        {
            if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static object? TryFindNestedMember(object source, string[] parentNames, string[] memberNames)
    {
        foreach (string parentName in parentNames)
        {
            object? parent = TryReadMember(source, parentName);
            if (parent == null)
            {
                continue;
            }

            object? nested = TryFindMember(parent, memberNames);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static object? TryFindMember(object source, string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            object? value = TryReadMember(source, memberName);
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<object?> EnumerateObjects(object source)
    {
        if (source is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        foreach (string memberName in new[] { "Cards", "cards", "Items", "items", "List", "list" })
        {
            if (TryReadMember(source, memberName) is IEnumerable nestedEnumerable)
            {
                foreach (object? item in nestedEnumerable)
                {
                    yield return item;
                }

                yield break;
            }
        }
    }

    private static string BuildCardIdentity(object card)
    {
        string? explicitId = TryReadStringByNames(card, CardIdMembers);
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return explicitId;
        }

        return $"mem-{RuntimeHelpers.GetHashCode(card):x8}";
    }

    private static string? TryReadStringByNames(object source, params string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            object? value = TryReadMember(source, memberName);
            if (value == null)
            {
                continue;
            }

            string? text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static bool TryReadInt(object source, string memberName, out int value)
    {
        value = default;
        object? rawValue = TryReadMember(source, memberName);
        return TryConvertToInt(rawValue, out value);
    }

    private static object? TryReadMember(object source, string memberName)
    {
        Type type = source.GetType();

        PropertyInfo? property = type.GetProperty(memberName, ReflectionFlags);
        if (property != null)
        {
            return property.GetValue(source);
        }

        FieldInfo? field = type.GetField(memberName, ReflectionFlags);
        if (field != null)
        {
            return field.GetValue(source);
        }

        return null;
    }

    private static float LoadSavedSpeed()
    {
        try
        {
            if (File.Exists(ConfigPath) && float.TryParse(File.ReadAllText(ConfigPath), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return Mathf.Clamp(parsed, SocsConstants.MinTimeScale, SocsConstants.MaxTimeScale);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SOCS config load warning: {ex.Message}");
        }

        return SocsConstants.DefaultTimeScale;
    }

    private static void SaveSpeed(float speed)
    {
        try
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ConfigPath, speed.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SOCS config save warning: {ex.Message}");
        }
    }
}

internal readonly record struct SocsPendingCommand(SocsInboundCommand Command, SocsClientConnection Client);

internal readonly record struct SnapshotProbeContext(
    object[] Roots,
    object? Run,
    object? PlayerManager,
    object? Player,
    object? Combat,
    object? Shop,
    object? Event
);

internal sealed class SocsMemberPath
{
    public SocsMemberPath(object root, IReadOnlyList<MemberInfo> members, string displayPath)
    {
        Root = root;
        Members = members;
        DisplayPath = displayPath;
    }

    public object Root { get; }
    public IReadOnlyList<MemberInfo> Members { get; }
    public string DisplayPath { get; }
    public string LeafName => Members.Count == 0 ? Root.GetType().Name : Members[^1].Name;

    public static SocsMemberPath CreateRoot(object root)
    {
        return new SocsMemberPath(root, Array.Empty<MemberInfo>(), root.GetType().Name);
    }

    public SocsMemberPath Append(MemberInfo member)
    {
        var members = Members.ToList();
        members.Add(member);
        return new SocsMemberPath(Root, members, $"{DisplayPath}.{member.Name}");
    }
}
