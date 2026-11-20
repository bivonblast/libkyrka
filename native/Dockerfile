# Build environment for the Linux and Windows libkyrka binaries.
#
# Ubuntu 22.04 is deliberately old: Linux binaries only run on systems
# with a glibc at least as new as the one they were built against, and
# glibc 2.35 matches the oldest distributions .NET 10 supports.
#
# The Windows binary is built from ubuntu:24.04 instead (BASE_IMAGE build
# arg), because libkyrka needs htonll(), which the mingw-w64 8 in 22.04 lacks.
ARG BASE_IMAGE=ubuntu:22.04
FROM ${BASE_IMAGE}

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      build-essential \
      ca-certificates \
      curl \
      gcc-aarch64-linux-gnu \
      binutils-aarch64-linux-gnu \
      libc6-dev-arm64-cross \
      gcc-mingw-w64-x86-64-posix \
      binutils-mingw-w64-x86-64 \
      perl \
 && rm -rf /var/lib/apt/lists/*

# The posix thread model provides the winpthread clock_gettime libkyrka uses.
RUN update-alternatives --set x86_64-w64-mingw32-gcc /usr/bin/x86_64-w64-mingw32-gcc-posix

WORKDIR /src
