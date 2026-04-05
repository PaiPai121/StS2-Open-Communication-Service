# SOCS Headless Engine Bridge API

SOCS (StS2 Open Communication Service) is a local TCP bridge that lets an external agent play Slay the Spire 2 without looking at the game window.

The bridge has two responsibilities:

- **Control plane**: accept commands from an external client.
- **State plane**: stream structured game snapshots back to that client.

The design goal is **headless play**: a Python or other external program should be able to make decisions using only this API contract, without reading the C# implementation and without relying on screen pixels, OCR, or UI automation.

## 1. Transport

- **Protocol**: TCP
- **Host**: `127.0.0.1`
- **Port**: `7777`
- **Encoding**: UTF-8 JSON
- **Frame format**: 4-byte little-endian unsigned length prefix followed by JSON payload bytes
- **Connection model**: local loopback only

Each message is sent as:

1. a 4-byte little-endian frame length
2. a JSON payload of that exact length

Clients must read complete frames before decoding JSON.

## 2. Envelope Types

### 2.1 Inbound command envelope

All client commands use this envelope:

```json
{
  "type": "command",
  "id": "6cf1db16-6d86-4e59-8c75-0f4ab0b3954c",
  "name": "ping",
  "payload": {}
}
```

Fields:

- `type`: must be `"command"`
- `id`: caller-generated request id used to correlate responses
- `name`: command name
- `payload`: command-specific object, empty object when no payload is required

### 2.2 Outbound snapshot envelope

The server pushes state snapshots using this envelope:

```json
{
  "type": "snapshot",
  "seq": 42,
  "frame": 1200,
  "ts": 1712300000123,
  "data": {
    "schemaVersion": 2,
    "screen": "combat",
    "lastActionStatus": "SUCCESS",
    "system": {
      "timeScale": 1.0
    },
    "runMeta": {
      "hp": 68,
      "gold": 99,
      "floor": 7,
      "potionCapacity": 3,
      "potions": [
        {
          "index": 0,
          "id": "DexterityPotion",
          "name": "Dexterity Potion",
          "usable": true,
          "description": "Gain Dexterity.",
          "targeting": null
        }
      ],
      "relics": [
        {
          "index": 0,
          "id": "BurningBlood",
          "name": "Burning Blood"
        }
      ]
    },
    "combat": {
      "playerEnergy": 3,
      "playerBlock": 0,
      "playerPowers": [
        {
          "id": "Strength",
          "amount": 1
        }
      ],
      "hand": [
        {
          "index": 0,
          "id": "Strike_R",
          "name": "Strike",
          "playable": true,
          "energyCost": 1,
          "targeting": "ENEMY",
          "upgraded": false,
          "type": "ATTACK",
          "baseDamage": 6,
          "baseBlock": null,
          "description": "Deal 6 damage."
        },
        {
          "index": 1,
          "id": "Defend_R",
          "name": "Defend",
          "playable": true,
          "energyCost": 1,
          "targeting": "NONE",
          "upgraded": false,
          "type": "SKILL",
          "baseDamage": null,
          "baseBlock": 5,
          "description": "Gain 5 Block."
        }
      ],
      "drawPile": [],
      "discardPile": [],
      "exhaustPile": [],
      "enemies": [
        {
          "index": 0,
          "id": "JawWorm",
          "name": "Jaw Worm",
          "hp": 42,
          "block": 0,
          "alive": true,
          "intentType": "ATTACK",
          "intentDamage": 11,
          "intentMulti": 1,
          "powers": []
        }
      ]
    },
    "map": {
      "nodes": [],
      "currentNode": null
    },
    "rewards": {
      "items": []
    },
    "rest": {
      "options": []
    },
    "shop": null,
    "event": null,
    "selection": {
      "kind": null,
      "requiresTarget": false,
      "options": []
    },
    "pending": {
      "waiting": false,
      "reason": null
    },
    "actionability": {
      "canPlayCard": true,
      "canChooseOption": false,
      "canEndTurn": true,
      "requiresTarget": false,
      "availableCommands": [
        "play_card",
        "end_turn"
      ]
    }
  }
}
```

Top-level fields:

- `type`: always `"snapshot"`
- `seq`: monotonically increasing snapshot sequence number
- `frame`: engine frame counter captured with the snapshot
- `ts`: Unix timestamp in milliseconds
- `data`: the structured game state

### 2.3 Outbound response envelope

The server sends a response after processing a command.

```json
{
  "type": "response",
  "id": "6cf1db16-6d86-4e59-8c75-0f4ab0b3954c",
  "name": "ping",
  "ok": true,
  "payload": null
}
```

Fields:

- `type`: always `"response"`
- `id`: mirrors the command id when available
- `name`: mirrors the command name
- `ok`: whether the command was accepted/executed successfully
- `payload`: optional command-specific result data

### 2.4 Outbound error envelope

The server sends an error envelope when a command payload is invalid or processing fails at protocol level.

```json
{
  "type": "error",
  "id": "6cf1db16-6d86-4e59-8c75-0f4ab0b3954c",
  "message": "Invalid command payload."
}
```

Fields:

- `type`: always `"error"`
- `id`: the related command id when known
- `message`: human-readable error description

## 3. Supported Commands

## 3.1 `ping`

Purpose: verify connectivity and request/response flow.

Request:

```json
{
  "type": "command",
  "id": "6f3e5e16-1df8-4170-9b59-b6c760dc3db5",
  "name": "ping",
  "payload": {}
}
```

Payload fields: none.

## 3.2 `set_time_scale`

Purpose: change the engine time scale.

Request:

```json
{
  "type": "command",
  "id": "1459a763-a391-43fc-b5b0-e2856884fefa",
  "name": "set_time_scale",
  "payload": {
    "value": 1.0
  }
}
```

Payload fields:

- `value` (`number`): requested time scale value

Notes:

- The runtime clamps this value to its supported minimum and maximum range.
- Clients should treat the next snapshot `data.system.timeScale` as the source of truth.

## 3.3 `play_card`

Purpose: play a card from hand, optionally with an explicit target.

Request by hand index:

```json
{
  "type": "command",
  "id": "68715d7b-1f3d-45d6-83f0-38ad5b845e91",
  "name": "play_card",
  "payload": {
    "index": 0
  }
}
```

Request by card id:

```json
{
  "type": "command",
  "id": "9d196590-6852-4eb6-b090-f1cbb0f52030",
  "name": "play_card",
  "payload": {
    "cardId": "Strike_R"
  }
}
```

Request with target:

```json
{
  "type": "command",
  "id": "1e24cce3-f4d6-4bb3-af91-2c0e27911144",
  "name": "play_card",
  "payload": {
    "index": 0,
    "targetIndex": 0
  }
}
```

Payload fields:

- `index` (`integer`, optional): hand position of the card to play
- `cardId` (`string`, optional): card id to play
- `targetIndex` (`integer`, optional): target enemy index
- `targetId` (`string`, optional): target id

Notes:

- Use `data.actionability.canPlayCard` and `data.actionability.availableCommands` to decide whether this command is currently legal.
- Use `data.actionability.requiresTarget` and `data.selection.requiresTarget` to determine whether a target must be chosen.
- `index` refers to the current hand snapshot, not a permanent deck position.

## 3.4 `end_turn`

Purpose: end the current turn.

Request:

```json
{
  "type": "command",
  "id": "f8188f81-29ab-41f5-ad1b-badccac70bb1",
  "name": "end_turn",
  "payload": {}
}
```

Payload fields: none.

Notes:

- Clients should only send this when `data.actionability.canEndTurn` is `true`.

## 3.5 `choose_option`

Purpose: select an indexed option from the current choice surface.

Request:

```json
{
  "type": "command",
  "id": "4778df20-a695-4b16-9f7a-bb5cf9f153e3",
  "name": "choose_option",
  "payload": {
    "optionIndex": 0
  }
}
```

Payload fields:

- `optionIndex` (`integer`): selected option index

Notes:

- Use `data.selection.options` to inspect the current option set.
- Use `data.actionability.canChooseOption` to determine legality.

## 3.6 `select_target`

Purpose: explicitly select a target when the bridge is waiting for targeting input.

Request by index:

```json
{
  "type": "command",
  "id": "f447d7d6-e9fd-4186-b823-cdc2826fdf06",
  "name": "select_target",
  "payload": {
    "targetIndex": 0
  }
}
```

Request by id:

```json
{
  "type": "command",
  "id": "3e7f8900-a4f0-4f0d-8820-7f18260db69b",
  "name": "select_target",
  "payload": {
    "targetId": "JawWorm"
  }
}
```

Payload fields:

- `targetIndex` (`integer`, optional): target index
- `targetId` (`string`, optional): target id

Notes:

- Send this when targeting is active and the bridge indicates a target is required.

## 3.7 Multi-stage interaction model

Some game interactions are multi-stage. A client must not assume that a successful `play_card` immediately returns the game to a stable primary decision surface.

Follow-up choice, target, discard, confirmation, or card-selection phases may appear through `selection`, `pending`, and `actionability`.

Recommended interpretation:

- `actionability` answers: what command categories are legal right now?
- `selection` answers: what concrete options or entities are being presented right now?
- `pending` answers: has the previous action chain fully settled, or is the runtime still waiting for a follow-up step?

Current protocol guidance:

- prefer the currently documented commands (`play_card`, `select_target`, `choose_option`, `end_turn`) as the stable external action surface
- do not assume every secondary interaction has a dedicated command name yet
- after any command, treat the next snapshot as the source of truth for whether the bridge has entered a secondary decision phase

## 4. Snapshot Data Model

## 4.1 Top-level state fields

`data` contains the current game state.

- `schemaVersion` (`integer`): current snapshot schema version. The current value is `2`.
- `screen` (`string`): current high-level screen or decision domain.
- `lastActionStatus` (`string`): result of the most recent action, typically `NONE`, `SUCCESS`, or `FAIL`.
- `system` (`object`): runtime/system-level state.
- `runMeta` (`object`): run-level state such as hp, gold, floor, potions, relics, and future long-horizon progression data.
- `combat` (`object|null`): combat-specific state.
- `map` (`object|null`): map state.
- `rewards` (`object|null`): reward screen state.
- `rest` (`object|null`): campfire/rest screen state.
- `shop` (`object|null`): shop state.
- `event` (`object|null`): event screen state.
- `selection` (`object|null`): active selection surface.
- `pending` (`object|null`): whether the game is still waiting on an intermediate state.
- `actionability` (`object|null`): command legality and next-action hints.

## 4.2 `screen`

`screen` tells the client what major decision surface it is currently on.

This is the broadest routing field for an external agent.

Typical values may include combat, shop, event, rewards, map, rest, or unknown-like fallback states depending on what the runtime can currently detect.

Recommended usage:

- use `screen` to choose the decision policy family
- use `selection` to inspect the immediate choice surface
- use `actionability` to determine which commands are currently legal

`screen` should not be treated as the only legality source. `actionability` is the stronger signal for whether a command should be sent.

## 4.3 `actionability`

`actionability` is the command-permission summary for the current state.

Fields:

- `canPlayCard` (`boolean`): whether `play_card` is currently legal
- `canChooseOption` (`boolean`): whether `choose_option` is currently legal
- `canEndTurn` (`boolean`): whether `end_turn` is currently legal
- `requiresTarget` (`boolean`): whether the current action flow still requires a target
- `availableCommands` (`string[]`): command names that are currently allowed

This is the most important decision domain for a headless agent.

Recommended usage:

1. wait for a snapshot
2. read `actionability`
3. only issue a command that is explicitly allowed
4. wait for the next snapshot or response before continuing

If `availableCommands` is empty, the client should assume that no externally meaningful action is currently available or that the game is in an intermediate state.

Multi-stage interaction note:

- after `play_card` or another primary command, `availableCommands` may shift from the main action surface into a follow-up interaction surface
- clients should not assume they can continue issuing `play_card` simply because they were previously in combat
- treat `availableCommands` as the strongest legality signal at every step of the action chain

## 4.4 `selection`

`selection` describes the active choice surface.

Fields:

- `kind` (`string|null`): selection category, when known
- `requiresTarget` (`boolean|null`): whether the current selection requires a target
- `options` (`object[]`): indexed options

`selection` is not limited to simple button rows. It is the general secondary interaction surface for headless clients.

Future or runtime-specific `kind` values may include:

- `CARD_CHOICE`
- `DISCARD_CHOICE`
- `EXHAUST_CHOICE`
- `UPGRADE_CHOICE`
- `TRANSFORM_CHOICE`
- `CONFIRMATION`

Each option uses this structure:

- `index` (`integer`): option index used by `choose_option`
- `label` (`string|null`): human-readable label
- `enabled` (`boolean`): whether the option is currently selectable

Interpretation:

- `selection` answers: **what choices are on the screen right now?**
- `actionability` answers: **which command category is legal right now?**

These are related but not identical. A client should inspect both.

## 4.5 `combat`

`combat` contains battle-state information used for tactical decisions.

Fields:

- `playerEnergy` (`integer|null`)
- `playerBlock` (`integer|null`)
- `playerPowers` (`SocsPowerSnapshot[]`)
- `hand` (`SocsHandCardSnapshot[]`)
- `drawPile` (`SocsHandCardSnapshot[]`)
- `discardPile` (`SocsHandCardSnapshot[]`)
- `exhaustPile` (`SocsHandCardSnapshot[]`)
- `enemies` (`SocsEnemySnapshot[]`)

### Player powers

Each power entry:

- `id` (`string`)
- `amount` (`integer|null`)

### Hand / pile cards

Each card entry may contain:

- `index` (`integer`)
- `id` (`string`)
- `name` (`string|null`)
- `playable` (`boolean|null`)
- `energyCost` (`integer|null`)
- `isXCost` (`boolean|null`, planned)
- `targeting` (`string|null`)
- `upgraded` (`boolean|null`)
- `retain` (`boolean|null`, planned)
- `ethereal` (`boolean|null`, planned)
- `type` (`string|null`)
- `baseDamage` (`integer|null`)
- `baseBlock` (`integer|null`)
- `description` (`string|null`)

Interpretation notes:

- `index` is the operational handle for the current snapshot, especially for `play_card`.
- `energyCost`, `baseDamage`, `baseBlock`, and similar reflective fields may be `null` when not discoverable.
- `isXCost` is part of the intended external contract so clients can distinguish special X-cost behavior from ordinary fixed-cost cards.
- `retain` and `ethereal` are part of the intended external contract because they materially affect end-of-turn and multi-turn planning.
- `playable` reflects current legality when the runtime can determine it.
- `targeting` is a string contract intended for client-side enum mapping.

Common `targeting` values a client should be prepared to handle include:

- `SINGLE_ENEMY`
- `ALL_ENEMIES`
- `NONE`
- `PLAYER`

Depending on runtime discoverability or game semantics, additional values may appear. Clients should treat unknown values as forward-compatible enum extensions rather than hard failures.

### Enemies

Each enemy entry may contain:

- `index` (`integer`)
- `id` (`string`)
- `name` (`string|null`)
- `hp` (`integer|null`)
- `block` (`integer|null`)
- `alive` (`boolean|null`)
- `intentType` (`string|null`)
- `intentDamage` (`integer|null`)
- `intentMulti` (`integer|null`)
- `powers` (`SocsPowerSnapshot[]`)

Interpretation notes:

- `intentDamage` may be per-hit damage.
- `intentMulti` indicates multi-hit count when known.
- Missing combat subfields should be treated as partial visibility, not transport failure.

## 4.6 `system`

Fields:

- `timeScale` (`number`): current engine time scale

This is the canonical place to confirm the effect of `set_time_scale`.

## 4.7 `runMeta`

Fields:

- `hp` (`integer|null`)
- `gold` (`integer|null`)
- `floor` (`integer|null`)
- `ascension` (`integer|null`, planned): current ascension difficulty level for run alignment and benchmark reproducibility
- `seed` (`string|null`, planned): run seed for reproducibility, offline analysis, and RL-style evaluation
- `potionCapacity` (`integer|null`)
- `potions` (`SocsPotionSnapshot[]`)
- `relics` (`SocsRelicSnapshot[]`)
- `deck` (`SocsHandCardSnapshot[]`, planned): current master deck for reward, shop, and remove-card decisions

`deck` is part of the intended external contract even though the current runtime model has not exposed it yet. External clients should plan for this field to appear under `runMeta`.

Potion fields:

- `index` (`integer`)
- `id` (`string`)
- `name` (`string|null`)
- `usable` (`boolean|null`)
- `description` (`string|null`)
- `targeting` (`string|null`)

Relic fields:

- `index` (`integer`)
- `id` (`string`)
- `name` (`string|null`)
- `counter` (`integer|null`, planned): dynamic relic progress, charge count, or turn counter when the runtime can expose it

`counter` is recommended for long-horizon planning because some relics require progress tracking before their value can be evaluated correctly.

Master deck card entries use the same shape as `SocsHandCardSnapshot`, but clients should treat `index` as unstable or non-operational outside the current hand. For deck-level reasoning, prefer `id`, `name`, upgrade state, type, and semantic fields.

## 4.8 `map`

Fields:

- `nodes` (`SocsMapNodeSnapshot[]`)
- `currentNode` (`SocsMapNodeSnapshot|null`)

Node fields:

- `index` (`integer`)
- `kind` (`string|null`)
- `label` (`string|null`)
- `enabled` (`boolean`)

## 4.9 `rewards`

Fields:

- `items` (`SocsRewardItemSnapshot[]`)

Reward item fields:

- `index` (`integer`)
- `kind` (`string|null`)
- `label` (`string|null`)
- `enabled` (`boolean`)

## 4.10 `rest`

Fields:

- `options` (`SocsOptionSnapshot[]`)

## 4.11 `shop`

Fields:

- `items` (`SocsShopItemSnapshot[]`)
- `leaveOption` (`SocsOptionSnapshot|null`)

Shop item fields:

- `index` (`integer`)
- `id` (`string`)
- `name` (`string|null`)
- `price` (`integer|null`)
- `kind` (`string|null`)
- `enabled` (`boolean`)

## 4.12 `event`

Fields:

- `eventName` (`string|null`)
- `options` (`SocsOptionSnapshot[]`)

## 4.13 `pending`

Fields:

- `waiting` (`boolean`)
- `reason` (`string|null`)

`pending` indicates that the game is between stable decision points or waiting for a follow-up action.

## 5. Client Decision Model

A recommended headless client loop is:

1. connect to `127.0.0.1:7777`
2. read frames continuously
3. wait for a `snapshot`
4. inspect `data.screen`
5. inspect `data.actionability`
6. inspect `data.selection` and `data.combat` if needed
7. send exactly one legal command
8. wait for the next `response` and/or `snapshot`
9. repeat

Recommended priority order:

1. `actionability`: what can I legally do right now?
2. `selection`: what indexed choice or target is currently exposed?
3. `screen`: what decision policy should I use?
4. `combat` / `shop` / `event` / `rewards` / `map` / `rest`: what domain data should that policy evaluate?

## 6. Stability Rules

SOCS is a best-effort reflective bridge. Clients must tolerate partial data.

Contract rules:

- unknown fields should be ignored
- nullable fields may legitimately be `null`
- lists may legitimately be empty
- a missing domain object usually means that domain is not active or not currently discoverable
- a partially populated object is still a valid snapshot
- clients should re-synchronize from subsequent snapshots instead of assuming a single command response contains the whole truth

Important implications:

- `combat: null` does not necessarily mean an error; it usually means the current screen is not combat
- `intentDamage: null` does not necessarily mean the enemy has no intent; it may mean the value is not currently discoverable
- clients should treat snapshots as the source of truth for current world state after command execution

## 7. Minimal Python Integration Outline

A minimal Python client should implement:

- TCP connection to `127.0.0.1:7777`
- frame reader for 4-byte little-endian length-prefixed JSON
- JSON decoder using UTF-8
- snapshot handler
- command writer using the command envelope shape defined above

A practical integration flow is:

1. connect
2. read a snapshot
3. if `actionability.canPlayCard` is true, choose a card and optionally a target
4. else if `actionability.canChooseOption` is true, pick an option from `selection.options`
5. else if `actionability.canEndTurn` is true, send `end_turn`
6. otherwise wait for the next snapshot

## 8. Non-Goals

External clients should not depend on:

- screen capture
- OCR
- pixel matching
- UI click coordinates
- undocumented internal C# runtime details

The public integration contract is the TCP framed JSON protocol described in this document.
