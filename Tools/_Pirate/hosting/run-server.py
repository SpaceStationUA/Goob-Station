#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Watchdog child: game PTY with a local command socket and console log."""

import argparse
import errno
import fcntl
import os
from pathlib import Path
import pty
import selectors
import signal
import socket
import subprocess
import struct
import termios
import sys


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
        master, slave = pty.openpty()
        fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 40, 120, 0, 0))
        process = subprocess.Popen(command, stdin=slave, stdout=slave, stderr=slave, start_new_session=True)
        os.close(slave)
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
                selector.register(master, selectors.EVENT_READ)
                selector.register(listener, selectors.EVENT_READ)
                pty_open = True
                while process.poll() is None or pty_open:
                    for key, _ in selector.select(timeout=1):
                        if key.fileobj == master:
                            try:
                                data = os.read(master, 65536)
                            except OSError as error:
                                if error.errno == errno.EIO:
                                    selector.unregister(master)
                                    pty_open = False
                                    continue
                                raise
                            if data:
                                log.write(data)
                                sys.stdout.buffer.write(data)
                                sys.stdout.buffer.flush()
                            else:
                                selector.unregister(master)
                                pty_open = False
                            continue
                        connection, _ = listener.accept()
                        with connection:
                            connection.settimeout(2)
                            data = bytearray()
                            try:
                                while len(data) <= 4096 and not data.endswith(b"\n"):
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
                                    os.write(master, command_line + b"\r")
                                    connection.sendall(b"OK\n")
                            except (TimeoutError, ConnectionError):
                                # A stalled or disconnected client must not kill the game.
                                continue
                return process.wait()
        finally:
            socket_path.unlink(missing_ok=True)
            os.close(master)
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()


if __name__ == "__main__":
    sys.exit(main())
