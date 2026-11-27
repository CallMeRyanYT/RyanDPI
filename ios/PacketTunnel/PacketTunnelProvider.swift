import Foundation
import NetworkExtension

final class PacketTunnelProvider: NEPacketTunnelProvider {
    private var socksGateway: RyanDpiSocksGateway?
    private var tunnelThread: Thread?
    private var connectedAt: Date?
    private let stopLock = NSLock()
    private var isStopped = false

    override func startTunnel(options: [String: NSObject]?, completionHandler: @escaping (Error?) -> Void) {
        RyanDpiLogStore.shared.info("Starting RyanDPI iOS NetworkExtension tunnel...")

        var settings = RyanDpiSettings.load()
        if let proto = protocolConfiguration as? NETunnelProviderProtocol,
           let json = proto.providerConfiguration?["settingsJson"] as? String,
           let data = json.data(using: .utf8),
           let decoded = try? JSONDecoder().decode(RyanDpiSettings.self, from: data) {
            settings = decoded
        }

        var socksHost = "127.0.0.1"
        var socksPort = RyanDpiSettings.gatewayPort
        var socksUser: String? = nil
        var socksPass: String? = nil

        if settings.region == .brazil, let custom = settings.parsedCustomSocksEndpoint() {
            socksHost = custom.host
            socksPort = custom.port
            if !settings.customSocksUsername.trimmingCharacters(in: .whitespaces).isEmpty {
                socksUser = settings.customSocksUsername.trimmingCharacters(in: .whitespaces)
                socksPass = settings.customSocksPassword
            }
            RyanDpiLogStore.shared.info("Brazil Custom SOCKS5 tunnel mode: \(socksHost):\(socksPort)")
        } else {
            let ciadpiPort = RyanDpiSettings.ciadpiInternalPort
            let args = settings.buildCiadpiArgs(listenPort: ciadpiPort)
            let rc = CiadpiController.start(args: args)
            guard rc == 0, CiadpiController.waitForLocalPort(ciadpiPort, timeoutMs: 3500) else {
                let err = NSError(domain: "RyanDPI.PacketTunnel", code: 1001, userInfo: [
                    NSLocalizedDescriptionKey: "Embedded ciadpi DPI engine failed to bind 127.0.0.1:\(ciadpiPort) (rc=\(rc))."
                ])
                RyanDpiLogStore.shared.error(err.localizedDescription)
                CiadpiController.stop()
                completionHandler(err)
                return
            }

            let resolver = DohDnsResolver(endpoints: settings.orderedDohEndpoints)
            let gateway = RyanDpiSocksGateway(
                listenPort: RyanDpiSettings.gatewayPort,
                upstreamCiadpiPort: ciadpiPort,
                dohResolver: resolver,
                blockQuicUdp443: settings.blockQuicUdp443 && settings.region != .brazil
            )
            do {
                try gateway.start()
                self.socksGateway = gateway
            } catch {
                RyanDpiLogStore.shared.error("SOCKS5 DoH gateway start failed: \(error.localizedDescription)")
                CiadpiController.stop()
                completionHandler(error)
                return
            }
        }

        let tunnelSettings = NEPacketTunnelNetworkSettings(tunnelRemoteAddress: "127.0.0.1")
        tunnelSettings.mtu = NSNumber(value: RyanDpiSettings.tunMtu)

        let ipv4 = NEIPv4Settings(addresses: [RyanDpiSettings.virtualTunIPv4], subnetMasks: ["255.255.255.255"])
        ipv4.includedRoutes = [NEIPv4Route.default()]
        ipv4.excludedRoutes = [
            NEIPv4Route(destinationAddress: "127.0.0.0", subnetMask: "255.0.0.0"),
            NEIPv4Route(destinationAddress: "192.168.0.0", subnetMask: "255.255.0.0"),
            NEIPv4Route(destinationAddress: "172.16.0.0", subnetMask: "255.240.0.0")
        ]
        tunnelSettings.ipv4Settings = ipv4

        let dnsServer = (settings.region == .brazil && settings.parsedCustomSocksEndpoint() != nil)
            ? settings.dohProvider.bootstrapIPs.first ?? "1.1.1.1"
            : RyanDpiSettings.virtualDnsIPv4
        let dns = NEDNSSettings(servers: [dnsServer])
        dns.matchDomains = [""]
        tunnelSettings.dnsSettings = dns

        setTunnelNetworkSettings(tunnelSettings) { [weak self] error in
            guard let self = self else { return }
            if let error = error {
                RyanDpiLogStore.shared.error("setTunnelNetworkSettings failed: \(error.localizedDescription)")
                self.cleanupEngines()
                completionHandler(error)
                return
            }

            let tunFd = self.resolveTunnelFileDescriptor()
            guard tunFd >= 0 else {
                let fdErr = NSError(domain: "RyanDPI.PacketTunnel", code: 1002, userInfo: [
                    NSLocalizedDescriptionKey: "Could not acquire iOS utun file descriptor."
                ])
                RyanDpiLogStore.shared.error(fdErr.localizedDescription)
                self.cleanupEngines()
                completionHandler(fdErr)
                return
            }

            let yaml = self.buildHevYaml(host: socksHost, port: socksPort, user: socksUser, pass: socksPass)
            self.isStopped = false
            self.connectedAt = Date()

            let thread = Thread {
                let bytes = Array(yaml.utf8)
                bytes.withUnsafeBufferPointer { ptr in
                    guard let base = ptr.baseAddress else { return }
                    _ = hev_socks5_tunnel_main_from_str(base, UInt32(bytes.count), tunFd)
                }
            }
            thread.name = "com.ryandpi.ios.hev-tunnel"
            thread.qualityOfService = .userInitiated
            self.tunnelThread = thread
            thread.start()

            RyanDpiLogStore.shared.info("RyanDPI iOS tunnel active (Region=\(settings.region.rawValue), Profile=\(settings.profile.rawValue), utunFd=\(tunFd)).")
            completionHandler(nil)
        }
    }

    override func stopTunnel(with reason: NEProviderStopReason, completionHandler: @escaping () -> Void) {
        RyanDpiLogStore.shared.info("Stopping RyanDPI iOS tunnel (reason=\(reason.rawValue))...")
        cleanupEngines()
        completionHandler()
    }

    override func handleAppMessage(_ messageData: Data, completionHandler: ((Data?) -> Void)?) {
        var txPkt: Int = 0
        var txBytes: Int = 0
        var rxPkt: Int = 0
        var rxBytes: Int = 0
        hev_socks5_tunnel_stats(&txPkt, &txBytes, &rxPkt, &rxBytes)

        let uptime = connectedAt.map { max(0, Int(Date().timeIntervalSince($0))) } ?? 0
        let msg = TrafficStatsMessage(
            txBytes: UInt64(max(0, txBytes)),
            rxBytes: UInt64(max(0, rxBytes)),
            txPackets: UInt64(max(0, txPkt)),
            rxPackets: UInt64(max(0, rxPkt)),
            uptimeSeconds: uptime,
            logs: Array(RyanDpiLogStore.shared.allLines().suffix(40))
        )
        completionHandler?(try? JSONEncoder().encode(msg))
    }

    private func cleanupEngines() {
        stopLock.lock()
        let alreadyStopped = isStopped
        isStopped = true
        stopLock.unlock()
        if alreadyStopped { return }

        hev_socks5_tunnel_quit()
        socksGateway?.stop()
        socksGateway = nil
        CiadpiController.stop()
        connectedAt = nil
    }

    private func resolveTunnelFileDescriptor() -> Int32 {
        if let fd = packetFlow.value(forKeyPath: "socket.fileDescriptor") as? Int32, fd >= 0 {
            return fd
        }
        return ryandpi_find_utun_fd()
    }

    private func buildHevYaml(host: String, port: Int, user: String?, pass: String?) -> String {
        var lines = [
            "tunnel:",
            "  mtu: \(RyanDpiSettings.tunMtu)",
            "socks5:",
            "  port: \(port)",
            "  address: '\(host)'",
            "  udp: 'udp'"
        ]
        if let u = user, !u.isEmpty {
            let eu = u.replacingOccurrences(of: "'", with: "''")
            let ep = (pass ?? "").replacingOccurrences(of: "'", with: "''")
            lines.append("  username: '\(eu)'")
            lines.append("  password: '\(ep)'")
        }
        // Ultra-lean memory settings to stay far below iOS 50 MB NetworkExtension limit
        lines.append(contentsOf: [
            "misc:",
            "  task-stack-size: 20480",
            "  tcp-buffer-size: 65536",
            "  connect-timeout: 5000",
            "  read-write-timeout: 180000",
            "  log-file: stderr",
            "  log-level: error"
        ])
        return lines.joined(separator: "\n") + "\n"
    }
}
