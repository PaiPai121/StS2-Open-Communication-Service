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


def print_message(message: dict[str, Any]) -> None:
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
