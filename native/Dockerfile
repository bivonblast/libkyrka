# Build environment for the Linux and Windows libkyrka binaries.
# build-docker.sh builds one image per target family:
#
#   TOOLCHAIN=linux    on ubuntu:20.04 (linux-x64, linux-arm64)
#   TOOLCHAIN=windows  on ubuntu:24.04 (win-x64)
#
# Ubuntu 20.04 is deliberately old: a Linux binary only loads on systems
# whose glibc is at least as new as the symbol versions it links against.
# .NET 10 supports glibc 2.27 and later (e.g. RHEL 8 with 2.28). glibc
# 2.33+ gives fstat() a new symbol version, so a build on 22.04 would
# require 2.33; on 20.04 the newest version needed is 2.25. build.sh
# verifies this after linking.
#
# The Windows binary needs a newer distribution because libkyrka uses
# htonll(), which older mingw-w64 releases lack.
ARG BASE_IMAGE=ubuntu:20.04
FROM ${BASE_IMAGE}
ARG TOOLCHAIN=linux

# For Windows, the posix thread model provides the winpthread
# clock_gettime() that libkyrka uses.
RUN set -e; \
    case "$TOOLCHAIN" in \
    linux)   packages="gcc-aarch64-linux-gnu binutils-aarch64-linux-gnu libc6-dev-arm64-cross" ;; \
    windows) packages="gcc-mingw-w64-x86-64-posix binutils-mingw-w64-x86-64" ;; \
    *)       echo "unknown TOOLCHAIN $TOOLCHAIN" >&2; exit 1 ;; \
    esac; \
    apt-get update; \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        build-essential ca-certificates curl perl $packages; \
    rm -rf /var/lib/apt/lists/*; \
    if [ "$TOOLCHAIN" = windows ]; then \
        update-alternatives --set x86_64-w64-mingw32-gcc /usr/bin/x86_64-w64-mingw32-gcc-posix; \
    fi

WORKDIR /src
