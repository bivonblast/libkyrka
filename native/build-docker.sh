#!/bin/sh
#
# Builds the Linux and Windows libkyrka binaries inside a Docker
# container, so it works the same on Windows, macOS, Linux and in CI.
#
# Usage:
#   native/build-docker.sh [rid ...]
#
# Without arguments it builds win-x64, linux-x64 and linux-arm64.
# osx-arm64 cannot be built in a container; run native/build.sh on a Mac.

set -eu

HERE="$(cd "$(dirname "$0")" && pwd)"

if [ "$#" -eq 0 ]; then
	set -- win-x64 linux-x64 linux-arm64
fi

for rid in "$@"; do
	# Linux builds use an old glibc for compatibility; the Windows
	# build needs a newer mingw-w64 (see Dockerfile).
	case "$rid" in
	win-*) base=ubuntu:24.04 ;;
	*) base=ubuntu:22.04 ;;
	esac

	image="libkyrka-net-native-build:$(echo "$base" | tr ':' '-')"
	docker build -q --build-arg BASE_IMAGE="$base" -t "$image" "$HERE" >/dev/null

	# MSYS_NO_PATHCONV stops Git Bash on Windows from rewriting the paths.
	MSYS_NO_PATHCONV=1 docker run --rm \
	    -v "$HERE:/src/native" \
	    -e KYRKA_WORK_DIR=/tmp/work \
	    "$image" sh /src/native/build.sh "$rid"
done
