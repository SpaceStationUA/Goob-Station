#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Watchdog child: game process with a local command socket and console log."""

import argparse
import selectors
import fcntl
import os
from pathlib import Path
import sys
import signal
import socket
import subprocess
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--socket", required=True)
    parser.add_argument("--log", required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    options = parser.parse_args()
    command = options.command
    if command[:1] == ["--"]:
        command = command[1:]
    if not command:
        parser.error("missing server command")

    socket_path = Path(options.socket)
    socket_path.parent.mkdir(parents=True, exist_ok=True)
    log_path = Path(options.log)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    # A lock prevents a second wrapper from unlinking an active command socket.
    with open(str(socket_path) + ".lock", "a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        socket_path.unlink(missing_ok=True)
        process = subprocess.Popen(
            command,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            start_new_session=True,
            bufsize=0,
        )
        assert process.stdin is not None and process.stdout is not None
        child_stdin = process.stdin
        child_stdout = process.stdout
        stopping = False

        def terminate(signum, _frame):
            nonlocal stopping
            stopping = True
            try:
                os.killpg(process.pid, signum)
            except ProcessLookupError:
                pass

        signal.signal(signal.SIGTERM, terminate)
        signal.signal(signal.SIGINT, terminate)
        try:
            with socket.socket(socket.AF_UNIX) as listener, selectors.DefaultSelector() as selector, open(log_path, "ab", buffering=0) as log:
                listener.bind(str(socket_path))
                os.chmod(socket_path, 0o600)
                listener.listen(8)
                listener.setblocking(False)
                child_stdout_fd = child_stdout.fileno()
                os.set_blocking(child_stdout_fd, False)
                selector.register(child_stdout_fd, selectors.EVENT_READ)
                selector.register(listener, selectors.EVENT_READ)
                output_open = True
                while process.poll() is None or output_open:
                    for key, _ in selector.select(timeout=1):
                        if key.fileobj == child_stdout_fd:
                            try:
                                data = os.read(child_stdout_fd, 65536)
                            except BlockingIOError:
                                continue
                            if data:
                                log.write(data)
                                sys.stdout.buffer.write(data)
                                sys.stdout.buffer.flush()
                            else:
                                selector.unregister(child_stdout_fd)
                                output_open = False
                            continue
                        connection, _ = listener.accept()
                        with connection:
                            data = bytearray()
                            request_deadline = time.monotonic() + 2
                            try:
                                while len(data) <= 4096 and not data.endswith(b"\n"):
                                    remaining = request_deadline - time.monotonic()
                                    if remaining <= 0:
                                        raise TimeoutError
                                    connection.settimeout(remaining)
                                    part = connection.recv(4097 - len(data))
                                    if not part:
                                        break
                                    data.extend(part)
                                command_line = bytes(data[:-1]) if data.endswith(b"\n") else b""
                                if stopping or process.poll() is not None:
                                    connection.sendall(b"ERR server stopping\n")
                                elif not command_line or len(data) > 4096 or any(byte < 32 or byte == 127 for byte in command_line):
                                    connection.sendall(b"ERR invalid command\n")
                                else:
                                    child_stdin.write(command_line + b"\n")
                                    child_stdin.flush()
                                    connection.sendall(b"OK\n")
                            except (TimeoutError, ConnectionError, BrokenPipeError):
                                # A stalled or disconnected client must not kill the game.
                                continue
                return process.wait()
        finally:
            socket_path.unlink(missing_ok=True)
            child_stdin.close()
            child_stdout.close()
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()


if __name__ == "__main__":
    sys.exit(main())
