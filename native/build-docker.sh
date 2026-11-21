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
	win-*) toolchain=windows base=ubuntu:24.04 ;;
	*) toolchain=linux base=ubuntu:20.04 ;;
	esac

	image="libkyrka-net-native-build:$toolchain"
	docker build -q --build-arg BASE_IMAGE="$base" \
	    --build-arg TOOLCHAIN="$toolchain" -t "$image" "$HERE" >/dev/null

	# On Linux, run as the calling user so native/out is not owned by
	# root. Docker Desktop (Windows, macOS) maps file ownership itself.
	user=""
	if [ "$(uname -s)" = Linux ]; then
		user="--user=$(id -u):$(id -g)"
	fi

	# MSYS_NO_PATHCONV stops Git Bash on Windows from rewriting the paths.
	MSYS_NO_PATHCONV=1 docker run --rm $user \
	    -v "$HERE:/src/native" \
	    -e KYRKA_WORK_DIR=/tmp/work \
	    "$image" sh /src/native/build.sh "$rid"
done
