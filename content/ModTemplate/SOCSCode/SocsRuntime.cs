using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
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
    private static readonly string[] CardNameMembers = ["Name", "name", "CardName", "DisplayName", "Title", "title", "LocalizedName", "localizedName", "DisplayText", "displayText"];
    private static readonly string[] EnemyNameMembers = ["DisplayName", "displayName", "LocalizedName", "localizedName", "Name", "name", "Title", "title", "CharacterName", "characterName", "MonsterName", "monsterName", "EnemyName", "enemyName"];
    private static readonly string[] CardIdMembers = ["Uuid", "UUID", "Guid", "guid", "Id", "id", "InstanceId", "instanceId", "CardId", "cardId", "Key", "key", "Slug", "slug"];
    private static readonly string[] CollectionCountMembers = ["Count", "count", "Length", "length", "Size", "size"];
    private static readonly string[] CardTypeKeywords = ["Card"];
    private static readonly string[] EnemyTypeKeywords = ["Enemy", "Monster", "Creature", "Opponent", "Combatant", "Unit", "Actor"];
    private static readonly string[] ZoneMemberKeywords = ["Hand", "Draw", "Deck", "Discard", "Exhaust", "Pile"];
    private static readonly string[] EnergyMemberNames = ["Energy", "energy", "CurrentEnergy", "currentEnergy", "EnergyCount", "energyCount"];
    private static readonly string[] EnergyContainerNames = ["EnergyManager", "energyManager", "EnergyPanel", "energyPanel", "Mana", "mana"];
    private static readonly string[] BlockMemberNames = ["Block", "block", "CurrentBlock", "currentBlock", "Armor", "armor", "BlockAmount", "blockAmount", "Defense", "defense"];
    private static readonly string[] PotionCollectionNames = ["Potions", "potions", "PotionBelt", "potionBelt", "Consumables", "consumables"];
    private static readonly string[] PotionCapacityNames = ["PotionCapacity", "potionCapacity", "MaxPotions", "maxPotions"];
    private static readonly string[] PotionUsableNames = ["CanUse", "canUse", "IsUsable", "isUsable", "Playable", "playable"];
    private static readonly string[] EnemyCollectionMemberNames = ["Enemies", "enemies", "Monsters", "monsters", "Creatures", "creatures", "Combatants", "combatants", "Opponents", "opponents", "Units", "units", "Actors", "actors", "Entities", "entities", "Slots", "slots", "Wave", "wave", "EncounterEntities", "encounterEntities"];
    private static readonly string[] AliveMemberNames = ["Alive", "alive", "IsAlive", "isAlive", "Dead", "dead", "IsDead", "isDead"];
    private static readonly string[] PlayableMemberNames = ["Playable", "playable", "IsPlayable", "isPlayable", "CanPlay", "canPlay", "CanUse", "canUse", "CanCast", "canCast"];
    private static readonly string[] CurrentCostMemberNames = ["CostForTurn", "costForTurn", "CurrentCost", "currentCost", "TurnCost", "turnCost", "DisplayedCost", "displayedCost", "EnergyCostForTurn", "energyCostForTurn"];
    private static readonly string[] CostMemberNames = ["Cost", "cost", "EnergyCost", "energyCost", "ManaCost", "manaCost", "BaseCost", "baseCost", "BaseEnergyCost", "baseEnergyCost", "PrintedCost", "printedCost"];
    private static readonly string[] XCostMemberNames = ["IsXCost", "isXCost", "XCost", "xCost", "VariableCost", "variableCost"];
    private static readonly string[] UpgradeMemberNames = ["Upgraded", "upgraded", "IsUpgraded", "isUpgraded", "UpgradedThisCombat", "upgradedThisCombat"];
    private static readonly string[] CardTypeMemberNames = ["Type", "type", "CardType", "cardType"];
    private static readonly string[] DamageMemberNames = ["BaseDamage", "baseDamage", "Damage", "damage", "DisplayedDamage", "displayedDamage"];
    private static readonly string[] DescriptionMemberNames = ["Description", "description", "RawDescription", "rawDescription", "Text", "text", "Body", "body"];
    private static readonly string[] ShallowWrapperMemberNames = ["Data", "data", "State", "state", "Info", "info", "Definition", "definition", "Details", "details", "Runtime", "runtime", "RuntimeData", "runtimeData", "RuntimeState", "runtimeState", "CardData", "cardData", "CardState", "cardState", "Entity", "entity"];
    private static readonly string[] TextValueMemberNames = ["Text", "text", "Value", "value", "Content", "content", "RawText", "rawText"];
    private static readonly string[] PowerCollectionMemberNames = ["Powers", "powers", "Statuses", "statuses", "Buffs", "buffs", "Debuffs", "debuffs", "Effects", "effects"];
    private static readonly string[] PowerIdMemberNames = ["Id", "id", "PowerId", "powerId", "Name", "name", "Key", "key"];
    private static readonly string[] PowerAmountMemberNames = ["Amount", "amount", "Stacks", "stacks", "Count", "count", "Value", "value"];
    private static readonly string[] IntentTypeMemberNames = ["Intent", "intent", "IntentType", "intentType", "MoveType", "moveType", "MoveIntent", "moveIntent", "Type", "type", "Kind", "kind"];
    private static readonly string[] IntentDamageMemberNames = ["IntentDamage", "intentDamage", "MoveDamage", "moveDamage", "Damage", "damage", "IntentDmg", "intentDmg", "BaseDamage", "baseDamage"];
    private static readonly string[] IntentMultiMemberNames = ["IntentMulti", "intentMulti", "MultiCount", "multiCount", "HitCount", "hitCount", "Multiplier", "multiplier", "Count", "count", "Hits", "hits"];
    private static readonly string[] IntentCarrierMemberNames = ["Intent", "intent", "IntentData", "intentData", "CurrentIntent", "currentIntent", "NextIntent", "nextIntent", "Move", "move", "NextMove", "nextMove", "PlannedMove", "plannedMove", "IntentState", "intentState"];
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
    private static readonly string[] EndTurnMethodNames = ["EndTurnPhaseOne", "EndTurn", "TryEndTurn", "OnPlayerEndTurnPing", "SendEndTurnPing"];
    private static readonly string[] PlayCardMethodNames = ["TryPlayCard", "PlayCard", "QueuePlayCard", "EnqueueCardPlay", "HandleRequestEnqueueActionMessage"];
    private static readonly string[] ChooseOptionMethodNames = ["ChooseOption", "SelectOption", "HandleOptionSelected", "OnOptionSelected", "ProceedFromTerminalRewardsScreen", "ClickEventProceedIfNeeded", "ClickRestSiteProceedIfNeeded", "OnProceedButtonPressed"];
    private static readonly string[] TargetSelectionMethodNames = ["SelectTarget", "ChooseTarget", "StartTargeting", "FinishTargeting", "ConfirmTarget", "AllowedToTargetCreature"];
    private static readonly string[] TargetCollectionMemberNames = ["Enemies", "enemies", "Monsters", "monsters", "Creatures", "creatures", "Combatants", "combatants", "Opponents", "opponents", "Targets", "targets", "ValidTargets", "validTargets"];
    private static readonly string[] TargetingStateMemberNames = ["TargetSelection", "targetSelection", "SingleCreatureTargeting", "singleCreatureTargeting", "MultiCreatureTargeting", "multiCreatureTargeting", "TargetManager", "targetManager"];
    private static readonly string[] PendingStateMemberNames = ["PendingState", "pendingState", "SelectionState", "selectionState", "InputState", "inputState"];
    private static readonly string[] DebugStateMemberNames = ["State", "state", "Mode", "mode", "ScreenState", "screenState"];
    private static readonly string[] VisibleMemberNames = ["Visible", "visible", "IsVisible", "isVisible", "Open", "open", "Active", "active"];
    private static readonly string[] SelectedTargetMemberNames = ["SelectedTarget", "selectedTarget", "CurrentTarget", "currentTarget", "HoveredTarget", "hoveredTarget"];
    private static readonly string[] LegalActionsMemberNames = ["LegalActions", "legalActions", "AvailableActions", "availableActions", "Commands", "commands"];
    private static readonly string[] ProceedScreenNames = ["Reward", "Rewards", "RewardScreen", "Rest", "RestScreen", "Campfire", "CampfireScreen", "Selection", "Choice"];
    private const int MaxDiagnosticEntries = 8;
    private const string SocsLogPrefix = "[SOCS]";
    private const string SocsDiagPrefix = "[SOCS][DIAG]";
    private const string SocsActionPrefix = "[SOCS][ACTION]";
    private const string SocsExecPrefix = "[SOCS][EXEC]";
    private const string SocsStatePrefix = "[SOCS][STATE]";
    private const string SocsProbePrefix = "[SOCS][PROBE]";
    private const string DiagnosticSectionSeparator = " | ";
    private static int _diagnosticSequence;
    private static string? _lastDiagnosticSummary;
    private static string? _lastProbeSummary;
    private static string? _lastSelectionSummary;
    private static string? _lastCommandSummary;
    private static string? _lastScreenSummary;
    private static string? _lastActionabilitySummary;
    private static string? _lastPendingSummary;
    private static string? _lastExecutionSummary;
    private static string? _lastPlayCardProbeSummary;
    private static string? _lastChooseOptionProbeSummary;
    private static string? _lastEndTurnProbeSummary;
    private static string? _lastSelectTargetProbeSummary;
    private static string? _lastTargetSummary;
    private static string? _lastExceptionSummary;
    private static string? _lastReflectionTargetSummary;
    private static string? _lastOptionSummary;
    private static string? _lastSnapshotSummary;
    private static string? _lastRunMetaSummary;
    private static string? _lastCombatSummary;
    private static string? _lastCommandPayloadSummary;
    private static string? _lastExecutionAttemptSummary;
    private static string? _lastTargetingProbeSummary;
    private static string? _lastSelectionProbeSummary;
    private static string? _lastAvailableCommandSummary;
    private static string? _lastPendingReasonSummary;
    private static string? _lastDetectScreenReason;
    private static string? _lastDeferredActionSummary;
    private static string? _lastResolvedTargetSummary;
    private static string? _lastResolvedOptionSummary;
    private static string? _lastResolvedCardSummary;
    private static string? _lastCommandResultSummary;
    private static string? _lastDispatchSummary;
    private static bool _cardProbeDiagnosticsLogged;
    private static bool _enemyProbeDiagnosticsLogged;
    private static int _currentCardProbeDiagnosticFrame = -1;
    private static int _currentEnemyProbeDiagnosticFrame = -1;
    private static int _cardProbeDiagnosticCount;
    private static int _enemyProbeDiagnosticCount;
    private static string? _lastReflectionFailureSummary;
    private static string? _lastSelectionKindSummary;
    private static string? _lastProbeContextSummary;
    private static string? _lastSnapshotBuildSummary;
    private static string? _lastActionStatusSummary;
    private static string? _lastTargetRequirementSummary;
    private static string? _lastCanSummary;
    private static string? _lastCombatTargetingSummary;
    private static string? _lastMethodProbeSummary;
    private static string? _lastScreenProbeSummary;
    private static string? _lastExecutionFailureSummary;
    private static string? _lastExecutionSuccessSummary;
    private static string? _lastPayloadSummary;
    private static string? _lastCommandNameSummary;
    private static string? _lastCommandIdSummary;
    private static string? _lastProbeCandidateSummary;
    private static string? _lastBuildSelectionReason;
    private static string? _lastBuildPendingReason;
    private static string? _lastBuildActionabilityReason;
    private static string? _lastCommandIntentSummary;
    private static string? _lastTargetCandidateSummary;
    private static string? _lastOptionCandidateSummary;
    private static string? _lastMethodCandidateSummary;
    private static string? _lastActiveScreenSummary;
    private static string? _lastCommandExecutionNote;
    private static string? _lastPlayableSummary;
    private static string? _lastChooseSummary;
    private static string? _lastEndTurnSummary;
    private static string? _lastTargetingSummary;
    private static string? _lastDiagnosticDigest;
    private static string? _lastSnapshotDigest;
    private static string? _lastRootSummary;
    private static string? _lastProbeRootsSummary;
    private static string? _lastDeferredProbeSummary;
    private static string? _lastSelectionOptionsSummary;
    private static string? _lastEnemyTargetSummary;
    private static string? _lastMethodMatchSummary;
    private static string? _lastMethodInvokeSummary;
    private static string? _lastMethodParamSummary;
    private static string? _lastScreenReasonSummary;
    private static string? _lastCommandAvailabilitySummary;
    private static string? _lastTargetingReasonSummary;
    private static string? _lastProbeFailureReason;
    private static string? _lastOptionExecutionSummary;
    private static string? _lastCardExecutionSummary;
    private static string? _lastTargetExecutionSummary;
    private static string? _lastEndTurnExecutionSummary;
    private static string? _lastSnapshotActionabilityDigest;
    private static string? _lastSnapshotSelectionDigest;
    private static string? _lastSnapshotPendingDigest;
    private static string? _lastSnapshotScreenDigest;
    private static string? _lastSnapshotCombatDigest;
    private static string? _lastSnapshotRunMetaDigest;
    private static string? _lastSnapshotSystemDigest;
    private static string? _lastSnapshotEventDigest;
    private static string? _lastSnapshotShopDigest;
    private static string? _lastSnapshotMapDigest;
    private static string? _lastSnapshotRewardsDigest;
    private static string? _lastSnapshotRestDigest;
    private static string? _lastSnapshotSelectionReason;
    private static string? _lastSnapshotPendingReason;
    private static string? _lastSnapshotActionabilityReason;
    private static string? _lastSnapshotTargetingReason;
    private static string? _lastSnapshotCommandReason;
    private static string? _lastMethodSearchReason;
    private static string? _lastProbeInvocationSummary;
    private static string? _lastTargetProbeInvocationSummary;
    private static string? _lastCommandDiagnosticFooter;
    private static string? _lastCommandDiagnosticHeader;
    private static string? _lastActionabilityDiagnostic;
    private static string? _lastSelectionDiagnostic;
    private static string? _lastPendingDiagnostic;
    private static string? _lastExecutionDiagnostic;
    private static string? _lastTargetDiagnostic;
    private static string? _lastOptionDiagnostic;
    private static string? _lastScreenDiagnostic;
    private static string? _lastProbeDiagnostic;
    private static string? _lastSummaryDiagnostic;
    private static string? _lastActionCommandSummary;
    private static string? _lastReadyForCombatTestSummary;
    private static string? _lastOneShotLogDigest;
    private static bool _pendingDiagnosticDump;
    private static bool _logNextSnapshotDetails;
    private static bool _detailedCommandLoggingEnabled;
    private static bool _detailedSnapshotLoggingEnabled;
    private static bool _detailedProbeLoggingEnabled;
    private static bool _detailedSelectionLoggingEnabled;
    private static bool _detailedActionabilityLoggingEnabled;
    private static bool _detailedExecutionLoggingEnabled;
    private static bool _detailedTargetLoggingEnabled;
    private static bool _detailedOptionLoggingEnabled;
    private static bool _detailedScreenLoggingEnabled;
    private static bool _detailedPendingLoggingEnabled;
    private static bool _detailedContextLoggingEnabled;
    private static bool _detailedFailureLoggingEnabled;
    private static bool _detailedSuccessLoggingEnabled;
    private static bool _logMethodCandidates;
    private static bool _logCommandPayloads;
    private static bool _logSelectionFallbacks;
    private static bool _logProbeFailures;
    private static bool _logStateDigests;
    private static bool _logOneShotSnapshotAfterCommand = true;
    private static bool _logCombatTestReadiness;
    private static bool _logScreenDetection;
    private static bool _logActionability;
    private static bool _logPending;
    private static bool _logSelection;
    private static bool _logContextRoots;
    private static bool _logReflectionFailures;
    private static bool _logExecutionAttempts;
    private static bool _logExecutionResults;
    private static bool _logCommandDispatch;
    private static bool _logTargetResolution;
    private static bool _logOptionResolution;
    private static bool _logCardResolution;
    private static bool _logProbeContext;
    private static bool _logSnapshotDigest;
    private static bool _logSnapshotBuilders;
    private static bool _logTargetingProbe;
    private static bool _logChooseProbe;
    private static bool _logPlayCardProbe;
    private static bool _logEndTurnProbe;
    private const int MaxPlausibleHandCards = 20;
    private const int MaxPlausibleZoneCards = 200;
    private const int MaxPlausibleShopItems = 64;
    private const int MaxPlausibleOptions = 16;

    private static SocsServer? _server;
    private static bool _initialized;
    private static long _sequence;
    private static ulong _lastSnapshotFrame;
    private static int _completedSnapshotBuildCount;
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

        if (!_server.HasClients)
        {
            return;
        }

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
        LogCommandDispatch(command, commandName);
        switch (commandName)
        {
            case "ping":
                MarkActionStatus(SocsActionStatus.Success);
                SendCommandResponse(client, command, true, new { message = "pong", lastActionStatus = _lastActionStatus });
                break;

            case "set_time_scale":
                float requestedValue = command.Payload?.Value ?? SocsConstants.DefaultTimeScale;
                float clampedValue = Mathf.Clamp(requestedValue, SocsConstants.MinTimeScale, SocsConstants.MaxTimeScale);
                _targetTimeScale = clampedValue;
                Callable.From(ApplyStickyTimeScale).CallDeferred();
                SaveSpeed(_targetTimeScale);
                InvalidateDiscoveryCache("set_time_scale");
                MarkActionStatus(SocsActionStatus.Success);
                SendCommandResponse(client, command, true, new { value = _targetTimeScale, lastActionStatus = _lastActionStatus });
                break;

            case "play_card":
                HandlePlayCard(command, client);
                break;

            case "end_turn":
                HandleEndTurn(command, client);
                break;

            case "choose_option":
                HandleChooseOption(command, client);
                break;

            case "select_target":
                HandleSelectTarget(command, client);
                break;

            default:
                MarkActionStatus(SocsActionStatus.Fail);
                LogFailure($"Unknown SOCS command: {command.Name ?? "<null>"}.");
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
        int? targetIndex = command.Payload?.TargetIndex;
        string? targetId = command.Payload?.TargetId;
        bool matched = TryMatchHandCard(index, requestedCardId, out SocsHandCardSnapshot? matchedCard);
        bool requiresTarget = matchedCard != null && CardRequiresTarget(matchedCard);
        SocsEnemySnapshot? target = null;
        bool targetResolved = !requiresTarget || TryResolveTarget(targetIndex, targetId, out target);

        if (!matched)
        {
            MarkActionStatus(SocsActionStatus.Fail);
            LogFailure($"play_card match failed | index={index?.ToString() ?? "null"} | cardId={requestedCardId ?? "<null>"}");
            SendCommandResponse(client, command, false, new
            {
                requestedIndex = index,
                requestedCardId,
                targetIndex,
                targetId,
                matched = false,
                matchedCard,
                lastActionStatus = _lastActionStatus,
                diagnostics = BuildCommandDiagnostics("play_card")
            });
            return;
        }

        if (!targetResolved)
        {
            MarkActionStatus(SocsActionStatus.Fail);
            LogFailure($"play_card target resolution failed | card={FormatCard(matchedCard)} | targetIndex={targetIndex?.ToString() ?? "null"} | targetId={targetId ?? "<null>"}");
            SendCommandResponse(client, command, false, new
            {
                requestedIndex = index,
                requestedCardId,
                targetIndex,
                targetId,
                matched = true,
                matchedCard,
                targetMatched = false,
                lastActionStatus = _lastActionStatus,
                diagnostics = BuildCommandDiagnostics("play_card")
            });
            return;
        }

        _lastResolvedCardSummary = FormatCard(matchedCard);
        _lastResolvedTargetSummary = requiresTarget ? FormatEnemy(target!) : "<none>";
        QueueDeferredAction("play_card", () => ExecutePlayCard(command, matchedCard!, targetIndex, targetId, requiresTarget ? target : null), client);
    }

    private static void HandleEndTurn(SocsInboundCommand command, SocsClientConnection client)
    {
        QueueDeferredAction("end_turn", () => ExecuteEndTurn(command), client);
    }

    private static void HandleChooseOption(SocsInboundCommand command, SocsClientConnection client)
    {
        int? optionIndex = command.Payload?.OptionIndex ?? command.Payload?.Index;
        if (!TryResolveOption(optionIndex, out SocsOptionSnapshot? option))
        {
            MarkActionStatus(SocsActionStatus.Fail);
            LogFailure($"choose_option resolution failed | optionIndex={optionIndex?.ToString() ?? "null"}");
            SendCommandResponse(client, command, false, new
            {
                optionIndex,
                lastActionStatus = _lastActionStatus,
                diagnostics = BuildCommandDiagnostics("choose_option")
            });
            return;
        }

        _lastResolvedOptionSummary = FormatOption(option!);
        QueueDeferredAction("choose_option", () => ExecuteChooseOption(command, option!), client);
    }

    private static void HandleSelectTarget(SocsInboundCommand command, SocsClientConnection client)
    {
        int? targetIndex = command.Payload?.TargetIndex ?? command.Payload?.Index;
        string? targetId = command.Payload?.TargetId ?? command.Payload?.CardId;
        if (!TryResolveTarget(targetIndex, targetId, out SocsEnemySnapshot? target))
        {
            MarkActionStatus(SocsActionStatus.Fail);
            LogFailure($"select_target resolution failed | targetIndex={targetIndex?.ToString() ?? "null"} | targetId={targetId ?? "<null>"}");
            SendCommandResponse(client, command, false, new
            {
                targetIndex,
                targetId,
                lastActionStatus = _lastActionStatus,
                diagnostics = BuildCommandDiagnostics("select_target")
            });
            return;
        }

        _lastResolvedTargetSummary = FormatEnemy(target!);
        QueueDeferredAction("select_target", () => ExecuteSelectTarget(command, target!), client);
    }

    private static void QueueDeferredAction(string actionName, Func<SocsExecutionResult> execute, SocsClientConnection client)
    {
        _lastDeferredActionSummary = actionName;
        _pendingDiagnosticDump = true;
        _logNextSnapshotDetails = true;
        Callable.From(() => CompleteDeferredAction(actionName, execute, client)).CallDeferred();
    }

    private static void CompleteDeferredAction(string actionName, Func<SocsExecutionResult> execute, SocsClientConnection client)
    {
        SocsExecutionResult result;
        try
        {
            result = execute();
        }
        catch (Exception ex)
        {
            result = SocsExecutionResult.Fail(actionName, $"Unhandled deferred exception: {ex.Message}");
            _lastExceptionSummary = ex.ToString();
        }
        finally
        {
            _pendingDiagnosticDump = false;
        }

        _lastExecutionSummary = result.Message;
        _lastCommandResultSummary = $"{actionName}:{result.Ok}:{result.Message}";
        LogExecutionResult(result);

        if (result.InvalidateCache)
        {
            InvalidateDiscoveryCache(actionName);
        }

        MarkActionStatus(result.Ok ? SocsActionStatus.Success : SocsActionStatus.Fail);
        SendCommandResponse(client, result.CommandName, result.Ok, result.Payload ?? new
        {
            message = result.Message,
            diagnostics = BuildCommandDiagnostics(result.CommandName),
            lastActionStatus = _lastActionStatus
        });
    }

    private static SocsExecutionResult ExecutePlayCard(SocsInboundCommand command, SocsHandCardSnapshot matchedCard, int? targetIndex, string? targetId, SocsEnemySnapshot? target)
    {
        object[] roots = EnumerateDiscoveryRoots().ToArray();
        object? executionRoot = SelectExecutionRoot(roots);
        _lastPlayCardProbeSummary = DescribeProbeTargets(executionRoot, PlayCardMethodNames);
        if (executionRoot == null)
        {
            return SocsExecutionResult.Fail("play_card", "No execution root available for play_card.");
        }

        if (TryInvokeNamedMethod(executionRoot, PlayCardMethodNames, BuildPlayCardArguments(matchedCard, target), out object? rawResult, out string invokedMethod, out string probeNote))
        {
            return SocsExecutionResult.Success("play_card", $"Invoked {invokedMethod} for {FormatCard(matchedCard)}.", new
            {
                requestedIndex = command.Payload?.Index,
                requestedCardId = command.Payload?.CardId,
                targetIndex,
                targetId,
                matchedCard,
                target,
                invokedMethod,
                rawResult = SummarizeObject(rawResult),
                diagnostics = BuildCommandDiagnostics("play_card"),
                lastActionStatus = _lastActionStatus
            }, invalidateCache: true);
        }

        return SocsExecutionResult.Fail("play_card", $"No play_card execution method matched. {probeNote}", new
        {
            requestedIndex = command.Payload?.Index,
            requestedCardId = command.Payload?.CardId,
            targetIndex,
            targetId,
            matchedCard,
            target,
            diagnostics = BuildCommandDiagnostics("play_card"),
            lastActionStatus = _lastActionStatus
        });
    }

    private static SocsExecutionResult ExecuteEndTurn(SocsInboundCommand command)
    {
        object[] roots = EnumerateDiscoveryRoots().ToArray();
        object? executionRoot = SelectExecutionRoot(roots);
        _lastEndTurnProbeSummary = DescribeProbeTargets(executionRoot, EndTurnMethodNames);
        if (executionRoot == null)
        {
            return SocsExecutionResult.Fail("end_turn", "No execution root available for end_turn.");
        }

        if (TryInvokeNamedMethod(executionRoot, EndTurnMethodNames, Array.Empty<object?>(), out object? rawResult, out string invokedMethod, out string probeNote))
        {
            return SocsExecutionResult.Success("end_turn", $"Invoked {invokedMethod}.", new
            {
                invokedMethod,
                rawResult = SummarizeObject(rawResult),
                diagnostics = BuildCommandDiagnostics("end_turn"),
                lastActionStatus = _lastActionStatus
            }, invalidateCache: true);
        }

        return SocsExecutionResult.Fail("end_turn", $"No end_turn execution method matched. {probeNote}", new
        {
            diagnostics = BuildCommandDiagnostics("end_turn"),
            lastActionStatus = _lastActionStatus
        });
    }

    private static SocsExecutionResult ExecuteChooseOption(SocsInboundCommand command, SocsOptionSnapshot option)
    {
        object[] roots = EnumerateDiscoveryRoots().ToArray();
        object? executionRoot = SelectExecutionRoot(roots);
        _lastChooseOptionProbeSummary = DescribeProbeTargets(executionRoot, ChooseOptionMethodNames);
        if (executionRoot == null)
        {
            return SocsExecutionResult.Fail("choose_option", "No execution root available for choose_option.");
        }

        if (TryInvokeNamedMethod(executionRoot, ChooseOptionMethodNames, new object?[] { option.Index, option.Label ?? string.Empty }, out object? rawResult, out string invokedMethod, out string probeNote))
        {
            return SocsExecutionResult.Success("choose_option", $"Invoked {invokedMethod} for {FormatOption(option)}.", new
            {
                option,
                invokedMethod,
                rawResult = SummarizeObject(rawResult),
                diagnostics = BuildCommandDiagnostics("choose_option"),
                lastActionStatus = _lastActionStatus
            }, invalidateCache: true);
        }

        return SocsExecutionResult.Fail("choose_option", $"No choose_option execution method matched. {probeNote}", new
        {
            option,
            diagnostics = BuildCommandDiagnostics("choose_option"),
            lastActionStatus = _lastActionStatus
        });
    }

    private static SocsExecutionResult ExecuteSelectTarget(SocsInboundCommand command, SocsEnemySnapshot target)
    {
        object[] roots = EnumerateDiscoveryRoots().ToArray();
        object? executionRoot = SelectExecutionRoot(roots);
        _lastSelectTargetProbeSummary = DescribeProbeTargets(executionRoot, TargetSelectionMethodNames);
        if (executionRoot == null)
        {
            return SocsExecutionResult.Fail("select_target", "No execution root available for select_target.");
        }

        if (TryInvokeNamedMethod(executionRoot, TargetSelectionMethodNames, new object?[] { target.Index, target.Id, target.Name ?? string.Empty }, out object? rawResult, out string invokedMethod, out string probeNote))
        {
            return SocsExecutionResult.Success("select_target", $"Invoked {invokedMethod} for {FormatEnemy(target)}.", new
            {
                target,
                invokedMethod,
                rawResult = SummarizeObject(rawResult),
                diagnostics = BuildCommandDiagnostics("select_target"),
                lastActionStatus = _lastActionStatus
            }, invalidateCache: true);
        }

        return SocsExecutionResult.Fail("select_target", $"No select_target execution method matched. {probeNote}", new
        {
            target,
            diagnostics = BuildCommandDiagnostics("select_target"),
            lastActionStatus = _lastActionStatus
        });
    }

    private static void SendCommandResponse(SocsClientConnection client, SocsInboundCommand command, bool ok, object payload)
    {
        SendCommandResponse(client, command.Name ?? "<null>", command.Id, ok, payload);
    }

    private static void SendCommandResponse(SocsClientConnection client, string commandName, bool ok, object payload)
    {
        SendCommandResponse(client, commandName, null, ok, payload);
    }

    private static void SendCommandResponse(SocsClientConnection client, string commandName, string? commandId, bool ok, object payload)
    {
        _server?.SendResponse(client, new SocsResponseEnvelope
        {
            Id = commandId,
            Name = commandName,
            Ok = ok,
            Payload = payload
        });
    }

    private static void LogCommandDispatch(SocsInboundCommand command, string commandName)
    {
        _lastCommandNameSummary = commandName;
        _lastCommandIdSummary = command.Id;
        _lastCommandPayloadSummary = DescribeCommandPayload(command.Payload);
        if (_logCommandDispatch)
        {
            GD.Print($"{SocsActionPrefix} dispatch #{++_diagnosticSequence} | id={command.Id ?? "<null>"} | name={commandName} | payload={_lastCommandPayloadSummary}");
        }
    }

    private static void LogExecutionResult(SocsExecutionResult result)
    {
        if (_logExecutionResults)
        {
            GD.Print($"{SocsExecPrefix} result | command={result.CommandName} | ok={result.Ok} | message={result.Message}");
        }
    }

    private static void LogFailure(string message)
    {
        _lastExecutionFailureSummary = message;
        if (_logProbeFailures || _detailedFailureLoggingEnabled)
        {
            GD.PushWarning($"{SocsExecPrefix} {message}");
        }
    }

    private static object?[] BuildPlayCardArguments(SocsHandCardSnapshot card, SocsEnemySnapshot? target)
    {
        return target == null
            ? new object?[] { card.Index, card.Id, card.Name ?? string.Empty }
            : new object?[] { card.Index, card.Id, card.Name ?? string.Empty, target.Index, target.Id, target.Name ?? string.Empty };
    }

    private static bool CardRequiresTarget(SocsHandCardSnapshot card)
    {
        return string.Equals(card.Targeting, "enemy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Targeting, "any", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Targeting, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveTarget(int? targetIndex, string? targetId, out SocsEnemySnapshot? target)
    {
        target = null;
        List<SocsEnemySnapshot> enemies = BuildEnemiesSnapshot(BuildProbeContext());
        _lastTargetCandidateSummary = string.Join(", ", enemies.Take(MaxDiagnosticEntries).Select(FormatEnemy));

        if (!string.IsNullOrWhiteSpace(targetId))
        {
            target = enemies.FirstOrDefault(enemy => string.Equals(enemy.Id, targetId, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                return true;
            }
        }

        if (targetIndex.HasValue)
        {
            target = enemies.FirstOrDefault(enemy => enemy.Index == targetIndex.Value);
            if (target != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveOption(int? optionIndex, out SocsOptionSnapshot? option)
    {
        option = null;
        GameStateSnapshot snapshot = BuildSnapshot();
        List<SocsOptionSnapshot> options = snapshot.Selection?.Options ?? [];
        _lastOptionCandidateSummary = string.Join(", ", options.Take(MaxDiagnosticEntries).Select(FormatOption));
        if (optionIndex is >= 0 && optionIndex < options.Count)
        {
            option = options[optionIndex.Value];
            return true;
        }

        return false;
    }

    private static bool TryInvokeNamedMethod(object source, string[] methodNames, object?[] candidateArguments, out object? result, out string invokedMethod, out string note)
    {
        result = null;
        invokedMethod = string.Empty;
        note = string.Empty;

        IEnumerable<object> candidates = EnumerateInvocationCandidates(source).Take(MaxDiagnosticEntries * 4);
        var attempts = new List<string>();
        foreach (object candidate in candidates)
        {
            foreach (MethodInfo method in candidate.GetType().GetMethods(ReflectionFlags))
            {
                if (!methodNames.Any(name => string.Equals(name, method.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                attempts.Add($"{candidate.GetType().Name}.{method.Name}/{method.GetParameters().Length}");
                if (!TryBuildInvocationArguments(method, candidateArguments, out object?[] invocationArgs))
                {
                    continue;
                }

                try
                {
                    result = method.Invoke(candidate, invocationArgs);
                    invokedMethod = $"{candidate.GetType().FullName}.{method.Name}";
                    _lastMethodInvokeSummary = invokedMethod;
                    _lastMethodParamSummary = string.Join(", ", invocationArgs.Select(SummarizeObject));
                    _lastMethodMatchSummary = string.Join(", ", attempts.Take(MaxDiagnosticEntries));
                    return true;
                }
                catch (Exception ex)
                {
                    _lastReflectionFailureSummary = $"{candidate.GetType().FullName}.{method.Name}: {ex.GetBaseException().Message}";
                    LogFailure($"reflection invoke failed | method={candidate.GetType().FullName}.{method.Name} | error={ex.GetBaseException().Message}");
                }
            }
        }

        note = attempts.Count == 0
            ? $"No candidate methods in {DescribeInvocationRoots(source)}"
            : $"Tried {string.Join(", ", attempts.Take(MaxDiagnosticEntries))}";
        _lastMethodCandidateSummary = note;
        return false;
    }

    private static IEnumerable<object> EnumerateInvocationCandidates(object source)
    {
        var seen = new HashSet<int>();
        foreach (object candidate in EnumerateObjectGraph(source))
        {
            int identity = RuntimeHelpers.GetHashCode(candidate);
            if (seen.Add(identity))
            {
                yield return candidate;
            }
        }
    }

    private static bool TryBuildInvocationArguments(MethodInfo method, object?[] availableArguments, out object?[] invocationArgs)
    {
        ParameterInfo[] parameters = method.GetParameters();
        invocationArgs = new object?[parameters.Length];
        int cursor = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            bool matched = false;
            for (int j = cursor; j < availableArguments.Length; j++)
            {
                object? candidate = availableArguments[j];
                if (candidate == null)
                {
                    if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) != null)
                    {
                        invocationArgs[i] = null;
                        cursor = j + 1;
                        matched = true;
                        break;
                    }

                    continue;
                }

                Type candidateType = candidate.GetType();
                if (parameter.ParameterType.IsAssignableFrom(candidateType))
                {
                    invocationArgs[i] = candidate;
                    cursor = j + 1;
                    matched = true;
                    break;
                }

                if (TryConvertArgument(candidate, parameter.ParameterType, out object? converted))
                {
                    invocationArgs[i] = converted;
                    cursor = j + 1;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                if (parameter.HasDefaultValue)
                {
                    invocationArgs[i] = parameter.DefaultValue;
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    private static bool TryConvertArgument(object candidate, Type targetType, out object? converted)
    {
        converted = null;
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (effectiveType == typeof(string))
            {
                converted = candidate.ToString();
                return true;
            }

            if (effectiveType == typeof(int) && TryConvertToInt(candidate, out int intValue))
            {
                converted = intValue;
                return true;
            }

            if (effectiveType == typeof(bool))
            {
                if (candidate is bool boolValue)
                {
                    converted = boolValue;
                    return true;
                }

                if (bool.TryParse(candidate.ToString(), out bool parsedBool))
                {
                    converted = parsedBool;
                    return true;
                }
            }

            if (effectiveType.IsEnum)
            {
                string? text = candidate.ToString();
                if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse(effectiveType, text, true, out object? enumValue))
                {
                    converted = enumValue;
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static object? SelectExecutionRoot(object[] roots)
    {
        SnapshotProbeContext context = BuildProbeContext();
        foreach (object candidate in EnumerateExecutionRootCandidates(context, roots))
        {
            object? normalized = NormalizeExecutionRoot(candidate);
            if (normalized == null)
            {
                continue;
            }

            if (HasAnyNamedMethod(normalized, PlayCardMethodNames)
                || HasAnyNamedMethod(normalized, EndTurnMethodNames)
                || HasAnyNamedMethod(normalized, ChooseOptionMethodNames)
                || HasAnyNamedMethod(normalized, TargetSelectionMethodNames))
            {
                return normalized;
            }
        }

        foreach (object candidate in EnumerateExecutionRootCandidates(context, roots))
        {
            object? normalized = NormalizeExecutionRoot(candidate);
            if (normalized != null)
            {
                return normalized;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateExecutionRootCandidates(SnapshotProbeContext context, IEnumerable<object> roots)
    {
        var seen = new HashSet<int>();
        foreach (object? candidate in new[] { context.Combat, context.Run, context.PlayerManager, context.Player }.Concat(roots))
        {
            if (candidate == null)
            {
                continue;
            }

            object? normalized = NormalizeExecutionRoot(candidate);
            if (normalized == null)
            {
                continue;
            }

            int identity = RuntimeHelpers.GetHashCode(normalized);
            if (seen.Add(identity))
            {
                yield return normalized;
            }
        }
    }

    private static object? NormalizeExecutionRoot(object? source)
    {
        if (source == null)
        {
            return null;
        }

        if (source is IEnumerable enumerable && source is not string)
        {
            foreach (object? item in enumerable)
            {
                if (item != null && ShouldTraverse(item))
                {
                    return item;
                }
            }

            return null;
        }

        return ShouldTraverse(source) ? source : null;
    }

    private static bool HasAnyNamedMethod(object source, string[] methodNames)
    {
        foreach (object candidate in EnumerateInvocationCandidates(source).Take(MaxDiagnosticEntries * 4))
        {
            if (candidate.GetType().GetMethods(ReflectionFlags).Any(method => methodNames.Any(name => string.Equals(name, method.Name, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeProbeTargets(object? root, string[] methodNames)
    {
        if (root == null)
        {
            return "<no-root>";
        }

        string summary = $"root={root.GetType().FullName} | methods={string.Join(",", methodNames)}";
        _lastProbeSummary = summary;
        if (_logMethodCandidates)
        {
            GD.Print($"{SocsProbePrefix} {summary}");
        }

        return summary;
    }

    private static string DescribeInvocationRoots(object source)
    {
        return source.GetType().FullName ?? source.GetType().Name;
    }

    private static string DescribeCommandPayload(SocsCommandPayload? payload)
    {
        if (payload == null)
        {
            return "<null>";
        }

        return string.Join(DiagnosticSectionSeparator, new[]
        {
            $"value={payload.Value?.ToString(CultureInfo.InvariantCulture) ?? "null"}",
            $"index={payload.Index?.ToString() ?? "null"}",
            $"optionIndex={payload.OptionIndex?.ToString() ?? "null"}",
            $"targetIndex={payload.TargetIndex?.ToString() ?? "null"}",
            $"cardId={payload.CardId ?? "<null>"}",
            $"targetId={payload.TargetId ?? "<null>"}"
        });
    }

    private static object BuildCommandDiagnostics(string commandName)
    {
        return new
        {
            command = commandName,
            screen = _lastScreenSummary,
            screenReason = _lastDetectScreenReason,
            selection = _lastSelectionSummary,
            selectionReason = _lastBuildSelectionReason,
            selectionOptions = _lastSelectionOptionsSummary,
            pending = _lastPendingSummary,
            pendingReason = _lastBuildPendingReason,
            actionability = _lastActionabilitySummary,
            targetRequirement = _lastTargetRequirementSummary,
            targets = _lastTargetCandidateSummary,
            options = _lastOptionCandidateSummary,
            card = _lastResolvedCardSummary,
            target = _lastResolvedTargetSummary,
            option = _lastResolvedOptionSummary,
            playCardProbe = _lastPlayCardProbeSummary,
            endTurnProbe = _lastEndTurnProbeSummary,
            chooseOptionProbe = _lastChooseOptionProbeSummary,
            selectTargetProbe = _lastSelectTargetProbeSummary,
            methodMatch = _lastMethodMatchSummary,
            methodInvoke = _lastMethodInvokeSummary,
            methodParams = _lastMethodParamSummary,
            reflectionFailure = _lastReflectionFailureSummary,
            execution = _lastExecutionSummary,
            failure = _lastExecutionFailureSummary,
            snapshot = _lastSnapshotDigest,
            commandPayload = _lastCommandPayloadSummary
        };
    }

    private static string FormatCard(SocsHandCardSnapshot card)
    {
        return $"[{card.Index}] {card.Name ?? card.Id}<{card.Id}> target={card.Targeting ?? "?"}";
    }

    private static string FormatEnemy(SocsEnemySnapshot enemy)
    {
        return $"[{enemy.Index}] {enemy.Name ?? enemy.Id}<{enemy.Id}> hp={enemy.Hp?.ToString() ?? "?"}";
    }

    private static string FormatOption(SocsOptionSnapshot option)
    {
        return $"[{option.Index}] {option.Label ?? "<null>"} enabled={option.Enabled}";
    }

    private static string SummarizeObject(object? value)
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
            _ => value.ToString() ?? value.GetType().Name
        };
    }

    private static void MarkActionStatus(string status)
    {
        _lastActionStatus = status;
        _lastActionStatusSummary = status;
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

    private readonly record struct SocsExecutionResult(string CommandName, bool Ok, string Message, object? Payload, bool InvalidateCache)
    {
        public static SocsExecutionResult Success(string commandName, string message, object? payload = null, bool invalidateCache = false)
            => new(commandName, true, message, payload, invalidateCache);

        public static SocsExecutionResult Fail(string commandName, string message, object? payload = null)
            => new(commandName, false, message, payload, false);
    }

    private static bool HasConnectedClients()
    {
        return _server?.HasClients == true;
    }

    private static void BroadcastSnapshotIfNeeded()
    {
        if (!HasConnectedClients())
        {
            return;
        }

        ulong currentFrame = (ulong)Engine.GetFramesDrawn();
        if (_lastSnapshotFrame != 0 && currentFrame - _lastSnapshotFrame < SocsConstants.SnapshotFrameInterval)
        {
            return;
        }

        try
        {
            var snapshot = new SocsSnapshotEnvelope
            {
                Seq = Interlocked.Increment(ref _sequence),
                Frame = currentFrame,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Data = BuildSnapshot()
            };

            byte[] payload = SocsProtocol.Serialize(snapshot);
            _server.Broadcast(payload);
            _completedSnapshotBuildCount++;
            _lastSnapshotFrame = currentFrame;
        }
        catch (Exception ex)
        {
            _lastExceptionSummary = ex.ToString();
            GD.PushWarning($"SOCS snapshot broadcast failed: {ex.Message}");
        }
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
        TryAssignField("selection", () => snapshot.Selection = BuildSelectionSnapshot(snapshot, context));
        TryAssignField("pending", () => snapshot.Pending = BuildPendingSnapshot(snapshot, context));
        TryAssignField("actionability", () => snapshot.Actionability = BuildActionabilitySnapshot(snapshot, context));

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
        TryAssignField("runMeta.potionCapacity", () => runMeta.PotionCapacity = BuildPotionCapacitySnapshot(context));
        TryAssignField("runMeta.potions", () => runMeta.Potions = BuildPotionsSnapshot(context));
        TryAssignField("runMeta.relics", () => runMeta.Relics = BuildRelicsSnapshot(context));
        return runMeta;
    }

    private static int? BuildPotionCapacitySnapshot(SnapshotProbeContext context)
    {
        return ProbeSnapshotInt(context, "potionCapacity", PotionCapacityNames);
    }

    private static List<SocsPotionSnapshot> BuildPotionsSnapshot(SnapshotProbeContext context)
    {
        object source = context.Player ?? context.Run ?? context.PlayerManager ?? context.Roots.First();
        List<SocsPotionSnapshot> potions = [];
        int index = 0;

        foreach (object potion in EnumerateCompositeItems(source, context.Roots, PotionCollectionNames, MaxPlausibleOptions))
        {
            if (!LooksLikePotion(potion))
            {
                continue;
            }

            int potionIndex = index++;
            var snapshot = new SocsPotionSnapshot
            {
                Index = potionIndex,
                Id = BuildCardIdentity(potion),
                Name = TryReadStringByNames(potion, CardNameMembers) ?? potion.GetType().Name,
                Usable = TryReadBoolByNames(potion, out bool usable, PotionUsableNames) ? usable : null,
                Description = TryReadStringByNames(potion, DescriptionMemberNames),
                Targeting = NormalizeTargeting(potion)
            };

            potions.Add(snapshot);
        }

        return potions;
    }

    private static bool LooksLikePotion(object value)
    {
        string typeName = value.GetType().Name;
        return ContainsAnyKeyword(typeName, ["Potion", "Consumable", "Flask", "Brew"])
            || TryReadStringByNames(value, CardNameMembers) != null && TryReadBoolByNames(value, out _, PotionUsableNames);
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
        if (!string.Equals(screen, "combat", StringComparison.OrdinalIgnoreCase) && !HasObservableCombatState(context))
        {
            return null;
        }

        var combat = new SocsCombatSnapshot();
        TryAssignField("combat.playerEnergy", () => combat.PlayerEnergy = ProbePlayerEnergy(context));
        TryAssignField("combat.playerBlock", () => combat.PlayerBlock = ProbePlayerBlock(context));
        TryAssignField("combat.playerPowers", () => combat.PlayerPowers = BuildPowersSnapshot(context.Player ?? FindCombatSource(context)));
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
        if (!string.Equals(screen, "rewards", StringComparison.OrdinalIgnoreCase) || context.Proceed == null)
        {
            return null;
        }

        List<SocsOptionSnapshot> options = BuildOptionsSnapshot(context.Proceed, context.Roots);
        if (options.Count == 0)
        {
            return null;
        }

        return new SocsRewardsSnapshot
        {
            Items = options.Select(option => new SocsRewardItemSnapshot
            {
                Index = option.Index,
                Kind = "option",
                Label = option.Label,
                Enabled = option.Enabled
            }).ToList()
        };
    }

    private static SocsRestSnapshot? BuildRestSnapshot(SnapshotProbeContext context, string screen)
    {
        if (!string.Equals(screen, "rest", StringComparison.OrdinalIgnoreCase) || context.Proceed == null)
        {
            return null;
        }

        List<SocsOptionSnapshot> options = BuildOptionsSnapshot(context.Proceed, context.Roots);
        if (options.Count == 0)
        {
            return null;
        }

        return new SocsRestSnapshot
        {
            Options = options
        };
    }

    private static SocsSelectionSnapshot? BuildSelectionSnapshot(GameStateSnapshot snapshot, SnapshotProbeContext context)
    {
        SocsSelectionSnapshot? selection = null;
        bool requiresTarget = IsTargetSelectionActive(context);

        if (requiresTarget && snapshot.Combat?.Enemies.Count > 0)
        {
            selection = new SocsSelectionSnapshot
            {
                Kind = "target",
                RequiresTarget = true,
                Options = snapshot.Combat.Enemies
                    .Where(enemy => enemy.Alive != false)
                    .Select(enemy => new SocsOptionSnapshot
                    {
                        Index = enemy.Index,
                        Label = enemy.Name ?? enemy.Id,
                        Enabled = enemy.Alive != false
                    }).ToList()
            };
            _lastBuildSelectionReason = "combat_target_selection";
        }
        else if (snapshot.Shop?.LeaveOption != null)
        {
            selection = new SocsSelectionSnapshot
            {
                Kind = "shop",
                RequiresTarget = false,
                Options = BuildSelectionOptions(snapshot.Shop.Items.Count, snapshot.Shop.LeaveOption)
            };
            _lastBuildSelectionReason = "shop_items_and_leave";
        }
        else if (snapshot.Event?.Options.Count > 0)
        {
            selection = new SocsSelectionSnapshot
            {
                Kind = "event",
                RequiresTarget = false,
                Options = snapshot.Event.Options.ToList()
            };
            _lastBuildSelectionReason = "event_options";
        }
        else if (snapshot.Rest?.Options.Count > 0)
        {
            selection = new SocsSelectionSnapshot
            {
                Kind = "rest",
                RequiresTarget = false,
                Options = snapshot.Rest.Options.ToList()
            };
            _lastBuildSelectionReason = "rest_options";
        }
        else if (snapshot.Rewards?.Items.Count > 0)
        {
            selection = new SocsSelectionSnapshot
            {
                Kind = "rewards",
                RequiresTarget = false,
                Options = snapshot.Rewards.Items.Select(item => new SocsOptionSnapshot
                {
                    Index = item.Index,
                    Label = item.Label,
                    Enabled = item.Enabled
                }).ToList()
            };
            _lastBuildSelectionReason = "reward_options";
        }
        else
        {
            _lastBuildSelectionReason = string.Equals(snapshot.Screen, "menu", StringComparison.OrdinalIgnoreCase)
                ? "menu_idle"
                : "no_active_selection";
        }

        _lastSelectionSummary = selection == null
            ? "<none>"
            : $"{selection.Kind ?? "unknown"} target={selection.RequiresTarget?.ToString() ?? "null"}";
        _lastSelectionKindSummary = selection?.Kind ?? "<none>";
        _lastSelectionOptionsSummary = selection == null || selection.Options.Count == 0
            ? "<none>"
            : string.Join(", ", selection.Options.Take(MaxDiagnosticEntries).Select(FormatOption));
        _lastSelectionDiagnostic = _lastSelectionSummary;
        _lastSnapshotSelectionDigest = _lastSelectionSummary;
        _lastSnapshotSelectionReason = _lastBuildSelectionReason;
        return selection;
    }

    private static SocsPendingSnapshot? BuildPendingSnapshot(GameStateSnapshot snapshot, SnapshotProbeContext context)
    {
        SocsPendingSnapshot? pending = null;

        if (_pendingDiagnosticDump)
        {
            pending = new SocsPendingSnapshot
            {
                Waiting = true,
                Reason = $"deferred:{_lastDeferredActionSummary ?? "command"}"
            };
            _lastBuildPendingReason = "deferred_action_in_flight";
        }
        else if (IsTargetSelectionActive(context))
        {
            pending = new SocsPendingSnapshot
            {
                Waiting = true,
                Reason = "awaiting_target_selection"
            };
            _lastBuildPendingReason = "combat_target_selection";
        }
        else if (string.Equals(snapshot.LastActionStatus, SocsActionStatus.Fail, StringComparison.OrdinalIgnoreCase))
        {
            pending = new SocsPendingSnapshot
            {
                Waiting = false,
                Reason = "last_action_failed"
            };
            _lastBuildPendingReason = "last_action_failed";
        }
        else
        {
            _lastBuildPendingReason = "idle";
        }

        _lastPendingSummary = pending == null
            ? "<none>"
            : $"waiting={pending.Waiting} reason={pending.Reason ?? "<null>"}";
        _lastPendingReasonSummary = pending?.Reason ?? "<none>";
        _lastPendingDiagnostic = _lastPendingSummary;
        _lastSnapshotPendingDigest = _lastPendingSummary;
        _lastSnapshotPendingReason = _lastBuildPendingReason;
        return pending;
    }

    private static SocsActionabilitySnapshot BuildActionabilitySnapshot(GameStateSnapshot snapshot, SnapshotProbeContext context)
    {
        var actionability = new SocsActionabilitySnapshot();
        actionability.AvailableCommands.Add("ping");
        actionability.AvailableCommands.Add("set_time_scale");

        bool hasCombat = snapshot.Combat != null;
        bool hasEnabledSelectionOptions = snapshot.Selection?.Options.Any(option => option.Enabled) == true;
        bool hasPlayableCard = snapshot.Combat?.Hand.Any(card => card.Playable == true) == true;
        bool targetSelectionActive = IsTargetSelectionActive(context);
        bool canPlayCard = hasCombat && hasPlayableCard && !_pendingDiagnosticDump && !targetSelectionActive;
        bool canChooseOption = hasEnabledSelectionOptions && !_pendingDiagnosticDump && !targetSelectionActive;
        bool canEndTurn = hasCombat && !_pendingDiagnosticDump && !targetSelectionActive && string.Equals(snapshot.Screen, "combat", StringComparison.OrdinalIgnoreCase);
        bool canSelectTarget = targetSelectionActive && !_pendingDiagnosticDump && snapshot.Combat?.Enemies.Any(enemy => enemy.Alive != false) == true;

        if (canPlayCard)
        {
            actionability.AvailableCommands.Add("play_card");
        }

        if (canEndTurn)
        {
            actionability.AvailableCommands.Add("end_turn");
        }

        if (canChooseOption)
        {
            actionability.AvailableCommands.Add("choose_option");
        }

        if (canSelectTarget)
        {
            actionability.AvailableCommands.Add("select_target");
        }

        actionability.CanPlayCard = canPlayCard;
        actionability.CanChooseOption = canChooseOption;
        actionability.CanEndTurn = canEndTurn;
        actionability.RequiresTarget = canSelectTarget;

        _lastAvailableCommandSummary = actionability.AvailableCommands.Count == 0
            ? "<none>"
            : string.Join(", ", actionability.AvailableCommands);
        _lastCommandAvailabilitySummary = _lastAvailableCommandSummary;
        _lastTargetRequirementSummary = canSelectTarget ? "target_required" : "target_not_required";
        _lastCanSummary = $"play={canPlayCard} choose={canChooseOption} end={canEndTurn} target={canSelectTarget}";
        _lastBuildActionabilityReason = _pendingDiagnosticDump
            ? "deferred_action_gate"
            : canSelectTarget
                ? "combat_target_followup"
                : hasCombat
                    ? "combat_commands_available"
                    : canChooseOption
                        ? "selection_commands_available"
                        : "baseline_commands_only";
        _lastActionabilitySummary = $"commands=[{_lastAvailableCommandSummary}] | {_lastCanSummary}";
        _lastActionabilityDiagnostic = _lastActionabilitySummary;
        _lastSnapshotActionabilityDigest = _lastActionabilitySummary;
        _lastSnapshotActionabilityReason = _lastBuildActionabilityReason;
        return actionability;
    }


    private static bool IsTargetSelectionActive(SnapshotProbeContext context)
    {
        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            if (candidate == null)
            {
                continue;
            }

            if (TryReadBoolByNames(candidate, out bool active, VisibleMemberNames) && active && TryFindMemberShallow(candidate, TargetCollectionMemberNames) != null)
            {
                return true;
            }

            object? targetingState = TryFindMemberShallow(candidate, TargetingStateMemberNames);
            if (targetingState != null)
            {
                if (TryReadBoolByNames(targetingState, out bool targetingVisible, VisibleMemberNames) && targetingVisible)
                {
                    return true;
                }

                if (TryReadBoolByNames(targetingState, out bool targetingEnabled, BoolStateMemberNames) && targetingEnabled)
                {
                    return true;
                }

                if (TryFindMemberShallow(targetingState, SelectedTargetMemberNames) != null || TryFindMemberShallow(targetingState, TargetCollectionMemberNames) != null)
                {
                    return true;
                }
            }

            object? pendingState = TryFindMemberShallow(candidate, PendingStateMemberNames);
            string? pendingText = pendingState == null ? null : TryExtractTextValue(pendingState);
            if (!string.IsNullOrWhiteSpace(pendingText)
                && (pendingText.Contains("target", StringComparison.OrdinalIgnoreCase)
                    || pendingText.Contains("enemy", StringComparison.OrdinalIgnoreCase)
                    || pendingText.Contains("monster", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
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
        object[] roots = BuildPriorityRoots();
        object? run = FindPriorityObject(roots, RunMemberNames);
        object? playerManager = FindPriorityObject(roots, PlayerManagerMemberNames);
        object? player = FindPlayerProbe(run, playerManager, roots);
        object? combat = FindPriorityObject(roots, CombatMemberNames);
        object? shop = FindPriorityObject(roots, ShopScreenNames);
        object? eventState = FindPriorityObject(roots, EventScreenNames);
        object? proceed = FindPriorityObject(roots, ProceedScreenNames);

        return new SnapshotProbeContext(roots, run, playerManager, player, combat, shop, eventState, proceed);
    }

    private static object[] BuildPriorityRoots()
    {
        var roots = new List<object>();
        var seen = new HashSet<int>();

        void AddRoot(object? candidate)
        {
            if (candidate == null || !ShouldTraverse(candidate))
            {
                return;
            }

            int identity = RuntimeHelpers.GetHashCode(candidate);
            if (seen.Add(identity))
            {
                roots.Add(candidate);
            }
        }

        AddRoot(_cachedSingleton);

        if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
        {
            foreach (Node child in tree.Root.GetChildren())
            {
                if (!ContainsAnyKeyword(child.Name, RootMemberNames) && !ContainsAnyKeyword(child.GetType().FullName, RootMemberNames))
                {
                    continue;
                }

                AddRoot(child);
                foreach (object? direct in EnumerateRootMembers(child))
                {
                    AddRoot(direct);
                }
            }
        }

        if (roots.Count == 0)
        {
            object? singleton = FindStaticSingletonCandidate();
            if (singleton != null)
            {
                _cachedSingleton = singleton;
                AddRoot(singleton);
            }
        }

        return roots.ToArray();
    }

    private static object? FindCombatSource(SnapshotProbeContext context)
    {
        object? bestSource = null;
        int bestScore = int.MinValue;

        void ConsiderCandidate(object? candidate)
        {
            if (candidate == null)
            {
                return;
            }

            int score = ScoreCombatSourceCandidate(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestSource = candidate;
            }
        }

        if (context.Combat != null)
        {
            ConsiderCandidate(context.Combat);
            if (TryFindObservableCombatSource(context.Combat, out object? nestedCombatSource))
            {
                ConsiderCandidate(nestedCombatSource);
            }
        }

        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            ConsiderCandidate(candidate);
            if (TryFindObservableCombatSource(candidate, out object? source))
            {
                ConsiderCandidate(source);
            }
        }

        return bestScore > 0 ? bestSource : null;
    }

    private static int ScoreCombatSourceCandidate(object source)
    {
        int score = 0;

        if (TryBuildCardSnapshot(source, MaxPlausibleZoneCards, out _, out int totalCount) && totalCount >= 0)
        {
            score += 2;
        }

        if (TryReadIntByNames(source, EnergyMemberNames) != null)
        {
            score += 1;
        }

        if (TryFindMemberShallow(source, CombatMemberNames) != null)
        {
            score += 2;
        }

        if (TryFindMemberShallow(source, EnemyCollectionMemberNames) != null)
        {
            score += 8;
        }

        int enemySignals = 0;
        foreach (object enemy in EnumerateCompositeItems(source, [source], EnemyCollectionMemberNames, MaxPlausibleOptions * 2))
        {
            if (!LooksLikeEnemyCandidate(enemy))
            {
                continue;
            }

            enemySignals++;
            score += 6;
            if (enemySignals >= 3)
            {
                break;
            }
        }

        return score;
    }

    private static bool TryFindObservableCombatSource(object source, out object? combatSource)
    {
        combatSource = null;

        foreach (string memberName in CombatMemberNames.Concat(EnemyCollectionMemberNames).Concat(HandParentNames).Concat(DrawPileNames).Concat(DiscardPileNames).Concat(ExhaustPileNames))
        {
            object? candidate = TryReadMember(source, memberName);
            if (candidate == null)
            {
                continue;
            }

            if (LooksLikeCombatSource(candidate))
            {
                combatSource = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeCombatSource(object source)
    {
        if (TryBuildCardSnapshot(source, MaxPlausibleZoneCards, out _, out int totalCount) && totalCount >= 0)
        {
            return true;
        }

        foreach (object enemy in EnumerateCompositeItems(source, [source], EnemyCollectionMemberNames, MaxPlausibleOptions * 2))
        {
            if (LooksLikeEnemyCandidate(enemy))
            {
                return true;
            }
        }

        return TryReadIntByNames(source, EnergyMemberNames) != null;
    }

    private static bool HasObservableCombatState(SnapshotProbeContext context)
    {
        if (context.Combat != null)
        {
            return true;
        }

        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            if (TryFindMemberShallow(candidate, EnemyCollectionMemberNames) != null
                || TryFindMemberShallow(candidate, HandParentNames) != null
                || TryFindMemberShallow(candidate, DrawPileNames) != null
                || TryFindMemberShallow(candidate, DiscardPileNames) != null
                || TryFindMemberShallow(candidate, ExhaustPileNames) != null)
            {
                return true;
            }

            if (TryReadIntByNames(candidate, EnergyMemberNames).HasValue)
            {
                return true;
            }

            foreach (string containerName in EnergyContainerNames)
            {
                object? container = TryReadMember(candidate, containerName);
                if (container != null && TryReadIntByNames(container, EnergyMemberNames).HasValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CanUseHeavySnapshotFallbacks()
    {
        return _completedSnapshotBuildCount > 0;
    }

    private static int? ProbeSnapshotInt(SnapshotProbeContext context, string cacheKey, string[] memberNames)
    {
        if (TryReadIntFromContext(context, memberNames, out int directValue))
        {
            return directValue;
        }

        if (!CanUseHeavySnapshotFallbacks())
        {
            return null;
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

        object? playerCombatant = FindPlayerCombatantOwner(context);
        if (playerCombatant != null)
        {
            int? playerValue = FindIntNearCombatant(playerCombatant, EnergyMemberNames);
            if (playerValue.HasValue)
            {
                return playerValue.Value;
            }
        }

        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            if (TryFindIntInGraph(candidate, EnergyMemberNames, out int graphValue))
            {
                return graphValue;
            }

            foreach (string containerName in EnergyContainerNames)
            {
                object? container = TryReadMember(candidate, containerName);
                if (container != null && TryFindIntInGraph(container, EnergyMemberNames, out graphValue))
                {
                    return graphValue;
                }
            }
        }

        if (!CanUseHeavySnapshotFallbacks())
        {
            return null;
        }

        int? cachedValue = TryGetCachedInt("playerEnergy");
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        EnsureDiscoveryCache();
        return TryGetCachedInt("playerEnergy");
    }

    private static int? ProbePlayerBlock(SnapshotProbeContext context)
    {
        if (TryReadIntFromContext(context, BlockMemberNames, out int directValue))
        {
            return directValue;
        }

        object? playerCombatant = FindPlayerCombatantOwner(context);
        if (playerCombatant != null)
        {
            int? playerValue = FindIntNearCombatant(playerCombatant, BlockMemberNames);
            if (playerValue.HasValue)
            {
                return playerValue.Value;
            }
        }

        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            if (TryFindIntInGraph(candidate, BlockMemberNames, out int graphValue))
            {
                return graphValue;
            }
        }

        if (!CanUseHeavySnapshotFallbacks())
        {
            return null;
        }

        int? cachedValue = TryGetCachedInt("playerBlock");
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        EnsureDiscoveryCache();
        return TryGetCachedInt("playerBlock");
    }

    private static int? ProbeEnemyBlock(SnapshotProbeContext context, object enemy)
    {
        int? directValue = TryReadIntByNames(enemy, BlockMemberNames);
        if (directValue.HasValue)
        {
            return directValue.Value;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        return owner == null ? null : FindIntNearCombatant(owner, BlockMemberNames);
    }

    private static int? ProbeEnemyHealth(SnapshotProbeContext context, object enemy)
    {
        int? directValue = TryReadIntByNames(enemy, "CurrentHealth", "CurrentHp", "Health", "Hp");
        if (directValue.HasValue)
        {
            return directValue.Value;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        return owner == null ? null : FindIntNearCombatant(owner, new[] { "CurrentHealth", "CurrentHp", "Health", "Hp" });
    }

    private static string? ProbeEnemyName(SnapshotProbeContext context, object enemy)
    {
        object? nestedCreature = TryReadMember(enemy, "Creature") ?? TryReadMember(enemy, "creature");
        string? nestedValue = nestedCreature == null ? null : TryReadStringByNames(nestedCreature, EnemyNameMembers);
        if (IsPlausibleEnemyName(nestedValue))
        {
            return nestedValue;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        if (owner != null)
        {
            string? ownerValue = TryReadStringByNames(owner, EnemyNameMembers);
            if (IsPlausibleEnemyName(ownerValue))
            {
                return ownerValue;
            }
        }

        string? directValue = TryReadStringByNames(enemy, EnemyNameMembers);
        if (IsPlausibleEnemyName(directValue))
        {
            return directValue;
        }

        string? fallbackValue = TryReadStringByNames(enemy, CardNameMembers);
        return IsPlausibleEnemyName(fallbackValue) ? fallbackValue : null;
    }

    private static bool IsPlausibleEnemyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !string.Equals(value, "Creature", StringComparison.OrdinalIgnoreCase);
    }

    private static string ProbeEnemyId(SnapshotProbeContext context, object enemy)
    {
        string? directValue = TryReadStringByNames(enemy, CardIdMembers);
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        if (owner != null)
        {
            string? ownerId = TryReadStringByNames(owner, CardIdMembers);
            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                return ownerId;
            }
        }

        return BuildCardIdentity(enemy);
    }

    private static bool? ProbeEnemyAlive(SnapshotProbeContext context, object enemy)
    {
        int? hp = ProbeEnemyHealth(context, enemy);
        return TryReadAliveState(enemy, hp);
    }

    private static string? ProbeEnemyIntentType(SnapshotProbeContext context, object enemy)
    {
        object? carrier = FindIntentCarrier(context, enemy);
        return carrier == null ? null : NormalizeIntentType(carrier);
    }

    private static int? ProbeEnemyIntentDamage(SnapshotProbeContext context, object enemy)
    {
        object? carrier = FindIntentCarrier(context, enemy);
        return carrier == null ? null : TryReadIntentDamage(carrier);
    }

    private static int? ProbeEnemyIntentMulti(SnapshotProbeContext context, object enemy)
    {
        object? carrier = FindIntentCarrier(context, enemy);
        return carrier == null ? null : TryReadIntentMulti(carrier);
    }

    private static object? FindIntentCarrier(SnapshotProbeContext context, object enemy)
    {
        object? directCarrier = TryResolveIntentCarrier(enemy);
        if (directCarrier != null)
        {
            return directCarrier;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        if (owner == null)
        {
            return null;
        }

        return TryResolveIntentCarrier(owner);
    }

    private static object? TryResolveIntentCarrier(object source)
    {
        object? nestedCarrier = TryFindMemberShallow(source, IntentCarrierMemberNames);
        if (nestedCarrier != null)
        {
            return nestedCarrier;
        }

        object? inlineType = TryFindMemberShallow(source, IntentTypeMemberNames);
        object? inlineDamage = TryFindMemberShallow(source, IntentDamageMemberNames);
        object? inlineMulti = TryFindMemberShallow(source, IntentMultiMemberNames);
        return inlineType != null || inlineDamage != null || inlineMulti != null ? source : null;
    }

    private static List<SocsPowerSnapshot> ProbeEnemyPowers(SnapshotProbeContext context, object enemy)
    {
        List<SocsPowerSnapshot> directValue = BuildPowersSnapshot(enemy);
        if (directValue.Count > 0)
        {
            return directValue;
        }

        object? owner = FindEnemyCombatantOwner(context, enemy);
        return owner == null ? [] : BuildPowersSnapshot(owner);
    }

    private static bool TryFindNodeOwningChild(object source, Func<object, bool> matchesChild, out object? owner)
    {
        foreach (object node in EnumerateObjectGraph(source))
        {
            foreach (PropertyInfo property in node.GetType().GetProperties(ReflectionFlags))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object? value;
                try
                {
                    value = property.GetValue(node);
                }
                catch
                {
                    continue;
                }

                if (value == null || value is string)
                {
                    continue;
                }

                if (value is IEnumerable enumerable)
                {
                    foreach (object? item in enumerable)
                    {
                        if (item != null && matchesChild(item))
                        {
                            owner = node;
                            return true;
                        }
                    }

                    continue;
                }

                if (matchesChild(value))
                {
                    owner = node;
                    return true;
                }
            }

            foreach (FieldInfo field in node.GetType().GetFields(ReflectionFlags))
            {
                object? value;
                try
                {
                    value = field.GetValue(node);
                }
                catch
                {
                    continue;
                }

                if (value == null || value is string)
                {
                    continue;
                }

                if (value is IEnumerable enumerable)
                {
                    foreach (object? item in enumerable)
                    {
                        if (item != null && matchesChild(item))
                        {
                            owner = node;
                            return true;
                        }
                    }

                    continue;
                }

                if (matchesChild(value))
                {
                    owner = node;
                    return true;
                }
            }
        }

        owner = null;
        return false;
    }

    private static bool IsSameReference(object left, object right)
    {
        return RuntimeHelpers.GetHashCode(left) == RuntimeHelpers.GetHashCode(right);
    }

    private static object? FindEnemyCombatantOwner(SnapshotProbeContext context, object enemy)
    {
        object? combatSource = FindCombatSource(context);
        if (combatSource == null)
        {
            return null;
        }

        return TryFindNodeOwningChild(combatSource, child => IsSameReference(child, enemy), out object? owner)
            ? owner
            : null;
    }

    private static object? FindPlayerCombatantOwner(SnapshotProbeContext context)
    {
        if (context.Player != null)
        {
            return context.Player;
        }

        object? combatSource = FindCombatSource(context);
        if (combatSource == null)
        {
            return null;
        }

        foreach (object node in EnumerateObjectGraph(combatSource))
        {
            object? nestedPlayer = TryFindMember(node, PlayerParentNames);
            if (nestedPlayer != null)
            {
                return nestedPlayer;
            }
        }

        return null;
    }

    private static int? FindIntNearCombatant(object source, string[] memberNames)
    {
        foreach (object node in EnumerateObjectGraph(source))
        {
            foreach (string memberName in memberNames)
            {
                if (TryReadInt(node, memberName, out int value))
                {
                    return value;
                }
            }
        }

        return null;
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
        if (!HasConnectedClients())
        {
            return false;
        }

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
            if (card == null)
            {
                return false;
            }

            object? semanticCard = ResolveCardSemanticRoot(card);
            if (semanticCard == null)
            {
                return false;
            }

            snapshot.Add(BuildCardSnapshot(card, semanticCard, index++));
        }

        return true;
    }

    private static SocsHandCardSnapshot BuildCardSnapshot(object rawCard, object card, int index)
    {
        var snapshot = new SocsHandCardSnapshot
        {
            Index = index,
            Id = BuildCardIdentity(card),
            Name = TryReadStringByNames(card, CardNameMembers),
            Playable = TryReadBoolByNames(card, out bool playable, PlayableMemberNames) ? playable : null,
            EnergyCost = ProbeCardEnergyCost(card),
            Targeting = NormalizeTargeting(card)
        };

        LogCardProbeDiagnostics(rawCard, card, snapshot, index);
        TryAssignField($"combat.hand[{index}].upgraded", () => snapshot.Upgraded = TryReadBoolByNames(card, out bool upgraded, UpgradeMemberNames) ? upgraded : null);
        TryAssignField($"combat.hand[{index}].type", () => snapshot.Type = NormalizeCardType(card));
        TryAssignField($"combat.hand[{index}].baseDamage", () => snapshot.BaseDamage = TryReadIntByNames(card, DamageMemberNames));
        TryAssignField($"combat.hand[{index}].baseBlock", () => snapshot.BaseBlock = TryReadIntByNames(card, BlockMemberNames));
        TryAssignField($"combat.hand[{index}].description", () => snapshot.Description = TryReadStringByNames(card, DescriptionMemberNames));
        return snapshot;
    }

    private static object? ResolveCardSemanticRoot(object source)
    {
        object? best = null;
        int bestScore = int.MinValue;

        void ConsiderCandidate(object candidate)
        {
            if (!LooksLikeCard(candidate))
            {
                return;
            }

            int score = ScoreCardSemanticCandidate(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            ConsiderCandidate(candidate);
        }

        return best;
    }

    private static int ScoreCardSemanticCandidate(object card)
    {
        int score = 0;
        if (ContainsAnyKeyword(card.GetType().Name, CardTypeKeywords) || ContainsAnyKeyword(card.GetType().FullName, CardTypeKeywords))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(TryReadStringByNames(card, CardNameMembers)))
        {
            score += 6;
        }

        if (!string.IsNullOrWhiteSpace(TryReadStringByNames(card, CardIdMembers)))
        {
            score += 4;
        }

        if (TryReadIntByNames(card, CurrentCostMemberNames).HasValue || TryReadIntByNames(card, CostMemberNames).HasValue)
        {
            score += 4;
        }

        if (TryFindMemberShallow(card, CardTypeMemberNames) != null)
        {
            score += 2;
        }

        if (TryFindMemberShallow(card, TargetTypeMemberNames) != null || TryFindMemberShallow(card, ["Targeting", "targeting", "TargetMode", "targetMode"]) != null)
        {
            score += 2;
        }

        if (TryReadBoolByNames(card, out _, PlayableMemberNames))
        {
            score += 1;
        }

        return score;
    }

    private static int? ProbeCardEnergyCost(object card)
    {
        int? currentCost = TryReadIntByNames(card, CurrentCostMemberNames);
        if (currentCost.HasValue)
        {
            return currentCost.Value;
        }

        return TryReadIntByNames(card, CostMemberNames);
    }

    private static bool? ProbeCardIsXCost(object card)
    {
        if (TryReadBoolByNames(card, out bool explicitXCost, XCostMemberNames))
        {
            return explicitXCost;
        }

        int? currentCost = TryReadIntByNames(card, CurrentCostMemberNames);
        if (currentCost == -1)
        {
            return true;
        }

        int? baseCost = TryReadIntByNames(card, CostMemberNames);
        return baseCost == -1 ? true : null;
    }

    private static string? NormalizeTargeting(object card)
    {
        object? rawTarget = TryFindMemberShallow(card, TargetTypeMemberNames)
            ?? TryFindMemberShallow(card, ["Targeting", "targeting", "TargetMode", "targetMode"]);
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
        object? rawType = TryFindMemberShallow(card, CardTypeMemberNames);
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
        object? rawIntent = TryFindMemberShallow(enemy, IntentTypeMemberNames);
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
        object? source = FindEnemySource(context);
        if (source != null && TryBuildEnemySnapshots(context, source, out List<SocsEnemySnapshot> directSnapshot, out _))
        {
            return directSnapshot;
        }

        if (TryGetCachedEnemySnapshot(context, out List<SocsEnemySnapshot> cachedSnapshot))
        {
            return cachedSnapshot;
        }

        if (TryGetEnemySnapshotFromDiscovery(context, out List<SocsEnemySnapshot> refreshedSnapshot))
        {
            return refreshedSnapshot;
        }

        return [];
    }

    private static object? FindEnemySource(SnapshotProbeContext context)
    {
        foreach (object candidate in EnumerateProbeCandidates(context))
        {
            foreach (object enemySource in EnumerateEnemySources(candidate))
            {
                return enemySource;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateEnemySources(object source)
    {
        foreach (object candidate in EnumerateNamedEnemySources(source))
        {
            if (IsAcceptedEnemySource(candidate))
            {
                yield return candidate;
            }
        }

        foreach (string combatName in CombatMemberNames)
        {
            object? combat = TryReadMember(source, combatName);
            if (combat == null)
            {
                continue;
            }

            foreach (object candidate in EnumerateNamedEnemySources(combat))
            {
                if (IsAcceptedEnemySource(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<object> EnumerateNamedEnemySources(object source)
    {
        object? direct = TryFindMember(source, EnemyCollectionMemberNames);
        if (direct != null)
        {
            yield return direct;
        }
    }

    private static bool TryBuildEnemySnapshots(SnapshotProbeContext context, object source, out List<SocsEnemySnapshot> enemies, out int totalCount)
    {
        enemies = [];
        totalCount = 0;
        var seen = new HashSet<int>();

        foreach (object? item in EnumerateObjects(source))
        {
            if (item == null)
            {
                continue;
            }

            totalCount++;
            if (!TryYieldEnemyCandidate(item, seen, out object? enemy) || enemy == null)
            {
                continue;
            }

            int enemyIndex = enemies.Count;
            int? hp = ProbeEnemyHealth(context, enemy);
            bool? alive = ProbeEnemyAlive(context, enemy);
            var snapshot = new SocsEnemySnapshot
            {
                Index = enemyIndex,
                Id = ProbeEnemyId(context, enemy),
                Name = ProbeEnemyName(context, enemy) ?? enemy.GetType().Name,
                Hp = hp,
                Block = ProbeEnemyBlock(context, enemy),
                Alive = alive
            };

            TryAssignField($"combat.enemies[{enemyIndex}].intentType", () => snapshot.IntentType = ProbeEnemyIntentType(context, enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].intentDamage", () => snapshot.IntentDamage = ProbeEnemyIntentDamage(context, enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].intentMulti", () => snapshot.IntentMulti = ProbeEnemyIntentMulti(context, enemy));
            TryAssignField($"combat.enemies[{enemyIndex}].powers", () => snapshot.Powers = ProbeEnemyPowers(context, enemy));
            LogEnemyProbeDiagnostics(context, item, enemy, snapshot, enemyIndex);
            enemies.Add(snapshot);
        }

        return totalCount > 0;
    }

    private static bool IsAcceptedEnemySource(object source)
    {
        int totalCount = 0;
        int recognizedCount = 0;
        var seen = new HashSet<int>();

        foreach (object? item in EnumerateObjects(source))
        {
            if (item == null)
            {
                continue;
            }

            totalCount++;
            if (!TryYieldEnemyCandidate(item, seen, out _))
            {
                continue;
            }

            recognizedCount++;
            if (recognizedCount > MaxPlausibleOptions * 2)
            {
                return false;
            }
        }

        return totalCount > 0 && recognizedCount > 0;
    }

    private static bool TryResolveEnemySourceFromPath(SocsMemberPath path, SnapshotProbeContext context, out object? value)
    {
        value = null;
        if (!IsEnemyCandidatePath(path))
        {
            return false;
        }

        if (!TryReadPathValue(path, out object? enemySource) || enemySource == null)
        {
            return false;
        }

        if (!TryBuildEnemySnapshots(context, enemySource, out List<SocsEnemySnapshot> enemies, out int totalCount)
            || totalCount <= 0
            || enemies.Count == 0
            || enemies.Count > MaxPlausibleOptions * 2)
        {
            return false;
        }

        value = enemySource;
        return true;
    }

    private static bool TryGetCachedEnemySnapshot(SnapshotProbeContext context, out List<SocsEnemySnapshot> snapshot)
    {
        snapshot = [];
        if (!CachedPaths.TryGetValue("enemies", out SocsMemberPath? enemyPath))
        {
            return false;
        }

        if (!TryResolveEnemySourceFromPath(enemyPath, context, out object? enemySource) || enemySource == null)
        {
            LogDiscoveryWarning($"[SOCS] [DISCOVERY] Cached enemy path invalidated: {enemyPath.DisplayPath}");
            CachedPaths.Remove("enemies");
            _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
            return false;
        }

        if (!TryBuildEnemySnapshots(context, enemySource, out snapshot, out _))
        {
            CachedPaths.Remove("enemies");
            _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
            return false;
        }

        return true;
    }

    private static bool TryGetEnemySnapshotFromDiscovery(SnapshotProbeContext context, out List<SocsEnemySnapshot> snapshot)
    {
        snapshot = [];
        if (!HasConnectedClients())
        {
            return false;
        }

        EnsureDiscoveryCache();
        return TryGetCachedEnemySnapshot(context, out snapshot);
    }

    private static bool TryFindEnemyPath(object gameRoot, SnapshotProbeContext context, out SocsMemberPath? path, out object? value)
    {
        foreach ((SocsMemberPath candidatePath, object candidateValue) in EnumerateDiscoveryPaths(gameRoot))
        {
            if (!IsAcceptedEnemyPath(candidatePath, candidateValue, context))
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

    private static bool IsAcceptedEnemyPath(SocsMemberPath path, object candidateValue, SnapshotProbeContext context)
    {
        return IsEnemyCandidatePath(path)
            && TryBuildEnemySnapshots(context, candidateValue, out List<SocsEnemySnapshot> enemies, out int totalCount)
            && totalCount > 0
            && enemies.Count > 0
            && enemies.Count <= MaxPlausibleOptions * 2;
    }

    private static bool IsEnemyCandidatePath(SocsMemberPath path)
    {
        return path.Members.Any(member => ContainsAnyKeyword(member.Name, EnemyCollectionMemberNames)
            || ContainsAnyKeyword(member.Name, CombatMemberNames)
            || ContainsAnyKeyword(member.Name, ["Encounter", "encounter", "Battle", "battle"]))
            || ContainsAnyKeyword(path.LeafName, EnemyCollectionMemberNames);
    }

    private static void TryCacheEnemyPath(object root)
    {
        SnapshotProbeContext context = BuildProbeContext();
        if (CachedPaths.ContainsKey("enemies") || !TryFindEnemyPath(root, context, out SocsMemberPath? path, out object? enemySource) || path == null)
        {
            return;
        }

        CachedPaths["enemies"] = path;
        LogDiscovery($"[SOCS] [DISCOVERY] Cached enemies: {path.DisplayPath} => {enemySource?.GetType().FullName ?? "<null>"}");
    }

    private static bool TryYieldEnemyCandidate(object source, HashSet<int> seen, out object? candidate)
    {
        object? semanticEnemy = ResolveEnemySemanticRoot(source);
        if (semanticEnemy == null)
        {
            candidate = null;
            return false;
        }

        int identity = RuntimeHelpers.GetHashCode(semanticEnemy);
        if (!seen.Add(identity))
        {
            candidate = null;
            return false;
        }

        candidate = semanticEnemy;
        return true;
    }

    private static object? ResolveEnemySemanticRoot(object source)
    {
        object? best = null;
        int bestScore = int.MinValue;

        void ConsiderCandidate(object candidate)
        {
            if (!LooksLikeEnemy(candidate))
            {
                return;
            }

            int score = ScoreEnemySemanticCandidate(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            ConsiderCandidate(candidate);
        }

        return best;
    }

    private static int ScoreEnemySemanticCandidate(object enemy)
    {
        int score = 0;
        if (ContainsAnyKeyword(enemy.GetType().Name, EnemyTypeKeywords) || ContainsAnyKeyword(enemy.GetType().FullName, EnemyTypeKeywords))
        {
            score += 8;
        }

        if (TryReadIntByNames(enemy, "CurrentHealth", "CurrentHp", "Health", "Hp").HasValue)
        {
            score += 6;
        }

        if (!string.IsNullOrWhiteSpace(TryReadStringByNames(enemy, EnemyNameMembers)))
        {
            score += 4;
        }

        if (TryResolveIntentCarrier(enemy) != null)
        {
            score += 4;
        }

        if (BuildPowersSnapshot(enemy).Count > 0)
        {
            score += 2;
        }

        return score;
    }

    private static bool LooksLikeEnemyCandidate(object value)
    {
        foreach (object candidate in EnumerateShallowProbeObjects(value))
        {
            if (LooksLikeEnemy(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEnemySignal(object value)
    {
        return TryReadIntByNames(value, "CurrentHealth", "CurrentHp", "Health", "Hp", "IntentDamage", "intentDamage", "MoveDamage", "moveDamage") != null
            || TryFindMemberShallow(value, IntentTypeMemberNames) != null
            || BuildPowersSnapshot(value).Count > 0;
    }

    private static bool LooksLikeEnemy(object value)
    {
        if (LooksLikePlayer(value))
        {
            return false;
        }

        return ContainsAnyKeyword(value.GetType().Name, EnemyTypeKeywords)
            || ContainsAnyKeyword(value.GetType().FullName, EnemyTypeKeywords)
            || HasEnemySignal(value);
    }

    private static bool LooksLikePlayer(object value)
    {
        return ContainsAnyKeyword(value.GetType().Name, PlayerParentNames)
            || ContainsAnyKeyword(value.GetType().FullName, PlayerParentNames)
            || TryReadBoolByNames(value, out _, PlayableMemberNames)
            || TryReadIntByNames(value, EnergyMemberNames) != null;
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
        TryCacheEnemyPath(root);
    }

    private static bool HasCompleteBattleZoneCache()
    {
        return CachedPaths.ContainsKey("hand")
            && CachedPaths.ContainsKey("drawPile")
            && CachedPaths.ContainsKey("discardPile")
            && CachedPaths.ContainsKey("exhaustPile")
            && CachedPaths.ContainsKey("enemies");
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
            case decimal decimalValue:
                value = (int)decimalValue;
                return true;
        }

        foreach (object candidate in EnumerateShallowProbeObjects(rawValue))
        {
            foreach (string memberName in new[] { "Value", "value", "Amount", "amount", "Cost", "cost", "Current", "current", "Base", "base" })
            {
                object? nestedValue = TryReadMember(candidate, memberName);
                if (nestedValue == null || ReferenceEquals(nestedValue, candidate))
                {
                    continue;
                }

                if (TryConvertToInt(nestedValue, out value))
                {
                    return true;
                }
            }
        }

        return int.TryParse(rawValue.ToString(), out value);
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

        if (LooksLikeActiveScreen(context.Proceed))
        {
            if (ContainsAnyKeyword(context.Proceed.GetType().Name, ["Rest", "Campfire"]) || ContainsAnyKeyword(context.Proceed.GetType().FullName, ["Rest", "Campfire"]))
            {
                return "rest";
            }

            if (BuildOptionsSnapshot(context.Proceed, context.Roots).Count > 0)
            {
                return "rewards";
            }

            return "menu";
        }

        if (HasObservableCombatState(context))
        {
            return "combat";
        }

        return "menu";
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
            foreach (object probe in EnumerateShallowProbeObjects(candidate))
            {
                int identity = RuntimeHelpers.GetHashCode(probe);
                if (seen.Add(identity))
                {
                    yield return probe;
                }
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
        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            foreach (string memberName in memberNames)
            {
                if (TryReadInt(candidate, memberName, out int value))
                {
                    return value;
                }
            }
        }

        foreach (object node in EnumerateObjectGraph(source))
        {
            foreach (string memberName in memberNames)
            {
                if (TryReadInt(node, memberName, out int value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryReadBoolByNames(object source, out bool value, params string[] memberNames)
    {
        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            foreach (string memberName in memberNames)
            {
                object? rawValue = TryReadMember(candidate, memberName);
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

    private static object? TryFindMemberShallow(object source, string[] memberNames)
    {
        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            object? value = TryFindMember(candidate, memberNames);
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
        foreach (object candidate in EnumerateShallowProbeObjects(source))
        {
            foreach (string memberName in memberNames)
            {
                object? value = TryReadMember(candidate, memberName);
                string? text = TryExtractTextValue(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        foreach (object node in EnumerateObjectGraph(source))
        {
            foreach (string memberName in memberNames)
            {
                object? value = TryReadMember(node, memberName);
                string? text = TryExtractTextValue(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? TryExtractTextValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        foreach (string memberName in TextValueMemberNames)
        {
            object? nested = TryReadMember(value, memberName);
            if (nested == null)
            {
                continue;
            }

            string? nestedText = TryExtractTextValue(nested);
            if (!string.IsNullOrWhiteSpace(nestedText))
            {
                return nestedText;
            }
        }

        string textValue = value.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(textValue))
        {
            return null;
        }

        return textValue.Any(ch => ch == '\uFFFD') ? null : textValue;
    }

    private static IEnumerable<object> EnumerateShallowProbeObjects(object source)
    {
        var seen = new HashSet<int>();
        int sourceIdentity = RuntimeHelpers.GetHashCode(source);
        if (seen.Add(sourceIdentity))
        {
            yield return source;
        }

        foreach (string memberName in ShallowWrapperMemberNames)
        {
            object? nested = TryReadMember(source, memberName);
            if (nested == null || nested is string || nested is IEnumerable)
            {
                continue;
            }

            int identity = RuntimeHelpers.GetHashCode(nested);
            if (seen.Add(identity))
            {
                yield return nested;
            }
        }
    }

    private static bool TryReadInt(object source, string memberName, out int value)
    {
        value = default;
        object? rawValue = TryReadMember(source, memberName);
        return TryConvertToInt(rawValue, out value);
    }

    private static void LogCardProbeDiagnostics(object rawCard, object card, SocsHandCardSnapshot snapshot, int index)
    {
        if (_currentCardProbeDiagnosticFrame != _framesSinceInit)
        {
            _currentCardProbeDiagnosticFrame = _framesSinceInit;
            _cardProbeDiagnosticCount = 0;
            _cardProbeDiagnosticsLogged = false;
        }

        if (_cardProbeDiagnosticsLogged || _cardProbeDiagnosticCount >= MaxPlausibleHandCards)
        {
            return;
        }

        _cardProbeDiagnosticCount++;
        string wrappers = DescribeShallowProbeObjects(rawCard);
        string currentCosts = DescribeNamedValues(card, CurrentCostMemberNames);
        string baseCosts = DescribeNamedValues(card, CostMemberNames);
        GD.Print($"{SocsDiagPrefix} card[{index}] raw={rawCard.GetType().FullName} resolved={card.GetType().FullName} name={snapshot.Name ?? "<null>"} energy={snapshot.EnergyCost?.ToString(CultureInfo.InvariantCulture) ?? "<null>"} wrappers={wrappers} current={currentCosts} base={baseCosts}");
        if (_cardProbeDiagnosticCount >= MaxPlausibleHandCards)
        {
            _cardProbeDiagnosticsLogged = true;
        }
    }

    private static void LogEnemyProbeDiagnostics(SnapshotProbeContext context, object rawEnemy, object enemy, SocsEnemySnapshot snapshot, int index)
    {
        if (_currentEnemyProbeDiagnosticFrame != _framesSinceInit)
        {
            _currentEnemyProbeDiagnosticFrame = _framesSinceInit;
            _enemyProbeDiagnosticCount = 0;
            _enemyProbeDiagnosticsLogged = false;
        }

        int maxEnemyDiagnostics = MaxPlausibleOptions * 2;
        if (_enemyProbeDiagnosticsLogged || _enemyProbeDiagnosticCount >= maxEnemyDiagnostics)
        {
            return;
        }

        _enemyProbeDiagnosticCount++;
        object? owner = FindEnemyCombatantOwner(context, enemy);
        object? carrier = FindIntentCarrier(context, enemy);
        string enemyNames = DescribeNamedValues(enemy, EnemyNameMembers);
        string ownerNames = owner == null ? "<null>" : DescribeNamedValues(owner, EnemyNameMembers);
        string carrierIntent = carrier == null ? "<null>" : DescribeNamedValues(carrier, IntentTypeMemberNames, IntentDamageMemberNames, IntentMultiMemberNames);
        GD.Print($"{SocsDiagPrefix} enemy[{index}] raw={rawEnemy.GetType().FullName} resolved={enemy.GetType().FullName} owner={owner?.GetType().FullName ?? "<null>"} carrier={carrier?.GetType().FullName ?? "<null>"} name={snapshot.Name} hp={snapshot.Hp?.ToString(CultureInfo.InvariantCulture) ?? "<null>"} names={enemyNames} ownerNames={ownerNames} intent={carrierIntent}");
        if (_enemyProbeDiagnosticCount >= maxEnemyDiagnostics)
        {
            _enemyProbeDiagnosticsLogged = true;
        }
    }

    private static string DescribeShallowProbeObjects(object source)
    {
        return string.Join(", ", EnumerateShallowProbeObjects(source).Take(4).Select(candidate => candidate.GetType().FullName ?? candidate.GetType().Name));
    }

    private static string DescribeNamedValues(object source, params string[][] memberGroups)
    {
        var segments = new List<string>();
        foreach (string[] memberNames in memberGroups)
        {
            foreach (object candidate in EnumerateShallowProbeObjects(source).Take(4))
            {
                foreach (string memberName in memberNames)
                {
                    object? value = TryReadMember(candidate, memberName);
                    if (value == null)
                    {
                        continue;
                    }

                    segments.Add($"{candidate.GetType().Name}.{memberName}={FormatDiscoveryValue(value)}");
                }
            }
        }

        return segments.Count == 0 ? "<none>" : string.Join(", ", segments.Take(12));
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
    object? Event,
    object? Proceed
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
