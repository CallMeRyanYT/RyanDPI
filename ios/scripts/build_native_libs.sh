#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
IOS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
VENDOR_DIR="$IOS_DIR/Vendor"
BUILD_TMP="$IOS_DIR/.native_build"

rm -rf "$BUILD_TMP"
mkdir -p "$VENDOR_DIR/include" "$VENDOR_DIR/lib" "$BUILD_TMP"

IOS_SDK="$(xcrun --sdk iphoneos --show-sdk-path)"
IOS_CC="$(xcrun --sdk iphoneos -f clang)"
MIN_IOS="-miphoneos-version-min=15.0"

echo "=== 1. Building libciadpi-ios.a (hufrea/byedpi v0.17.3 + ciadpi_bridge) ==="
git clone --depth 1 --branch v0.17.3 https://github.com/hufrea/byedpi.git "$BUILD_TMP/byedpi"

# iOS forbids BSD daemon(3), so disable the optional -D/--daemon CLI path cleanly
python3 - "$BUILD_TMP/byedpi/main.c" <<'PY'
import sys
p = sys.argv[1]
txt = open(p, 'r', encoding='utf-8').read()
txt = txt.replace('#define DAEMON', '/* #define DAEMON disabled on iOS */')
open(p, 'w', encoding='utf-8').write(txt)
PY

BYEDPI_SRC=(packets.c main.c conev.c proxy.c desync.c mpool.c extend.c)
BYEDPI_OBJS=()
for f in "${BYEDPI_SRC[@]}"; do
    EXTRA_DEFS="-D_DEFAULT_SOURCE"
    if [[ "$f" == "main.c" ]]; then
        EXTRA_DEFS="$EXTRA_DEFS -Dmain=ciadpi_main"
    fi
    "$IOS_CC" -isysroot "$IOS_SDK" -arch arm64 $MIN_IOS -O2 -std=c99 \
        -I"$BUILD_TMP/byedpi" $EXTRA_DEFS \
        -Wall -Wno-unused -Wno-unused-parameter \
        -c "$BUILD_TMP/byedpi/$f" -o "$BUILD_TMP/byedpi_${f%.c}.o"
    BYEDPI_OBJS+=("$BUILD_TMP/byedpi_${f%.c}.o")
done

"$IOS_CC" -isysroot "$IOS_SDK" -arch arm64 $MIN_IOS -O2 -std=c11 \
    -I"$BUILD_TMP/byedpi" -I"$IOS_DIR/Native" \
    -c "$IOS_DIR/Native/ciadpi_bridge.c" -o "$BUILD_TMP/ciadpi_bridge.o"
BYEDPI_OBJS+=("$BUILD_TMP/ciadpi_bridge.o")

libtool -static -o "$VENDOR_DIR/lib/libciadpi-ios.a" "${BYEDPI_OBJS[@]}"
cp "$IOS_DIR/Native/ciadpi_bridge.h" "$VENDOR_DIR/include/ciadpi_bridge.h"

echo "=== 2. Running macOS host sanity test of embedded ciadpi iOS presets ==="
MAC_OBJS=()
for f in "${BYEDPI_SRC[@]}"; do
    EXTRA_DEFS="-D_DEFAULT_SOURCE"
    if [[ "$f" == "main.c" ]]; then
        EXTRA_DEFS="$EXTRA_DEFS -Dmain=ciadpi_main"
    fi
    clang -O2 -std=c99 -I"$BUILD_TMP/byedpi" $EXTRA_DEFS \
        -c "$BUILD_TMP/byedpi/$f" -o "$BUILD_TMP/mac_${f%.c}.o"
    MAC_OBJS+=("$BUILD_TMP/mac_${f%.c}.o")
done
clang -O2 -std=c11 -I"$BUILD_TMP/byedpi" -I"$IOS_DIR/Native" \
    -c "$IOS_DIR/Native/ciadpi_bridge.c" -o "$BUILD_TMP/mac_bridge.o"
cat > "$BUILD_TMP/test_ciadpi.c" <<'EOF'
#include <stdio.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include "ciadpi_bridge.h"

static int check_port(int port) {
    int s = socket(AF_INET, SOCK_STREAM, 0);
    if (s < 0) return 0;
    struct sockaddr_in a = {0};
    a.sin_family = AF_INET;
    a.sin_port = htons((uint16_t)port);
    a.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    int ok = (connect(s, (struct sockaddr *)&a, sizeof(a)) == 0);
    close(s);
    return ok;
}

int main(void) {
    const char *ru_args[] = {
        "ciadpi", "-i", "127.0.0.1", "-p", "18081", "-c", "256", "-b", "16384",
        "-u", "3600", "-L", "3", "-a", "3",
        "--hosts", ":googlevideo.com", "--tlsrec", "1+s", "--tlsrec", "-5+se", "--split", "2", "--disorder", "1+s", "--split", "4:14:3+s",
        "--auto=torst,redirect,ssl_err", "--hosts", ":googlevideo.com", "--split", "2", "--disoob", "1+s", "--tlsrec", "-5+se", "--split", "4:14:3+s",
        "--auto=none", "--split", "2", "--disorder", "-1+s", "--tlsrec", "1+s", "--mod-http=h,d",
        "--auto=torst,redirect,ssl_err", "--oob", "1+s", "--tlsrec", "1+s",
        "--auto=torst,redirect,ssl_err"
    };
    int rc = ryandpi_ciadpi_start((int)(sizeof(ru_args)/sizeof(ru_args[0])), ru_args);
    if (rc != 0) { fprintf(stderr, "start failed rc=%d\n", rc); return 1; }
    usleep(350000);
    if (!ryandpi_ciadpi_is_running() || !check_port(18081)) {
        fprintf(stderr, "ciadpi Russia preset did not bind 127.0.0.1:18081!\n");
        ryandpi_ciadpi_stop();
        return 2;
    }
    ryandpi_ciadpi_stop();
    if (ryandpi_ciadpi_is_running()) {
        fprintf(stderr, "ciadpi did not stop cleanly!\n");
        return 3;
    }
    /* Verify restart works cleanly after optind/optreset reset */
    const char *tr_args[] = {
        "ciadpi", "-i", "127.0.0.1", "-p", "18082", "-c", "256", "-b", "16384",
        "-u", "3600", "-L", "3",
        "--tlsrec", "1+s", "--mod-http=h,d",
        "--auto=torst,redirect,ssl_err", "--tlsrec", "1+s", "--split", "1", "--mod-http=h,d",
        "--auto=torst,redirect,ssl_err", "--oob", "1+s", "--tlsrec", "1+s",
        "--auto=torst,redirect,ssl_err"
    };
    rc = ryandpi_ciadpi_start((int)(sizeof(tr_args)/sizeof(tr_args[0])), tr_args);
    usleep(350000);
    if (rc != 0 || !ryandpi_ciadpi_is_running() || !check_port(18082)) {
        fprintf(stderr, "ciadpi restart Turkey preset failed!\n");
        ryandpi_ciadpi_stop();
        return 4;
    }
    ryandpi_ciadpi_stop();
    printf("[PASS] Embedded ciadpi bridge start, port bind, stop & clean restart verified!\n");
    return 0;
}
EOF
clang -O2 -I"$IOS_DIR/Native" "$BUILD_TMP/test_ciadpi.c" "${MAC_OBJS[@]}" "$BUILD_TMP/mac_bridge.o" -lpthread -o "$BUILD_TMP/test_ciadpi"
"$BUILD_TMP/test_ciadpi"

echo "=== 3. Building libhev-socks5-tunnel-ios.a (heiher/hev-socks5-tunnel 2.20.0) ==="
git clone --recursive --depth 1 --branch 2.20.0 https://github.com/heiher/hev-socks5-tunnel.git "$BUILD_TMP/hev-socks5-tunnel"
pushd "$BUILD_TMP/hev-socks5-tunnel" >/dev/null
make PP="$IOS_CC" \
     CC="$IOS_CC" \
     CFLAGS="-isysroot $IOS_SDK -arch arm64 $MIN_IOS" \
     LFLAGS="-isysroot $IOS_SDK -arch arm64 $MIN_IOS" \
     static
libtool -static -o "$VENDOR_DIR/lib/libhev-socks5-tunnel-ios.a" \
    bin/libhev-socks5-tunnel.a \
    third-part/lwip/bin/liblwip.a \
    third-part/yaml/bin/libyaml.a \
    third-part/hev-task-system/bin/libhev-task-system.a
cp src/hev-main.h "$VENDOR_DIR/include/hev-main.h"
popd >/dev/null

rm -rf "$BUILD_TMP"
lipo -info "$VENDOR_DIR/lib/libciadpi-ios.a" "$VENDOR_DIR/lib/libhev-socks5-tunnel-ios.a"
echo "[OK] Native iOS static libraries ready in $VENDOR_DIR/lib"
