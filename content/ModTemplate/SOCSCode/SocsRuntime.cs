using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

namespace SOCS.Code;

internal static class SocsRuntime
{
    private const int DiscoveryMaxDepth = 6;
    private const int DiscoveryMaxNodes = 512;
    private static readonly string ConfigPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        $"{MainFile.ModId.ToLowerInvariant()}_config.txt"
    );

    private static readonly ConcurrentQueue<SocsPendingCommand> PendingCommands = new();
    private static readonly BindingFlags ReflectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] RootMemberNames = ["Run", "Game", "Dungeon", "AbstractDungeon"];
    private static readonly string[] DungeonSingletonKeywords = ["Dungeon", "Game"];
    private static readonly string[] HandParentNames = ["Hand", "hand", "HandGroup", "CardsInHand", "CardsInHandGroup", "HandCards", "cardsInHand", "Cards", "cards"];
    private static readonly string[] PlayerParentNames = ["Player", "player", "Hero", "hero", "Character", "character"];
    private static readonly string[] CardNameMembers = ["Name", "name", "CardName", "DisplayName", "Title", "title"];
    private static readonly string[] CardIdMembers = ["Uuid", "UUID", "Guid", "guid", "Id", "id", "InstanceId", "instanceId"];

    private static SocsServer? _server;
    private static bool _initialized;
    private static long _sequence;
    private static ulong _lastSnapshotFrame;
    private static float _targetTimeScale = SocsConstants.DefaultTimeScale;
    private static string _lastActionStatus = SocsActionStatus.None;

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

        EnsureStickyTimeScale();
        DrainCommands();
        BroadcastSnapshotIfNeeded();
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

        TryAssignField("hp", () => snapshot.Hp = TryGetIntByNames("CurrentHealth", "CurrentHp", "Health", "Hp"));
        TryAssignField("gold", () => snapshot.Gold = TryGetIntByNames("Gold", "gold"));
        TryAssignField("floor", () => snapshot.Floor = TryGetIntByNames("Floor", "floor", "CurrentFloor", "ActFloor"));
        TryAssignField("timeScale", () => snapshot.TimeScale = (float)Engine.TimeScale);
        TryAssignField("hand", () => snapshot.Hand = BuildHandSnapshot());
        TryAssignField("lastActionStatus", () => snapshot.LastActionStatus = _lastActionStatus);

        return snapshot;
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
            if (TryFindIntInGraph(root, memberNames, out int value))
            {
                return value;
            }
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
        List<SocsHandCardSnapshot> hand = BuildHandSnapshot();

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

    private static List<SocsHandCardSnapshot> BuildHandSnapshot()
    {
        foreach (object root in EnumerateDiscoveryRoots())
        {
            object? handSource = TryFindHandSource(root);
            if (handSource == null)
            {
                continue;
            }

            var snapshot = new List<SocsHandCardSnapshot>();
            int index = 0;
            foreach (object? card in EnumerateObjects(handSource))
            {
                if (card == null)
                {
                    continue;
                }

                snapshot.Add(new SocsHandCardSnapshot
                {
                    Index = index++,
                    Id = BuildCardIdentity(card),
                    Name = TryReadStringByNames(card, CardNameMembers)
                });
            }

            if (snapshot.Count > 0)
            {
                return snapshot;
            }
        }

        return [];
    }

    private static object? TryFindHandSource(object root)
    {
        foreach (object node in EnumerateObjectGraph(root))
        {
            object? direct = TryFindMember(node, HandParentNames);
            if (direct != null)
            {
                return direct;
            }

            object? nested = TryFindNestedMember(node, PlayerParentNames, HandParentNames);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateDiscoveryRoots()
    {
        var yielded = new HashSet<int>();

        SceneTree? tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root != null)
        {
            foreach (object candidate in EnumerateSceneCandidates(tree.Root))
            {
                int id = RuntimeHelpers.GetHashCode(candidate);
                if (yielded.Add(id))
                {
                    yield return candidate;
                }
            }
        }

        object? singleton = FindStaticSingletonCandidate();
        if (singleton != null)
        {
            int id = RuntimeHelpers.GetHashCode(singleton);
            if (yielded.Add(id))
            {
                yield return singleton;
            }
        }
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
                if (singleton != null)
                {
                    return singleton;
                }
            }
        }

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

            if (!ContainsAnyKeyword(property.Name, DungeonSingletonKeywords))
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

            if (!ContainsAnyKeyword(field.Name, DungeonSingletonKeywords))
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
                if (int.TryParse(rawValue.ToString(), out int parsed))
                {
                    value = parsed;
                    return true;
                }

                return false;
        }
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
