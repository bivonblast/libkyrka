#!/bin/sh
#
# Builds libkyrka as a self-contained shared library for one .NET
# runtime identifier (RID). libsodium is compiled from a pinned,
# checksum-verified release and linked statically, so the resulting
# library has no dependencies beyond the C runtime of the platform.
#
# Usage:
#   native/build.sh <rid>
#
# Supported RIDs and the toolchain each one needs:
#   linux-x64    gcc
#   linux-arm64  gcc on arm64, or aarch64-linux-gnu-gcc on x64
#   win-x64      x86_64-w64-mingw32-gcc (cross-compile, as upstream does)
#   osx-arm64    clang (Xcode command line tools)
#
# Output: native/out/<rid>/<library file>
#
# On Windows, run native/build-docker.sh instead; it runs this script
# inside a Linux container that has every toolchain installed.

set -eu

LIBSODIUM_VERSION=1.0.22
LIBSODIUM_SHA256=adbdd8f16149e81ac6078a03aca6fc03b592b89ef7b5ed83841c086191be3349
LIBSODIUM_URL="https://github.com/jedisct1/libsodium/releases/download/${LIBSODIUM_VERSION}-RELEASE/libsodium-${LIBSODIUM_VERSION}.tar.gz"

RID="${1:?usage: build.sh <rid>}"
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK_ROOT="${KYRKA_WORK_DIR:-$HERE/work}"
WORK="$WORK_ROOT/$RID"
OUT="$HERE/out/$RID"
UPSTREAM="$HERE/libkyrka"
JOBS="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2)"

log() {
	printf '==> %s\n' "$*"
}

fail() {
	printf 'error: %s\n' "$*" >&2
	exit 1
}

# Select the toolchain for the requested RID.
#
# libkyrka and libsodium already compile with a stack protector and
# FORTIFY_SOURCE and link with RELRO. HARDENING adds what they leave out
# on Linux: stack clash protection and control-flow protection (CET on
# x64, BTI/PAC on arm64).
configure_toolchain() {
	HOST=""
	AR=ar
	STRIP=strip
	HARDENING=""

	case "$RID" in
	linux-x64)
		CC=gcc
		OSNAME=linux
		LIBFILE=libkyrka.so
		HARDENING="-fstack-clash-protection -fcf-protection=full"
		;;
	linux-arm64)
		OSNAME=linux
		LIBFILE=libkyrka.so
		HARDENING="-fstack-clash-protection -mbranch-protection=standard"
		if [ "$(uname -m)" = "aarch64" ]; then
			CC=gcc
		else
			HOST=aarch64-linux-gnu
			CC=$HOST-gcc
			AR=$HOST-ar
			STRIP=$HOST-strip
		fi
		;;
	win-x64)
		HOST=x86_64-w64-mingw32
		CC=$HOST-gcc
		AR=$HOST-ar
		STRIP=$HOST-strip
		OSNAME=windows
		LIBFILE=kyrka.dll
		;;
	osx-arm64)
		CC=clang
		OSNAME=darwin
		LIBFILE=libkyrka.dylib
		export MACOSX_DEPLOYMENT_TARGET="${MACOSX_DEPLOYMENT_TARGET:-12.0}"
		;;
	*)
		fail "unsupported RID '$RID'"
		;;
	esac

	command -v "$CC" >/dev/null 2>&1 || fail "compiler '$CC' not found"
}

sha256_of() {
	if command -v sha256sum >/dev/null 2>&1; then
		sha256sum "$1" | cut -d ' ' -f 1
	else
		shasum -a 256 "$1" | cut -d ' ' -f 1
	fi
}

# Download libsodium once and verify it against the pinned checksum.
fetch_libsodium() {
	TARBALL="$WORK_ROOT/libsodium-$LIBSODIUM_VERSION.tar.gz"

	if [ ! -f "$TARBALL" ]; then
		log "downloading libsodium $LIBSODIUM_VERSION"
		mkdir -p "$WORK_ROOT"
		curl -fsSL -o "$TARBALL.tmp" "$LIBSODIUM_URL"
		mv "$TARBALL.tmp" "$TARBALL"
	fi

	actual="$(sha256_of "$TARBALL")"
	if [ "$actual" != "$LIBSODIUM_SHA256" ]; then
		rm -f "$TARBALL"
		fail "libsodium checksum mismatch (got $actual)"
	fi
}

build_libsodium() {
	log "building libsodium for $RID"
	rm -rf "$WORK/libsodium-src" "$WORK/sodium"
	mkdir -p "$WORK/libsodium-src"
	tar -xzf "$TARBALL" -C "$WORK/libsodium-src" --strip-components=1

	(
		cd "$WORK/libsodium-src"
		# Setting CFLAGS replaces libsodium's default -O3, so keep it.
		if [ -n "$HARDENING" ]; then
			export CFLAGS="-O3 $HARDENING"
		fi
		./configure ${HOST:+--host=$HOST} CC="$CC" \
		    --prefix="$WORK/sodium" \
		    --disable-shared --enable-static --with-pic \
		    --disable-dependency-tracking >/dev/null
		make -j"$JOBS" >/dev/null
		make install >/dev/null
	)
}

build_libkyrka() {
	log "building libkyrka for $RID"
	rm -rf "$WORK/libkyrka"
	mkdir -p "$WORK/libkyrka"

	[ -f "$UPSTREAM/Makefile" ] || \
	    fail "native/libkyrka is empty, run: git submodule update --init"

	# Build from a private copy so the submodule stays pristine.
	(cd "$UPSTREAM" && tar -cf - --exclude=.git .) | \
	    (cd "$WORK/libkyrka" && tar -xf -)

	# A Windows checkout may have converted line endings to CRLF,
	# which breaks make; normalize the copy back to LF.
	find "$WORK/libkyrka" -type f \( -name '*.c' -o -name '*.h' -o \
	    -name 'Makefile' -o -name '*.mk' -o -name 'RELEASE' \) \
	    -exec perl -pi -e 's/\r$//' {} +

	# The Makefile appends its own flags to CFLAGS from the environment.
	CFLAGS="$HARDENING" make -C "$WORK/libkyrka" -j1 \
	    CC="$CC" AR="$AR" OSNAME="$OSNAME" \
	    LIBSODIUM_PATH="$WORK/sodium" >/dev/null
}

# The exported API is exactly the set of functions in the public header.
generate_export_lists() {
	grep -oE '\bkyrka_[a-z0-9_]+\(' "$UPSTREAM/include/libkyrka/libkyrka.h" | \
	    tr -d '(' | sort -u > "$WORK/exports.txt"

	{
		echo "{ global:"
		sed 's/$/;/' "$WORK/exports.txt"
		echo "local: *; };"
	} > "$WORK/exports.map"

	sed 's/^/_/' "$WORK/exports.txt" > "$WORK/exports.darwin"

	{
		echo "EXPORTS"
		cat "$WORK/exports.txt"
	} > "$WORK/exports.def"
}

# .NET 10 supports glibc 2.27 and later, so the library must not need more.
GLIBC_BASELINE=2.27

# Only used for Linux builds, which always have GNU sort (-V).
check_glibc_baseline() {
	objdump="${HOST:+$HOST-}objdump"
	needed="$("$objdump" -T "$1" | grep -o 'GLIBC_[0-9.]*' | \
	    sed 's/GLIBC_//' | sort -uV | tail -n 1)"
	newest="$(printf '%s\n%s\n' "$needed" "$GLIBC_BASELINE" | sort -V | tail -n 1)"

	[ "$newest" = "$GLIBC_BASELINE" ] || \
	    fail "$LIBFILE needs glibc $needed, newer than the $GLIBC_BASELINE baseline"
	log "glibc requirement: $needed (baseline $GLIBC_BASELINE)"
}

link_shared_library() {
	log "linking $LIBFILE"
	archive="$WORK/libkyrka/libkyrka.a"
	sodium="$WORK/sodium/lib/libsodium.a"
	target="$WORK/$LIBFILE"

	case "$OSNAME" in
	linux)
		"$CC" -shared -o "$target" \
		    -Wl,--whole-archive "$archive" -Wl,--no-whole-archive \
		    "$sodium" -Wl,--version-script="$WORK/exports.map" \
		    -Wl,-z,noexecstack -Wl,-z,relro -Wl,-z,now
		"$STRIP" --strip-unneeded "$target"
		check_glibc_baseline "$target"
		;;
	windows)
		"$CC" -shared -o "$target" "$WORK/exports.def" \
		    -Wl,--whole-archive "$archive" -Wl,--no-whole-archive \
		    "$sodium" -static-libgcc \
		    -Wl,-Bstatic -lwinpthread -Wl,-Bdynamic \
		    -ladvapi32 -lws2_32
		"$STRIP" --strip-unneeded "$target"
		;;
	darwin)
		"$CC" -dynamiclib -o "$target" \
		    -install_name "@rpath/$LIBFILE" \
		    -Wl,-force_load,"$archive" "$sodium" \
		    -Wl,-exported_symbols_list,"$WORK/exports.darwin"
		strip -x "$target"
		;;
	esac

	mkdir -p "$OUT"
	cp "$target" "$OUT/$LIBFILE"
}

configure_toolchain
fetch_libsodium
build_libsodium
build_libkyrka
generate_export_lists
link_shared_library

log "done: native/out/$RID/$LIBFILE"
