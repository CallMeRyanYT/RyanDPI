#ifndef RYANDPI_CIADPI_BRIDGE_H
#define RYANDPI_CIADPI_BRIDGE_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Starts embedded ciadpi (ByeDPI) in-process on a background pthread.
 * Returns 0 if started cleanly, non-zero if already running or invalid args.
 */
int ryandpi_ciadpi_start(int argc, const char *const *argv);

/**
 * Cleanly shuts down the listening socket and joins the ciadpi pthread.
 */
void ryandpi_ciadpi_stop(void);

/**
 * Returns 1 if ciadpi thread is currently running, 0 otherwise.
 */
int ryandpi_ciadpi_is_running(void);

/**
 * Scans open file descriptors inside NEPacketTunnelProvider to locate Apple's utun socket FD.
 * Works reliably across iOS 15, 16, 17, 18, and newer without relying on private KVC keys alone.
 * Returns the utun file descriptor (>= 0), or -1 if not found.
 */
int ryandpi_find_utun_fd(void);

#ifdef __cplusplus
}
#endif

#endif /* RYANDPI_CIADPI_BRIDGE_H */
