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
    private static readonly string[] RootMemberNames = ["Run", "Game", "Dungeon", "AbstractDungeon", "Battle", "Combat"];
    private static readonly string[] DungeonSingletonKeywords = ["Dungeon", "Game", "Battle", "Combat", "Run"];
    private static readonly string[] DiscoveryMemberKeywords = ["Run", "Player", "Dungeon", "State", "Combat", "Screen", "Room", "Battle", "Hand", "Card"];
    private static readonly string[] StaticSingletonMemberNames = ["Instance", "Current", "CurrentRun", "CurrentState", "CurrentDungeon", "SharedInstance", "_instance", "s_instance"];
    private static readonly string[] HandParentNames = ["Hand", "hand", "HandGroup", "CardsInHand", "CardsInHandGroup", "HandCards", "cardsInHand", "Cards", "cards"];
    private static readonly string[] PlayerParentNames = ["Player", "player", "Hero", "hero", "Character", "character"];
    private static readonly string[] TraceTerminalNames = ["Run", "CurrentRun", "Combat", "Battle", "Player", "Hero", "Character", "Hand", "HandGroup", "CardsInHand", "Cards", "CurrentHealth", "CurrentHp", "Health", "Hp", "Gold", "gold", "Floor", "floor", "CurrentFloor", "ActFloor"];
    private const int TraceLogLimit = 192;
    private static readonly string[] CardNameMembers = ["Name", "name", "CardName", "DisplayName", "Title", "title"];
    private static readonly string[] CardIdMembers = ["Uuid", "UUID", "Guid", "guid", "Id", "id", "InstanceId", "instanceId"];

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
        EnsureDiscoveryCache();

        var snapshot = new GameStateSnapshot();

        TryAssignField("hp", () => snapshot.Hp = TryGetCachedInt("hp") ?? TryGetIntByNames("CurrentHealth", "CurrentHp", "Health", "Hp"));
        TryAssignField("gold", () => snapshot.Gold = TryGetCachedInt("gold") ?? TryGetIntByNames("Gold", "gold"));
        TryAssignField("floor", () => snapshot.Floor = TryGetCachedInt("floor") ?? TryGetIntByNames("Floor", "floor", "CurrentFloor", "ActFloor"));
        TryAssignField("timeScale", () => snapshot.TimeScale = (float)Engine.TimeScale);
        TryAssignField("hand", () => snapshot.Hand = BuildHandSnapshot());
        TryAssignField("lastActionStatus", () => snapshot.LastActionStatus = _lastActionStatus);

        return snapshot;
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
                LogTargetedDiscoveryTrace(root);
            }

            TryCacheIntPath("hp", root, "CurrentHealth", "CurrentHp", "Health", "Hp");
            TryCacheIntPath("gold", root, "Gold", "gold");
            TryCacheIntPath("floor", root, "Floor", "floor", "CurrentFloor", "ActFloor");
            TryCacheHandPath(root);

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

        GD.PushWarning($"[SOCS] [DISCOVERY] Cached path invalidated for {key}: {path.DisplayPath}");
        CachedPaths.Remove(key);
        _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
        return null;
    }

    private static bool HasCompleteDiscoveryCache()
    {
        return CachedPaths.ContainsKey("hp")
            && CachedPaths.ContainsKey("gold")
            && CachedPaths.ContainsKey("floor")
            && CachedPaths.ContainsKey("hand");
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
        GD.Print($"[SOCS] [DISCOVERY] Cached {key}: {path.DisplayPath} => {FormatDiscoveryValue(value)}");
    }

    private static void TryCacheHandPath(object gameRoot)
    {
        if (CachedPaths.ContainsKey("hand") || !TryFindHandPath(gameRoot, out SocsMemberPath? path, out object? handSource) || path == null)
        {
            return;
        }

        CachedPaths["hand"] = path;
        GD.Print($"[SOCS] [DISCOVERY] Cached hand: {path.DisplayPath} => {handSource?.GetType().FullName ?? "<null>"}");
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

    private static bool TryFindHandPath(object gameRoot, out SocsMemberPath? path, out object? value)
    {
        foreach ((SocsMemberPath candidatePath, object candidateValue) in EnumerateDiscoveryPaths(gameRoot))
        {
            if (!HandParentNames.Any(name => string.Equals(name, candidatePath.LeafName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!EnumerateObjects(candidateValue).Any(item => item != null))
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

    private static void LogTargetedDiscoveryTrace(object root)
    {
        string rootType = root.GetType().FullName ?? root.GetType().Name;
        GD.Print($"[SOCS] [TRACE] Begin targeted trace from {rootType}");

        int logged = 0;
        foreach ((SocsMemberPath path, object value) in EnumerateDiscoveryPaths(root))
        {
            if (!ShouldLogTracePath(path, value))
            {
                continue;
            }

            GD.Print($"[SOCS] [TRACE] {path.DisplayPath} ({value.GetType().FullName}) => {FormatTraceValue(value)}");
            logged++;
            if (logged >= TraceLogLimit)
            {
                GD.Print($"[SOCS] [TRACE] Trace truncated after {logged} matches from {rootType}");
                return;
            }
        }

        GD.Print($"[SOCS] [TRACE] Trace completed with {logged} matches from {rootType}");
    }

    private static bool ShouldLogTracePath(SocsMemberPath path, object value)
    {
        if (TraceTerminalNames.Any(name => string.Equals(name, path.LeafName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string displayPath = path.DisplayPath;
        if (ContainsAnyKeyword(displayPath, TraceTerminalNames))
        {
            return true;
        }

        Type type = value.GetType();
        return ContainsAnyKeyword(type.Name, TraceTerminalNames)
            || ContainsAnyKeyword(type.FullName, TraceTerminalNames);
    }

    private static string FormatTraceValue(object value)
    {
        if (value is IEnumerable enumerable and not string and not IDictionary)
        {
            int count = 0;
            foreach (object? _ in enumerable)
            {
                count++;
                if (count >= 16)
                {
                    break;
                }
            }

            return $"{value.GetType().FullName} [count~{count}]";
        }

        return FormatDiscoveryValue(value);
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
        if (CachedPaths.TryGetValue("hand", out SocsMemberPath? handPath)
            && TryReadPathValue(handPath, out object? cachedHandSource)
            && cachedHandSource != null)
        {
            List<SocsHandCardSnapshot> cachedSnapshot = BuildHandSnapshotFromSource(cachedHandSource);
            if (cachedSnapshot.Count > 0)
            {
                return cachedSnapshot;
            }

            CachedPaths.Remove("hand");
            _nextDiscoveryFrame = Math.Min(_nextDiscoveryFrame, _framesSinceInit + 1);
        }

        foreach (object root in EnumerateDiscoveryRoots())
        {
            object? handSource = TryFindHandSource(root);
            if (handSource == null)
            {
                continue;
            }

            var snapshot = BuildHandSnapshotFromSource(handSource);
            if (snapshot.Count > 0)
            {
                return snapshot;
            }
        }

        return [];
    }

    private static List<SocsHandCardSnapshot> BuildHandSnapshotFromSource(object handSource)
    {
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

        return snapshot;
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

        GD.Print($"[SOCS] [DISCOVERY] Root candidate ({source}): {root.GetType().FullName}");
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
                if (singleton != null)
                {
                    GD.Print($"[SOCS] [DISCOVERY] Found singleton candidate: {type.FullName} -> {singleton.GetType().FullName}");
                    return singleton;
                }
            }
        }

        GD.Print("[SOCS] [DISCOVERY] No singleton candidate found");
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
