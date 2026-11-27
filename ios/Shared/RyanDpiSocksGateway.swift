import Foundation
import Darwin

// MARK: - Swift wrapper for embedded libciadpi-ios.a C bridge

public enum CiadpiController {
    public static func start(args: [String]) -> Int32 {
        let cStrings = args.map { strdup($0) }
        defer { cStrings.forEach { free($0) } }
        var ptrs: [UnsafePointer<CChar>?] = cStrings.map { UnsafePointer($0) }
        return ptrs.withUnsafeMutableBufferPointer { buf in
            guard let base = buf.baseAddress else { return -1 }
            return ryandpi_ciadpi_start(Int32(args.count), base)
        }
    }

    public static func stop() {
        ryandpi_ciadpi_stop()
    }

    public static var isRunning: Bool {
        ryandpi_ciadpi_is_running() != 0
    }

    public static func waitForLocalPort(_ port: Int, timeoutMs: Int = 3500) -> Bool {
        let deadline = Date().addingTimeInterval(Double(timeoutMs) / 1000.0)
        while Date() < deadline {
            if canConnectLoopback(port: port) {
                return true
            }
            usleep(60_000)
        }
        return false
    }

    private static func canConnectLoopback(port: Int) -> Bool {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { return false }
        defer { Darwin.close(fd) }

        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = UInt16(port).bigEndian
        addr.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))

        let res = withUnsafePointer(to: &addr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        return res == 0
    }
}

// MARK: - Encrypted DNS-over-HTTPS (RFC 8484) Resolver with TTL Cache

public final class DohDnsResolver {
    private struct CacheEntry {
        let wire: Data
        let expiresAt: Date
    }

    private let endpoints: [String]
    private let session: URLSession
    private let cacheLock = NSLock()
    private var cache: [String: CacheEntry] = [:]
    private let maxCacheEntries = 512

    public init(endpoints: [String]) {
        self.endpoints = endpoints.isEmpty ? ["https://1.1.1.1/dns-query", "https://8.8.8.8/dns-query"] : endpoints
        let cfg = URLSessionConfiguration.ephemeral
        cfg.timeoutIntervalForRequest = 4.0
        cfg.timeoutIntervalForResource = 5.0
        cfg.httpMaximumConnectionsPerHost = 4
        cfg.waitsForConnectivity = false
        cfg.requestCachePolicy = .reloadIgnoringLocalCacheData
        self.session = URLSession(configuration: cfg)
    }

    public func invalidate() {
        session.invalidateAndCancel()
        cacheLock.lock()
        cache.removeAll()
        cacheLock.unlock()
    }

    public func resolveWireQuery(_ query: Data) -> Data {
        guard query.count >= 12 else { return query }
        let key = extractCacheKey(query)
        if let k = key {
            cacheLock.lock()
            if let entry = cache[k], entry.expiresAt > Date() {
                cacheLock.unlock()
                var hit = entry.wire
                hit[0] = query[0]
                hit[1] = query[1]
                return hit
            }
            cacheLock.unlock()
        }

        for ep in endpoints {
            if let reply = fetchDohSync(endpoint: ep, query: query), reply.count >= 12 {
                if let k = key {
                    let ttl = extractMinTtl(reply)
                    cacheLock.lock()
                    if cache.count >= maxCacheEntries {
                        cache.removeAll(keepingCapacity: true)
                    }
                    cache[k] = CacheEntry(wire: reply, expiresAt: Date().addingTimeInterval(TimeInterval(ttl)))
                    cacheLock.unlock()
                }
                return reply
            }
        }

        // Return SERVFAIL with matching TXID if all DoH endpoints are temporarily unreachable
        var servfail = query
        if servfail.count >= 4 {
            servfail[2] = (query[2] | 0x80)
            servfail[3] = 0x82 // RA=1, RCODE=2 (SERVFAIL)
        }
        return servfail
    }

    private func fetchDohSync(endpoint: String, query: Data) -> Data? {
        guard let url = URL(string: endpoint) else { return nil }
        var req = URLRequest(url: url, timeoutInterval: 3.5)
        req.httpMethod = "POST"
        req.httpBody = query
        req.setValue("application/dns-message", forHTTPHeaderField: "Content-Type")
        req.setValue("application/dns-message", forHTTPHeaderField: "Accept")

        let sem = DispatchSemaphore(value: 0)
        var resultData: Data?
        let task = session.dataTask(with: req) { data, resp, err in
            if err == nil,
               let http = resp as? HTTPURLResponse,
               (200...299).contains(http.statusCode),
               let d = data, d.count >= 12 {
                resultData = d
            }
            sem.signal()
        }
        task.resume()
        _ = sem.wait(timeout: .now() + 3.8)
        return resultData
    }

    private func extractCacheKey(_ packet: Data) -> String? {
        guard packet.count > 16 else { return nil }
        var idx = 12
        var labels: [String] = []
        while idx < packet.count {
            let len = Int(packet[idx])
            if len == 0 {
                idx += 1
                break
            }
            if (len & 0xC0) != 0 || idx + 1 + len > packet.count { return nil }
            let sub = packet.subdata(in: (idx + 1)..<(idx + 1 + len))
            if let s = String(data: sub, encoding: .ascii) {
                labels.append(s.lowercased())
            }
            idx += 1 + len
        }
        guard idx + 4 <= packet.count else { return nil }
        let qtype = (UInt16(packet[idx]) << 8) | UInt16(packet[idx + 1])
        return "\(labels.joined(separator: ".")):\(qtype)"
    }

    private func extractMinTtl(_ packet: Data) -> Int {
        // Safe compact cache TTL (60s-300s) for responsive CDN & IP handoff
        guard packet.count >= 12 else { return 60 }
        let anCount = (Int(packet[6]) << 8) | Int(packet[7])
        return anCount > 0 ? 180 : 30
    }
}

// MARK: - POSIX Socket Helpers

internal enum PosixSocketIO {
    static func configureNoSigPipe(_ fd: Int32) {
        var one: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &one, socklen_t(MemoryLayout<Int32>.size))
    }

    static func configureTcp(_ fd: Int32, timeoutSec: Int = 180) {
        configureNoSigPipe(fd)
        var one: Int32 = 1
        setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &one, socklen_t(MemoryLayout<Int32>.size))
        var tv = timeval(tv_sec: timeoutSec, tv_usec: 0)
        setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))
        setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))
    }

    static func readExact(_ fd: Int32, count: Int) -> [UInt8]? {
        guard count >= 0 else { return nil }
        if count == 0 { return [] }
        var buf = [UInt8](repeating: 0, count: count)
        var offset = 0
        while offset < count {
            let n = buf.withUnsafeMutableBytes { raw -> Int in
                guard let base = raw.baseAddress else { return -1 }
                return Darwin.recv(fd, base.advanced(by: offset), count - offset, 0)
            }
            if n <= 0 {
                if n < 0 && errno == EINTR { continue }
                return nil
            }
            offset += n
        }
        return buf
    }

    @discardableResult
    static func writeAll(_ fd: Int32, bytes: [UInt8]) -> Bool {
        return bytes.withUnsafeBytes { raw in
            guard let base = raw.baseAddress else { return false }
            return writeAllRaw(fd, ptr: base, length: bytes.count)
        }
    }

    @discardableResult
    static func writeAllRaw(_ fd: Int32, ptr: UnsafeRawPointer, length: Int) -> Bool {
        var sent = 0
        while sent < length {
            let n = Darwin.send(fd, ptr.advanced(by: sent), length - sent, 0)
            if n <= 0 {
                if n < 0 && errno == EINTR { continue }
                return false
            }
            sent += n
        }
        return true
    }

    static func connectLoopback(port: Int, timeoutSec: Int = 5) -> Int32 {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { return -1 }
        configureTcp(fd, timeoutSec: timeoutSec)

        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = UInt16(port).bigEndian
        addr.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))

        let res = withUnsafePointer(to: &addr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        if res != 0 {
            Darwin.close(fd)
            return -1
        }
        return fd
    }

    static func spliceBidirectional(a: Int32, b: Int32, isRunning: @escaping () -> Bool) {
        var pfds = [
            pollfd(fd: a, events: Int16(POLLIN), revents: 0),
            pollfd(fd: b, events: Int16(POLLIN), revents: 0)
        ]
        var buf = [UInt8](repeating: 0, count: 16384)
        var aClosed = false
        var bClosed = false

        while isRunning() && (!aClosed || !bClosed) {
            pfds[0].fd = aClosed ? -1 : a
            pfds[0].revents = 0
            pfds[1].fd = bClosed ? -1 : b
            pfds[1].revents = 0

            let rc = Darwin.poll(&pfds, 2, 15000)
            if rc < 0 {
                if errno == EINTR { continue }
                break
            }
            if rc == 0 {
                continue
            }

            if !aClosed && (pfds[0].revents & Int16(POLLIN | POLLHUP | POLLERR)) != 0 {
                let n = buf.withUnsafeMutableBytes { Darwin.recv(a, $0.baseAddress!, 16384, 0) }
                if n <= 0 {
                    aClosed = true
                    Darwin.shutdown(b, SHUT_WR)
                } else if !buf.withUnsafeBytes({ writeAllRaw(b, ptr: $0.baseAddress!, length: n) }) {
                    break
                }
            }

            if !bClosed && (pfds[1].revents & Int16(POLLIN | POLLHUP | POLLERR)) != 0 {
                let n = buf.withUnsafeMutableBytes { Darwin.recv(b, $0.baseAddress!, 16384, 0) }
                if n <= 0 {
                    bClosed = true
                    Darwin.shutdown(a, SHUT_WR)
                } else if !buf.withUnsafeBytes({ writeAllRaw(a, ptr: $0.baseAddress!, length: n) }) {
                    break
                }
            }
        }
    }
}

// MARK: - Local RFC 1928 SOCKS5 Gateway (DoH Port 53 + QUIC UDP/443 Drop -> ciadpi)

public final class RyanDpiSocksGateway {
    private let listenPort: Int
    private let upstreamPort: Int
    private let dohResolver: DohDnsResolver
    private let blockQuicUdp443: Bool

    private let stateLock = NSLock()
    private var running = false
    private var listenFd: Int32 = -1
    private var activeFds = Set<Int32>()

    private let acceptQueue = DispatchQueue(label: "com.ryandpi.ios.socks.accept", qos: .userInitiated)
    private let workerQueue = DispatchQueue(label: "com.ryandpi.ios.socks.worker", qos: .userInitiated, attributes: .concurrent)
    private let dnsQueue = DispatchQueue(label: "com.ryandpi.ios.socks.dns", qos: .userInitiated, attributes: .concurrent)

    public init(listenPort: Int, upstreamCiadpiPort: Int, dohResolver: DohDnsResolver, blockQuicUdp443: Bool = true) {
        self.listenPort = listenPort
        self.upstreamPort = upstreamCiadpiPort
        self.dohResolver = dohResolver
        self.blockQuicUdp443 = blockQuicUdp443
    }

    public func start() throws {
        stateLock.lock()
        if running {
            stateLock.unlock()
            return
        }

        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else {
            stateLock.unlock()
            throw NSError(domain: "RyanDpiSocksGateway", code: Int(errno), userInfo: [NSLocalizedDescriptionKey: "socket() failed"])
        }
        var one: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &one, socklen_t(MemoryLayout<Int32>.size))
        PosixSocketIO.configureNoSigPipe(fd)

        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = UInt16(listenPort).bigEndian
        addr.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))

        let bindRes = withUnsafePointer(to: &addr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindRes == 0, Darwin.listen(fd, 128) == 0 else {
            let err = errno
            Darwin.close(fd)
            stateLock.unlock()
            throw NSError(domain: "RyanDpiSocksGateway", code: Int(err), userInfo: [NSLocalizedDescriptionKey: "bind/listen failed on 127.0.0.1:\(listenPort)"])
        }

        listenFd = fd
        running = true
        stateLock.unlock()

        acceptQueue.async { [weak self] in
            self?.acceptLoop(serverFd: fd)
        }
        RyanDpiLogStore.shared.info("Encrypted DoH SOCKS5 Gateway listening on 127.0.0.1:\(listenPort) -> ciadpi:\(upstreamPort)")
    }

    public func stop() {
        stateLock.lock()
        guard running else {
            stateLock.unlock()
            return
        }
        running = false
        let sfd = listenFd
        listenFd = -1
        let fdsToClose = activeFds
        activeFds.removeAll()
        stateLock.unlock()

        if sfd >= 0 {
            Darwin.shutdown(sfd, SHUT_RDWR)
            Darwin.close(sfd)
        }
        for fd in fdsToClose {
            Darwin.shutdown(fd, SHUT_RDWR)
            Darwin.close(fd)
        }
        dohResolver.invalidate()
    }

    private func isRunning() -> Bool {
        stateLock.lock()
        let r = running
        stateLock.unlock()
        return r
    }

    private func trackFd(_ fd: Int32) {
        stateLock.lock()
        activeFds.insert(fd)
        stateLock.unlock()
    }

    private func untrackAndClose(_ fd: Int32) {
        stateLock.lock()
        activeFds.remove(fd)
        stateLock.unlock()
        Darwin.close(fd)
    }

    private func acceptLoop(serverFd: Int32) {
        while isRunning() {
            var caddr = sockaddr_in()
            var clen = socklen_t(MemoryLayout<sockaddr_in>.size)
            let cfd = withUnsafeMutablePointer(to: &caddr) {
                $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    Darwin.accept(serverFd, $0, &clen)
                }
            }
            if cfd < 0 {
                if errno == EINTR { continue }
                break
            }
            PosixSocketIO.configureTcp(cfd)
            trackFd(cfd)
            workerQueue.async { [weak self] in
                guard let self = self else {
                    Darwin.close(cfd)
                    return
                }
                defer { self.untrackAndClose(cfd) }
                self.handleClient(cfd)
            }
        }
    }

    private func handleClient(_ cfd: Int32) {
        // 1. Greeting
        guard let hdr = PosixSocketIO.readExact(cfd, count: 2), hdr[0] == 0x05 else { return }
        let nMethods = Int(hdr[1])
        if nMethods > 0 {
            guard PosixSocketIO.readExact(cfd, count: nMethods) != nil else { return }
        }
        guard PosixSocketIO.writeAll(cfd, bytes: [0x05, 0x00]) else { return }

        // 2. Request
        guard let reqHdr = PosixSocketIO.readExact(cfd, count: 4), reqHdr[0] == 0x05 else { return }
        let cmd = reqHdr[1]
        let rsv = reqHdr[2]
        let atyp = reqHdr[3]

        let addrPayload: [UInt8]
        let dstHost: String
        switch atyp {
        case 0x01:
            guard let ip4 = PosixSocketIO.readExact(cfd, count: 4) else { return }
            addrPayload = ip4
            dstHost = "\(ip4[0]).\(ip4[1]).\(ip4[2]).\(ip4[3])"
        case 0x03:
            guard let lByte = PosixSocketIO.readExact(cfd, count: 1) else { return }
            let dLen = Int(lByte[0])
            guard let dom = PosixSocketIO.readExact(cfd, count: dLen) else { return }
            addrPayload = [lByte[0]] + dom
            dstHost = String(bytes: dom, encoding: .ascii) ?? ""
        case 0x04:
            guard let ip6 = PosixSocketIO.readExact(cfd, count: 16) else { return }
            addrPayload = ip6
            dstHost = "::"
        default:
            sendSocksReply(cfd, code: 0x08)
            return
        }

        guard let portBytes = PosixSocketIO.readExact(cfd, count: 2) else { return }
        let dstPort = (Int(portBytes[0]) << 8) | Int(portBytes[1])

        switch cmd {
        case 0x01:
            handleTcpConnect(cfd, rsv: rsv, atyp: atyp, addrPayload: addrPayload, portBytes: portBytes, dstHost: dstHost, dstPort: dstPort)
        case 0x03:
            handleUdpAssociate(cfd)
        default:
            sendSocksReply(cfd, code: 0x07)
        }
    }

    private func handleTcpConnect(_ cfd: Int32, rsv: UInt8, atyp: UInt8, addrPayload: [UInt8], portBytes: [UInt8], dstHost: String, dstPort: Int) {
        if dstHost == RyanDpiSettings.virtualDnsIPv4 && dstPort != 53 {
            sendSocksReply(cfd, code: 0x05)
            return
        }
        if dstPort == 53 {
            sendSocksReply(cfd, code: 0x00)
            handleDnsOverTcp(cfd)
            return
        }

        let ufd = PosixSocketIO.connectLoopback(port: upstreamPort, timeoutSec: 5)
        guard ufd >= 0 else {
            sendSocksReply(cfd, code: 0x05)
            return
        }
        trackFd(ufd)
        defer { untrackAndClose(ufd) }

        guard PosixSocketIO.writeAll(ufd, bytes: [0x05, 0x01, 0x00]),
              let upGreet = PosixSocketIO.readExact(ufd, count: 2),
              upGreet[0] == 0x05, upGreet[1] == 0x00 else {
            sendSocksReply(cfd, code: 0x01)
            return
        }

        var req = [UInt8]()
        req.reserveCapacity(6 + addrPayload.count)
        req.append(contentsOf: [0x05, 0x01, rsv, atyp])
        req.append(contentsOf: addrPayload)
        req.append(contentsOf: portBytes)
        guard PosixSocketIO.writeAll(ufd, bytes: req) else {
            sendSocksReply(cfd, code: 0x01)
            return
        }

        guard let repHdr = PosixSocketIO.readExact(ufd, count: 4) else {
            sendSocksReply(cfd, code: 0x01)
            return
        }
        let repAddr: [UInt8]
        switch repHdr[3] {
        case 0x01:
            guard let a = PosixSocketIO.readExact(ufd, count: 4) else { return }
            repAddr = a
        case 0x03:
            guard let l = PosixSocketIO.readExact(ufd, count: 1),
                  let d = PosixSocketIO.readExact(ufd, count: Int(l[0])) else { return }
            repAddr = l + d
        case 0x04:
            guard let a = PosixSocketIO.readExact(ufd, count: 16) else { return }
            repAddr = a
        default:
            sendSocksReply(cfd, code: 0x01)
            return
        }
        guard let repPort = PosixSocketIO.readExact(ufd, count: 2) else { return }

        var fullReply = repHdr
        fullReply.append(contentsOf: repAddr)
        fullReply.append(contentsOf: repPort)
        guard PosixSocketIO.writeAll(cfd, bytes: fullReply), repHdr[1] == 0x00 else { return }

        PosixSocketIO.configureTcp(ufd, timeoutSec: 180)
        PosixSocketIO.spliceBidirectional(a: cfd, b: ufd) { [weak self] in self?.isRunning() ?? false }
    }

    private func handleDnsOverTcp(_ cfd: Int32) {
        while isRunning() {
            guard let lenBytes = PosixSocketIO.readExact(cfd, count: 2) else { break }
            let len = (Int(lenBytes[0]) << 8) | Int(lenBytes[1])
            guard len > 0, len <= 65535, let q = PosixSocketIO.readExact(cfd, count: len) else { break }
            let ans = dohResolver.resolveWireQuery(Data(q))
            var framed = [UInt8](repeating: 0, count: 2 + ans.count)
            framed[0] = UInt8((ans.count >> 8) & 0xFF)
            framed[1] = UInt8(ans.count & 0xFF)
            ans.copyBytes(to: &framed[2], count: ans.count)
            if !PosixSocketIO.writeAll(cfd, bytes: framed) { break }
        }
    }

    private func handleUdpAssociate(_ ctrlFd: Int32) {
        let localUdp = Darwin.socket(AF_INET, SOCK_DGRAM, 0)
        guard localUdp >= 0 else {
            sendSocksReply(ctrlFd, code: 0x01)
            return
        }
        PosixSocketIO.configureNoSigPipe(localUdp)

        var bindAddr = sockaddr_in()
        bindAddr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        bindAddr.sin_family = sa_family_t(AF_INET)
        bindAddr.sin_port = 0
        bindAddr.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))

        let bres = withUnsafePointer(to: &bindAddr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(localUdp, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bres == 0 else {
            Darwin.close(localUdp)
            sendSocksReply(ctrlFd, code: 0x01)
            return
        }

        var boundAddr = sockaddr_in()
        var boundLen = socklen_t(MemoryLayout<sockaddr_in>.size)
        _ = withUnsafeMutablePointer(to: &boundAddr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.getsockname(localUdp, $0, &boundLen)
            }
        }
        let relayPort = Int(UInt16(bigEndian: boundAddr.sin_port))
        trackFd(localUdp)

        let reply: [UInt8] = [
            0x05, 0x00, 0x00, 0x01,
            127, 0, 0, 1,
            UInt8((relayPort >> 8) & 0xFF),
            UInt8(relayPort & 0xFF)
        ]
        guard PosixSocketIO.writeAll(ctrlFd, bytes: reply) else {
            untrackAndClose(localUdp)
            return
        }

        var upCtrlFd: Int32 = -1
        var upUdpFd: Int32 = -1
        var upTarget = sockaddr_in()
        var hasUpRelay = false
        var lastClientAddr = sockaddr_in()
        var hasClientAddr = false

        defer {
            untrackAndClose(localUdp)
            if upUdpFd >= 0 { untrackAndClose(upUdpFd) }
            if upCtrlFd >= 0 { untrackAndClose(upCtrlFd) }
        }

        func ensureUpstreamUdp() -> Bool {
            if hasUpRelay && upUdpFd >= 0 { return true }
            let uctrl = PosixSocketIO.connectLoopback(port: upstreamPort, timeoutSec: 4)
            guard uctrl >= 0 else { return false }
            guard PosixSocketIO.writeAll(uctrl, bytes: [0x05, 0x01, 0x00]),
                  let g = PosixSocketIO.readExact(uctrl, count: 2), g[0] == 0x05, g[1] == 0x00,
                  PosixSocketIO.writeAll(uctrl, bytes: [0x05, 0x03, 0x00, 0x01, 0, 0, 0, 0, 0, 0]),
                  let r = PosixSocketIO.readExact(uctrl, count: 4), r[0] == 0x05, r[1] == 0x00 else {
                Darwin.close(uctrl)
                return false
            }
            var ipBytes: [UInt8] = [127, 0, 0, 1]
            switch r[3] {
            case 0x01:
                guard let a = PosixSocketIO.readExact(uctrl, count: 4) else { Darwin.close(uctrl); return false }
                if !(a[0] == 0 && a[1] == 0 && a[2] == 0 && a[3] == 0) { ipBytes = a }
            case 0x04:
                guard PosixSocketIO.readExact(uctrl, count: 16) != nil else { Darwin.close(uctrl); return false }
            case 0x03:
                guard let l = PosixSocketIO.readExact(uctrl, count: 1),
                      PosixSocketIO.readExact(uctrl, count: Int(l[0])) != nil else { Darwin.close(uctrl); return false }
            default:
                Darwin.close(uctrl)
                return false
            }
            guard let pBytes = PosixSocketIO.readExact(uctrl, count: 2) else { Darwin.close(uctrl); return false }
            let upPort = (UInt16(pBytes[0]) << 8) | UInt16(pBytes[1])
            guard upPort > 0 else { Darwin.close(uctrl); return false }

            let uudp = Darwin.socket(AF_INET, SOCK_DGRAM, 0)
            guard uudp >= 0 else { Darwin.close(uctrl); return false }
            PosixSocketIO.configureNoSigPipe(uudp)

            upTarget = sockaddr_in()
            upTarget.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
            upTarget.sin_family = sa_family_t(AF_INET)
            upTarget.sin_port = upPort.bigEndian
            upTarget.sin_addr = in_addr(s_addr: inet_addr("\(ipBytes[0]).\(ipBytes[1]).\(ipBytes[2]).\(ipBytes[3])"))

            upCtrlFd = uctrl
            upUdpFd = uudp
            trackFd(uctrl)
            trackFd(uudp)
            hasUpRelay = true
            return true
        }

        var buf = [UInt8](repeating: 0, count: 65535)
        while isRunning() {
            var pfds = [
                pollfd(fd: ctrlFd, events: Int16(POLLIN), revents: 0),
                pollfd(fd: localUdp, events: Int16(POLLIN), revents: 0),
                pollfd(fd: hasUpRelay ? upUdpFd : -1, events: Int16(POLLIN), revents: 0)
            ]
            let rc = Darwin.poll(&pfds, 3, 20000)
            if rc < 0 {
                if errno == EINTR { continue }
                break
            }
            if rc == 0 { continue }

            // Control TCP closed -> end UDP association
            if (pfds[0].revents & Int16(POLLIN | POLLHUP | POLLERR)) != 0 {
                var tmp = [UInt8](repeating: 0, count: 16)
                let n = tmp.withUnsafeMutableBytes { Darwin.recv(ctrlFd, $0.baseAddress!, 16, 0) }
                if n <= 0 { break }
            }

            // Packet from hev-socks5-tunnel -> dispatch DNS/QUIC/UDP
            if (pfds[1].revents & Int16(POLLIN)) != 0 {
                var src = sockaddr_in()
                var srclen = socklen_t(MemoryLayout<sockaddr_in>.size)
                let n = buf.withUnsafeMutableBytes { raw -> Int in
                    withUnsafeMutablePointer(to: &src) {
                        $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { sptr in
                            Darwin.recvfrom(localUdp, raw.baseAddress!, 65535, 0, sptr, &srclen)
                        }
                    }
                }
                if n >= 10 {
                    lastClientAddr = src
                    hasClientAddr = true
                    let frag = buf[2]
                    if frag == 0 {
                        let atyp = buf[3]
                        let headerLen: Int
                        switch atyp {
                        case 0x01: headerLen = 10
                        case 0x04: headerLen = 22
                        case 0x03: headerLen = 7 + Int(buf[4])
                        default: headerLen = 0
                        }
                        if headerLen > 0 && n >= headerLen {
                            let dstPort = (Int(buf[headerLen - 2]) << 8) | Int(buf[headerLen - 1])
                            if dstPort == 53 {
                                let hdrCopy = Array(buf[0..<headerLen])
                                let queryData = Data(buf[headerLen..<n])
                                let clientTarget = src
                                dnsQueue.async { [weak self] in
                                    guard let self = self, self.isRunning() else { return }
                                    let ans = self.dohResolver.resolveWireQuery(queryData)
                                    var out = hdrCopy
                                    out.append(contentsOf: ans)
                                    var dest = clientTarget
                                    _ = out.withUnsafeBytes { raw in
                                        withUnsafePointer(to: &dest) {
                                            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { ptr in
                                                Darwin.sendto(localUdp, raw.baseAddress!, out.count, 0, ptr, socklen_t(MemoryLayout<sockaddr_in>.size))
                                            }
                                        }
                                    }
                                }
                            } else if blockQuicUdp443 && dstPort == 443 {
                                // Drop ISP-throttled QUIC UDP/443 -> instant TCP/443 HTTP/2 fallback
                            } else if ensureUpstreamUdp() {
                                var dest = upTarget
                                _ = buf.withUnsafeBytes { raw in
                                    withUnsafePointer(to: &dest) {
                                        $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { ptr in
                                            Darwin.sendto(upUdpFd, raw.baseAddress!, n, 0, ptr, socklen_t(MemoryLayout<sockaddr_in>.size))
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Reply from ciadpi UDP -> send back to hev-socks5-tunnel
            if hasUpRelay && (pfds[2].revents & Int16(POLLIN)) != 0 {
                let n = buf.withUnsafeMutableBytes { Darwin.recv(upUdpFd, $0.baseAddress!, 65535, 0) }
                if n > 0 && hasClientAddr {
                    var dest = lastClientAddr
                    _ = buf.withUnsafeBytes { raw in
                        withUnsafePointer(to: &dest) {
                            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { ptr in
                                Darwin.sendto(localUdp, raw.baseAddress!, n, 0, ptr, socklen_t(MemoryLayout<sockaddr_in>.size))
                            }
                        }
                    }
                }
            }
        }
    }

    private func sendSocksReply(_ fd: Int32, code: UInt8) {
        PosixSocketIO.writeAll(fd, bytes: [0x05, code, 0x00, 0x01, 0, 0, 0, 0, 0, 0])
    }
}
