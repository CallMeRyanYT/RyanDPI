import Foundation
import Darwin

/// Lightweight POSIX HTTP/1.1 + HTTPS CONNECT local proxy listening on 127.0.0.1:8080
/// and forwarding every request through local SOCKS5 (127.0.0.1:1080 -> ciadpi).
///
/// Enables full DPI bypass on iPhones sideloaded with a FREE Apple ID (AltStore / SideStore / Sideloadly)
/// where Apple restricts the paid NetworkExtension VPN entitlement: users simply set
/// Settings -> Wi-Fi -> (i) -> Configure Proxy -> Manual -> 127.0.0.1 : 8080!
public final class LocalHttpProxyServer {
    private let listenPort: Int
    private let socksPort: Int

    private let stateLock = NSLock()
    private var running = false
    private var listenFd: Int32 = -1
    private var activeFds = Set<Int32>()

    private let acceptQueue = DispatchQueue(label: "com.ryandpi.ios.httpproxy.accept", qos: .userInitiated)
    private let workerQueue = DispatchQueue(label: "com.ryandpi.ios.httpproxy.worker", qos: .userInitiated, attributes: .concurrent)

    public init(listenPort: Int = RyanDpiSettings.localHttpProxyPort, socksPort: Int = RyanDpiSettings.gatewayPort) {
        self.listenPort = listenPort
        self.socksPort = socksPort
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
            throw NSError(domain: "LocalHttpProxyServer", code: Int(errno), userInfo: [NSLocalizedDescriptionKey: "socket() failed"])
        }
        var one: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &one, socklen_t(MemoryLayout<Int32>.size))
        PosixSocketIO.configureNoSigPipe(fd)

        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = UInt16(listenPort).bigEndian
        addr.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))

        let bres = withUnsafePointer(to: &addr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bres == 0, Darwin.listen(fd, 128) == 0 else {
            let err = errno
            Darwin.close(fd)
            stateLock.unlock()
            throw NSError(domain: "LocalHttpProxyServer", code: Int(err), userInfo: [NSLocalizedDescriptionKey: "bind/listen failed on 127.0.0.1:\(listenPort)"])
        }

        listenFd = fd
        running = true
        stateLock.unlock()

        acceptQueue.async { [weak self] in
            self?.acceptLoop(serverFd: fd)
        }
        RyanDpiLogStore.shared.info("Local Wi-Fi HTTP/HTTPS Proxy listening on 127.0.0.1:\(listenPort) -> SOCKS5 127.0.0.1:\(socksPort)")
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
        let fds = activeFds
        activeFds.removeAll()
        stateLock.unlock()

        if sfd >= 0 {
            Darwin.shutdown(sfd, SHUT_RDWR)
            Darwin.close(sfd)
        }
        for fd in fds {
            Darwin.shutdown(fd, SHUT_RDWR)
            Darwin.close(fd)
        }
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
                guard let self = self else { Darwin.close(cfd); return }
                defer { self.untrackAndClose(cfd) }
                self.handleClient(cfd)
            }
        }
    }

    private func handleClient(_ cfd: Int32) {
        var headerBuf = [UInt8]()
        headerBuf.reserveCapacity(2048)
        var tmp = [UInt8](repeating: 0, count: 1024)
        var headerEndIdx = -1

        while headerBuf.count < 16384 {
            let n = tmp.withUnsafeMutableBytes { Darwin.recv(cfd, $0.baseAddress!, 1024, 0) }
            if n <= 0 { return }
            headerBuf.append(contentsOf: tmp[0..<n])
            if let idx = findHeaderTerminator(headerBuf) {
                headerEndIdx = idx
                break
            }
        }
        guard headerEndIdx > 0 else { return }

        guard let headerStr = String(bytes: headerBuf[0..<headerEndIdx], encoding: .utf8) ??
                              String(bytes: headerBuf[0..<headerEndIdx], encoding: .isoLatin1) else { return }
        let lines = headerStr.components(separatedBy: "\r\n")
        guard let reqLine = lines.first else { return }
        let parts = reqLine.split(separator: " ")
        guard parts.count >= 3 else { return }
        let method = String(parts[0]).uppercased()
        let target = String(parts[1])
        let version = String(parts[2])

        if method == "CONNECT" {
            guard let (host, port) = parseHostPort(target, defaultPort: 443) else {
                sendSimpleHttp(cfd, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n")
                return
            }
            let ufd = connectViaSocks5(host: host, port: port)
            guard ufd >= 0 else {
                sendSimpleHttp(cfd, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
                return
            }
            trackFd(ufd)
            defer { untrackAndClose(ufd) }

            sendSimpleHttp(cfd, "\(version) 200 Connection Established\r\n\r\n")
            if headerEndIdx < headerBuf.count {
                let extra = Array(headerBuf[headerEndIdx..<headerBuf.count])
                _ = PosixSocketIO.writeAll(ufd, bytes: extra)
            }
            PosixSocketIO.spliceBidirectional(a: cfd, b: ufd) { [weak self] in self?.isRunning() ?? false }
        } else {
            // Plain HTTP proxy request: extract host/port from absolute URL or Host header
            let (host, port, rewrittenPath) = parseHttpRequestTarget(target: target, headers: lines)
            guard !host.isEmpty else {
                sendSimpleHttp(cfd, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n")
                return
            }
            let ufd = connectViaSocks5(host: host, port: port)
            guard ufd >= 0 else {
                sendSimpleHttp(cfd, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
                return
            }
            trackFd(ufd)
            defer { untrackAndClose(ufd) }

            var rebuilt = "\(method) \(rewrittenPath) \(version)\r\n"
            for line in lines.dropFirst() where !line.isEmpty {
                if line.lowercased().hasPrefix("proxy-connection:") { continue }
                rebuilt += "\(line)\r\n"
            }
            rebuilt += "Connection: close\r\n\r\n"
            guard PosixSocketIO.writeAll(ufd, bytes: Array(rebuilt.utf8)) else { return }
            if headerEndIdx < headerBuf.count {
                let extra = Array(headerBuf[headerEndIdx..<headerBuf.count])
                _ = PosixSocketIO.writeAll(ufd, bytes: extra)
            }
            PosixSocketIO.spliceBidirectional(a: cfd, b: ufd) { [weak self] in self?.isRunning() ?? false }
        }
    }

    private func connectViaSocks5(host: String, port: Int) -> Int32 {
        let ufd = PosixSocketIO.connectLoopback(port: socksPort, timeoutSec: 5)
        guard ufd >= 0 else { return -1 }

        guard PosixSocketIO.writeAll(ufd, bytes: [0x05, 0x01, 0x00]),
              let g = PosixSocketIO.readExact(ufd, count: 2), g[0] == 0x05, g[1] == 0x00 else {
            Darwin.close(ufd)
            return -1
        }

        let domBytes = Array(host.utf8)
        guard domBytes.count <= 255 else {
            Darwin.close(ufd)
            return -1
        }
        var req: [UInt8] = [0x05, 0x01, 0x00, 0x03, UInt8(domBytes.count)]
        req.append(contentsOf: domBytes)
        req.append(UInt8((port >> 8) & 0xFF))
        req.append(UInt8(port & 0xFF))

        guard PosixSocketIO.writeAll(ufd, bytes: req),
              let rep = PosixSocketIO.readExact(ufd, count: 4), rep[0] == 0x05, rep[1] == 0x00 else {
            Darwin.close(ufd)
            return -1
        }

        switch rep[3] {
        case 0x01: _ = PosixSocketIO.readExact(ufd, count: 6)
        case 0x04: _ = PosixSocketIO.readExact(ufd, count: 18)
        case 0x03:
            if let l = PosixSocketIO.readExact(ufd, count: 1) {
                _ = PosixSocketIO.readExact(ufd, count: Int(l[0]) + 2)
            }
        default: break
        }

        PosixSocketIO.configureTcp(ufd, timeoutSec: 180)
        return ufd
    }

    private func findHeaderTerminator(_ buf: [UInt8]) -> Int? {
        guard buf.count >= 4 else { return nil }
        for i in 0...(buf.count - 4) {
            if buf[i] == 13 && buf[i + 1] == 10 && buf[i + 2] == 13 && buf[i + 3] == 10 {
                return i + 4
            }
        }
        return nil
    }

    private func parseHostPort(_ raw: String, defaultPort: Int) -> (String, Int)? {
        if raw.hasPrefix("["), let closeIdx = raw.firstIndex(of: "]") {
            let host = String(raw[raw.index(after: raw.startIndex)..<closeIdx])
            let rest = raw[raw.index(after: closeIdx)...]
            if rest.hasPrefix(":"), let p = Int(rest.dropFirst()) { return (host, p) }
            return (host, defaultPort)
        }
        if let colon = raw.lastIndex(of: ":"), let p = Int(raw[raw.index(after: colon)...]) {
            return (String(raw[..<colon]), p)
        }
        return raw.isEmpty ? nil : (raw, defaultPort)
    }

    private func parseHttpRequestTarget(target: String, headers: [String]) -> (String, Int, String) {
        if let url = URL(string: target), let h = url.host {
            let p = url.port ?? 80
            var path = url.path.isEmpty ? "/" : url.path
            if let q = url.query { path += "?\(q)" }
            return (h, p, path)
        }
        for line in headers.dropFirst() {
            if line.lowercased().hasPrefix("host:") {
                let val = line.dropFirst(5).trimmingCharacters(in: .whitespaces)
                if let (h, p) = parseHostPort(val, defaultPort: 80) {
                    return (h, p, target.isEmpty ? "/" : target)
                }
            }
        }
        return ("", 80, "/")
    }

    private func sendSimpleHttp(_ fd: Int32, _ text: String) {
        PosixSocketIO.writeAll(fd, bytes: Array(text.utf8))
    }
}
