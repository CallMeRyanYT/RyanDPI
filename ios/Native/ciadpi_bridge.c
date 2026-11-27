#include "ciadpi_bridge.h"

#include <arpa/inet.h>
#include <errno.h>
#include <net/if.h>
#include <netinet/in.h>
#include <pthread.h>
#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/kern_control.h>
#include <sys/socket.h>
#include <sys/sys_domain.h>
#include <unistd.h>

#include "params.h"

#ifndef UTUN_OPT_IFNAME
#define UTUN_OPT_IFNAME 2
#endif

/* Exported by libciadpi.a compiled with -Dmain=ciadpi_main */
extern int ciadpi_main(int argc, char **argv);
extern int server_fd;
extern struct params params;

/* Darwin getopt_long global reset flags */
extern int optind;
extern int optreset;

typedef struct {
    int argc;
    char **argv;
} ciadpi_thread_args_t;

static pthread_mutex_t g_ciadpi_lock = PTHREAD_MUTEX_INITIALIZER;
static pthread_t g_ciadpi_thread;
static atomic_int g_ciadpi_running = 0;
static int g_thread_valid = 0;

static void reset_ciadpi_defaults(void)
{
    memset(&params, 0, sizeof(params));
    params.await_int = 10;
    params.ipv6 = 1;
    params.resolve = 1;
    params.udp = 1;
    params.max_open = 256;
    params.bfsize = 16384;
    params.baddr.in6.sin6_family = AF_INET6;
    params.laddr.in.sin_family = AF_INET;
    params.debug = 0;
    server_fd = -1;
    optind = 1;
    optreset = 1;
}

static void free_thread_args(ciadpi_thread_args_t *ta)
{
    if (!ta) return;
    if (ta->argv) {
        for (int i = 0; i < ta->argc; i++) {
            free(ta->argv[i]);
        }
        free(ta->argv);
    }
    free(ta);
}

static void *ciadpi_worker(void *ctx)
{
    ciadpi_thread_args_t *ta = (ciadpi_thread_args_t *)ctx;
    reset_ciadpi_defaults();
    (void)ciadpi_main(ta->argc, ta->argv);
    atomic_store(&g_ciadpi_running, 0);
    free_thread_args(ta);
    return NULL;
}

int ryandpi_ciadpi_start(int argc, const char *const *argv)
{
    if (argc <= 0 || !argv) return -1;

    pthread_mutex_lock(&g_ciadpi_lock);
    if (atomic_load(&g_ciadpi_running) != 0) {
        pthread_mutex_unlock(&g_ciadpi_lock);
        return -2;
    }

    if (g_thread_valid) {
        pthread_join(g_ciadpi_thread, NULL);
        g_thread_valid = 0;
    }

    ciadpi_thread_args_t *ta = (ciadpi_thread_args_t *)calloc(1, sizeof(ciadpi_thread_args_t));
    if (!ta) {
        pthread_mutex_unlock(&g_ciadpi_lock);
        return -3;
    }
    ta->argc = argc;
    ta->argv = (char **)calloc((size_t)argc + 1, sizeof(char *));
    if (!ta->argv) {
        free(ta);
        pthread_mutex_unlock(&g_ciadpi_lock);
        return -3;
    }
    for (int i = 0; i < argc; i++) {
        ta->argv[i] = strdup(argv[i] ? argv[i] : "");
        if (!ta->argv[i]) {
            free_thread_args(ta);
            pthread_mutex_unlock(&g_ciadpi_lock);
            return -3;
        }
    }

    atomic_store(&g_ciadpi_running, 1);

    pthread_attr_t attr;
    pthread_attr_init(&attr);
    /* Give ciadpi a compact 512 KB stack to keep iOS NetworkExtension RSS well below 50 MB */
    pthread_attr_setstacksize(&attr, 512 * 1024);

    int rc = pthread_create(&g_ciadpi_thread, &attr, ciadpi_worker, ta);
    pthread_attr_destroy(&attr);
    if (rc != 0) {
        atomic_store(&g_ciadpi_running, 0);
        free_thread_args(ta);
        pthread_mutex_unlock(&g_ciadpi_lock);
        return -4;
    }

    g_thread_valid = 1;
    pthread_mutex_unlock(&g_ciadpi_lock);
    return 0;
}

void ryandpi_ciadpi_stop(void)
{
    pthread_mutex_lock(&g_ciadpi_lock);
    int fd = server_fd;
    if (fd > 0) {
        /* Match hufrea/byedpi on_cancel(): shutdown wakes poll()/accept() and destroy_pool() closes fd cleanly */
        shutdown(fd, SHUT_RDWR);
    }
    if (g_thread_valid) {
        pthread_t th = g_ciadpi_thread;
        g_thread_valid = 0;
        pthread_mutex_unlock(&g_ciadpi_lock);
        pthread_join(th, NULL);
        atomic_store(&g_ciadpi_running, 0);
        return;
    }
    atomic_store(&g_ciadpi_running, 0);
    pthread_mutex_unlock(&g_ciadpi_lock);
}

int ryandpi_ciadpi_is_running(void)
{
    return atomic_load(&g_ciadpi_running);
}

int ryandpi_find_utun_fd(void)
{
    struct ctl_info ctlInfo;
    memset(&ctlInfo, 0, sizeof(ctlInfo));
    strncpy(ctlInfo.ctl_name, "com.apple.net.utun_control", sizeof(ctlInfo.ctl_name) - 1);

    for (int fd = 0; fd < 1024; fd++) {
        struct sockaddr_ctl addr;
        socklen_t len = sizeof(addr);
        if (getpeername(fd, (struct sockaddr *)&addr, &len) == 0 &&
            addr.sc_family == AF_SYSTEM &&
            addr.ss_sysaddr == AF_SYS_CONTROL) {
            char ifname[IFNAMSIZ];
            socklen_t iflen = sizeof(ifname);
            memset(ifname, 0, sizeof(ifname));
            if (getsockopt(fd, SYSPROTO_CONTROL, UTUN_OPT_IFNAME, ifname, &iflen) == 0 &&
                strncmp(ifname, "utun", 4) == 0) {
                return fd;
            }
        }
    }
    return -1;
}
