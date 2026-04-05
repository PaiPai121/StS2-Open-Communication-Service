#!/usr/bin/env python3
import argparse
import json
import socket
import struct
import sys
import uuid
from typing import Any

HOST = "127.0.0.1"
PORT = 7777
MAX_FRAME_BYTES = 1024 * 1024


def read_exact(sock: socket.socket, size: int) -> bytes:
    chunks = []
    remaining = size
    while remaining > 0:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("socket closed")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def read_frame(sock: socket.socket) -> bytes:
    header = read_exact(sock, 4)
    (length,) = struct.unpack("<I", header)
    if length <= 0 or length > MAX_FRAME_BYTES:
        raise ValueError(f"invalid frame length: {length}")
    return read_exact(sock, length)


def write_frame(sock: socket.socket, message: dict[str, Any]) -> None:
    payload = json.dumps(message, ensure_ascii=False).encode("utf-8")
    header = struct.pack("<I", len(payload))
    sock.sendall(header + payload)


def make_command(name: str, payload: dict[str, Any] | None = None) -> dict[str, Any]:
    return {
        "type": "command",
        "id": str(uuid.uuid4()),
        "name": name,
        "payload": payload or {},
    }


def format_value(value: Any, default: str = "?") -> str:
    if value is None:
        return default
    return str(value)


def format_power_list(powers: list[dict[str, Any]] | None) -> str:
    if not powers:
        return "[]"

    items: list[str] = []
    for power in powers:
        name = format_value(power.get("id"), "Unknown")
        amount = power.get("amount")
        items.append(f"{name}: {amount}" if amount is not None else name)
    return "[" + ", ".join(items) + "]"


def format_card_line(card: dict[str, Any]) -> str:
    index = format_value(card.get("index"), "?")
    name = format_value(card.get("name") or card.get("id"), "Unknown")
    cost = format_value(card.get("energyCost"), "?")
    targeting = format_value(card.get("targeting"), "None")
    pieces = [f"[{index}] {name} ({cost})"]

    if card.get("baseDamage") is not None:
        pieces.append(f"Dmg: {card['baseDamage']}")
    if card.get("baseBlock") is not None:
        pieces.append(f"Blk: {card['baseBlock']}")
    pieces.append(f"Target: {targeting}")
    return " | ".join(pieces)


def print_snapshot(message: dict[str, Any]) -> None:
    data = message.get("data") or {}
    run_meta = data.get("runMeta") or {}
    combat = data.get("combat") or {}
    enemies = combat.get("enemies") or []
    hand = combat.get("hand") or []

    print("=" * 42)
    print("[TURN START]")
    print(
        "Player HP: "
        f"{format_value(run_meta.get('hp'))} "
        f"| Block: {format_value(combat.get('playerBlock'), '0')} "
        f"| Energy: {format_value(combat.get('playerEnergy'))}"
    )
    print(f"Player Powers: {format_power_list(combat.get('playerPowers'))}")

    potion_capacity = run_meta.get("potionCapacity")
    potions = run_meta.get("potions") or []
    if potion_capacity is not None or potions:
        potion_items = []
        for potion in potions:
            potion_name = format_value(potion.get("name") or potion.get("id"), "Unknown")
            usable = potion.get("usable")
            if usable is None:
                potion_items.append(potion_name)
            else:
                potion_items.append(f"{potion_name} [usable={usable}]")
        print(
            f"Potions: {len(potions)}/{format_value(potion_capacity)} "
            + ("| " + ", ".join(potion_items) if potion_items else "")
        )

    print("-" * 42)
    print("[ENEMIES]")
    if enemies:
        for enemy in enemies:
            index = format_value(enemy.get("index"), "?")
            name = format_value(enemy.get("name") or enemy.get("id"), "Unknown")
            hp = format_value(enemy.get("hp"))
            block = format_value(enemy.get("block"), "0")
            intent_type = format_value(enemy.get("intentType"), "UNKNOWN")
            intent_damage = format_value(enemy.get("intentDamage"), "?")
            intent_multi = format_value(enemy.get("intentMulti"), "?")
            print(f"({index}) {name} [HP: {hp}] | BLOCK: {block}")
            print(f"    -> Intent: {intent_type} ({intent_damage} x {intent_multi})")
            print(f"    -> Powers: {format_power_list(enemy.get('powers'))}")
    else:
        print("(none)")

    print("-" * 42)
    print("[HAND CARDS]")
    if hand:
        for card in hand:
            print(format_card_line(card))
    else:
        print("(empty)")
    print("=" * 42, flush=True)


def print_message(message: dict[str, Any]) -> None:
    if message.get("type") == "snapshot":
        print_snapshot(message)
        return
    print(json.dumps(message, ensure_ascii=False, indent=2), flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify SOCS TCP snapshots and commands")
    parser.add_argument("--host", default=HOST)
    parser.add_argument("--port", type=int, default=PORT)
    parser.add_argument("--ping", action="store_true", help="send ping after connect")
    parser.add_argument("--set-time-scale", type=float, dest="time_scale", help="send set_time_scale command")
    parser.add_argument("--play-card-index", type=int, help="send play_card with index")
    parser.add_argument("--play-card-id", help="send play_card with cardId")
    parser.add_argument("--read-count", type=int, default=0, help="stop after N received messages (0 = infinite)")
    args = parser.parse_args()

    with socket.create_connection((args.host, args.port)) as sock:
        print(f"connected to {args.host}:{args.port}", file=sys.stderr)

        if args.ping:
            write_frame(sock, make_command("ping"))
        if args.time_scale is not None:
            write_frame(sock, make_command("set_time_scale", {"value": args.time_scale}))
        if args.play_card_index is not None or args.play_card_id:
            payload: dict[str, Any] = {}
            if args.play_card_index is not None:
                payload["index"] = args.play_card_index
            if args.play_card_id:
                payload["cardId"] = args.play_card_id
            write_frame(sock, make_command("play_card", payload))

        received = 0
        while True:
            payload = read_frame(sock)
            message = json.loads(payload.decode("utf-8"))
            print_message(message)
            received += 1
            if args.read_count > 0 and received >= args.read_count:
                return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("\nstopped", file=sys.stderr)
        raise SystemExit(0)
