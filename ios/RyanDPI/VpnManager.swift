import Foundation
import Combine
import UIKit
#if !RYANDPI_FREE_EDITION
import NetworkExtension
#endif

enum ConnectionState: Equatable {
    case disconnected
    case connecting
    case connected
    case error(String)
}

struct SelfTestResultItem: Identifiable {
    let id: String
    let name: String
    let passed: Bool
    let detail: String
    let latencyMs: Int
}

@MainActor
final class RyanDpiController: ObservableObject {
    @Published var settings: RyanDpiSettings
    @Published var state: ConnectionState = .disconnected
    @Published var txBytes: UInt64 = 0
    @Published var rxBytes: UInt64 = 0
    @Published var uptimeSeconds: Int = 0
    @Published var logs: [String] = []
    @Published var selfTestResults: [SelfTestResultItem] = []
    @Published var isTesting: Bool = false

    #if RYANDPI_FREE_EDITION
    let isFreeEdition: Bool = true
    private var socksGateway: RyanDpiSocksGateway?
    private var httpProxy: LocalHttpProxyServer?
    private var bgTaskId: UIBackgroundTaskIdentifier = .invalid
    private var startedAt: Date?
    #else
    let isFreeEdition: Bool = false
    private var tunnelManager: NETunnelProviderManager?
    private var statusObserver: NSObjectProtocol?
    #endif

    private var statsTimer: Timer?

    init() {
        self.settings = RyanDpiSettings.load()
        self.logs = RyanDpiLogStore.shared.allLines()
        if self.logs.isEmpty {
            RyanDpiLogStore.shared.info("RyanDPI v3.2.0 Unbound iOS ready (\(isFreeEdition ? "Free Apple ID Proxy" : "System VPN")).")
            self.logs = RyanDpiLogStore.shared.allLines()
        }
        #if !RYANDPI_FREE_EDITION
        loadTunnelManager()
        #endif
    }

    deinit {
        statsTimer?.invalidate()
        #if !RYANDPI_FREE_EDITION
        if let obs = statusObserver {
            NotificationCenter.default.removeObserver(obs)
        }
        #endif
    }

    func saveSettings() {
        settings.save()
    }

    func selectRegion(_ region: RegionPreset) {
        settings.applyRegionPreset(region)
        settings.save()
        RyanDpiLogStore.shared.info("Region preset set to \(region.rawValue) (profile=\(settings.profile.rawValue)).")
        refreshLogs()
    }

    func toggleConnection() {
        switch state {
        case .connected, .connecting:
            disconnect()
        case .disconnected, .error:
            connect()
        }
    }

    func connect() {
        settings.save()
        #if RYANDPI_FREE_EDITION
        startFreeLocalProxy()
        #else
        startSystemVpn()
        #endif
    }

    func disconnect() {
        #if RYANDPI_FREE_EDITION
        stopFreeLocalProxy()
        #else
        stopSystemVpn()
        #endif
    }

    func clearLogs() {
        RyanDpiLogStore.shared.clear()
        logs = []
    }

    func refreshLogs() {
        logs = RyanDpiLogStore.shared.allLines()
    }

    func createMobileConfigFileURL() -> URL? {
        let xml = MobileConfigGenerator.generateDohProfileXml(provider: settings.dohProvider)
        let fileName = "RyanDPI-iOS-EncryptedDNS-\(settings.dohProvider.rawValue).mobileconfig"
        let url = FileManager.default.temporaryDirectory.appendingPathComponent(fileName)
        do {
            try xml.write(to: url, atomically: true, encoding: .utf8)
            return url
        } catch {
            RyanDpiLogStore.shared.error("Failed to generate .mobileconfig: \(error.localizedDescription)")
            refreshLogs()
            return nil
        }
    }

    // MARK: - Live Self-Test

    func runSelfTest() {
        guard !isTesting else { return }
        isTesting = true
        selfTestResults = []

        let activeSettings = self.settings
        let isConnected: Bool = (state == .connected)
        let strings = AppStrings.forLanguage(activeSettings.language)

        Task.detached(priority: .userInitiated) { [weak self] in
            let cfg = URLSessionConfiguration.ephemeral
            cfg.timeoutIntervalForRequest = 6.0
            cfg.timeoutIntervalForResource = 7.0
            cfg.requestCachePolicy = .reloadIgnoringLocalCacheData
            #if RYANDPI_FREE_EDITION
            if isConnected {
                cfg.connectionProxyDictionary = [
                    kCFNetworkProxiesHTTPEnable as String: true,
                    kCFNetworkProxiesHTTPProxy as String: "127.0.0.1",
                    kCFNetworkProxiesHTTPPort as String: RyanDpiSettings.localHttpProxyPort,
                    kCFNetworkProxiesHTTPSEnable as String: true,
                    kCFNetworkProxiesHTTPSProxy as String: "127.0.0.1",
                    kCFNetworkProxiesHTTPSPort as String: RyanDpiSettings.localHttpProxyPort
                ]
            }
            #endif
            let session = URLSession(configuration: cfg)
            defer { session.invalidateAndCancel() }

            let targets: [(String, String, String)] = {
                switch activeSettings.region {
                case .russia:
                    return [
                        ("discord", "Discord API", "https://discord.com/api/v10/gateway"),
                        ("youtube", "YouTube Web", "https://www.youtube.com/generate_204"),
                        ("yt_ggc", "YouTube Video (GGC)", "https://redirector.googlevideo.com/report_mapping"),
                        ("instagram", "Instagram", "https://www.instagram.com/"),
                        ("roblox", "Roblox", "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer"),
                        ("baseline", "Cloudflare Baseline", "https://1.1.1.1/cdn-cgi/trace")
                    ]
                case .brazil:
                    return [
                        ("discord", "Discord API", "https://discord.com/api/v10/gateway"),
                        ("discord_cdn", "Discord Media CDN", "https://cdn.discordapp.com/"),
                        ("baseline", "Cloudflare Trace", "https://1.1.1.1/cdn-cgi/trace")
                    ]
                default:
                    return [
                        ("discord", "Discord API", "https://discord.com/api/v10/gateway"),
                        ("roblox", "Roblox API", "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer"),
                        ("youtube", "YouTube Video (GGC)", "https://redirector.googlevideo.com/report_mapping"),
                        ("wattpad", "Wattpad", "https://www.wattpad.com/"),
                        ("baseline", "Cloudflare Baseline", "https://1.1.1.1/cdn-cgi/trace")
                    ]
                }
            }()

            var out: [SelfTestResultItem] = []
            for (id, name, urlStr) in targets {
                let start = Date()
                var passed = false
                var detail = strings.blocked
                if let url = URL(string: urlStr) {
                    var req = URLRequest(url: url, timeoutInterval: 6.0)
                    req.httpMethod = "GET"
                    req.setValue("RyanDPI-iOS/3.2.0", forHTTPHeaderField: "User-Agent")
                    do {
                        let (_, resp) = try await session.data(for: req)
                        if let http = resp as? HTTPURLResponse, (200...499).contains(http.statusCode) {
                            passed = true
                            detail = "\(strings.unlocked) (HTTP \(http.statusCode))"
                        } else if let http = resp as? HTTPURLResponse {
                            detail = "\(strings.blocked) (HTTP \(http.statusCode))"
                        }
                    } catch {
                        detail = "\(strings.blocked) (\(error.localizedDescription.prefix(38)))"
                    }
                }
                let ms = max(1, Int(Date().timeIntervalSince(start) * 1000.0))
                out.append(SelfTestResultItem(id: id, name: name, passed: passed, detail: detail, latencyMs: ms))
            }

            await MainActor.run {
                self?.selfTestResults = out
                self?.isTesting = false
                self?.refreshLogs()
            }
        }
    }

    // MARK: - Free Apple ID Local Proxy Mode

    #if RYANDPI_FREE_EDITION
    private func startFreeLocalProxy() {
        state = .connecting
        let cfg = self.settings
        Task.detached(priority: .userInitiated) { [weak self] in
            let ciadpiPort = RyanDpiSettings.ciadpiInternalPort
            let args = cfg.buildCiadpiArgs(listenPort: ciadpiPort)
            let rc = CiadpiController.start(args: args)
            guard rc == 0, CiadpiController.waitForLocalPort(ciadpiPort, timeoutMs: 3500) else {
                CiadpiController.stop()
                await MainActor.run {
                    let msg = "ciadpi failed to bind 127.0.0.1:\(ciadpiPort) (rc=\(rc))."
                    RyanDpiLogStore.shared.error(msg)
                    self?.state = .error(msg)
                    self?.refreshLogs()
                }
                return
            }

            let resolver = DohDnsResolver(endpoints: cfg.orderedDohEndpoints)
            let gw = RyanDpiSocksGateway(
                listenPort: RyanDpiSettings.gatewayPort,
                upstreamCiadpiPort: ciadpiPort,
                dohResolver: resolver,
                blockQuicUdp443: cfg.blockQuicUdp443
            )
            let http = LocalHttpProxyServer(
                listenPort: RyanDpiSettings.localHttpProxyPort,
                socksPort: RyanDpiSettings.gatewayPort
            )
            do {
                try gw.start()
                try http.start()
                await MainActor.run {
                    guard let self = self else { return }
                    self.socksGateway = gw
                    self.httpProxy = http
                    self.startedAt = Date()
                    self.state = .connected
                    self.bgTaskId = UIApplication.shared.beginBackgroundTask(withName: "RyanDPIProxy") { [weak self] in
                        Task { @MainActor in
                            if let id = self?.bgTaskId, id != .invalid {
                                UIApplication.shared.endBackgroundTask(id)
                                self?.bgTaskId = .invalid
                            }
                        }
                    }
                    self.startStatsTimer()
                    self.refreshLogs()
                }
            } catch {
                gw.stop()
                http.stop()
                CiadpiController.stop()
                await MainActor.run {
                    RyanDpiLogStore.shared.error("Local proxy start failed: \(error.localizedDescription)")
                    self?.state = .error(error.localizedDescription)
                    self?.refreshLogs()
                }
            }
        }
    }

    private func stopFreeLocalProxy() {
        statsTimer?.invalidate()
        statsTimer = nil
        httpProxy?.stop()
        httpProxy = nil
        socksGateway?.stop()
        socksGateway = nil
        CiadpiController.stop()
        startedAt = nil
        uptimeSeconds = 0
        if bgTaskId != .invalid {
            UIApplication.shared.endBackgroundTask(bgTaskId)
            bgTaskId = .invalid
        }
        state = .disconnected
        RyanDpiLogStore.shared.info("RyanDPI local proxy stopped cleanly.")
        refreshLogs()
    }

    private func startStatsTimer() {
        statsTimer?.invalidate()
        statsTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self = self, let s = self.startedAt else { return }
                self.uptimeSeconds = max(0, Int(Date().timeIntervalSince(s)))
                self.refreshLogs()
            }
        }
    }
    #endif

    // MARK: - Full System-Wide NetworkExtension VPN Mode

    #if !RYANDPI_FREE_EDITION
    private func loadTunnelManager() {
        NETunnelProviderManager.loadAllFromPreferences { [weak self] managers, error in
            Task { @MainActor in
                guard let self = self else { return }
                if let error = error {
                    RyanDpiLogStore.shared.warn("VPN profile load note: \(error.localizedDescription)")
                    self.refreshLogs()
                    return
                }
                if let existing = managers?.first {
                    self.tunnelManager = existing
                    self.syncStatus(existing.connection.status)
                    self.observeStatus(for: existing)
                }
            }
        }
    }

    private func observeStatus(for manager: NETunnelProviderManager) {
        if let obs = statusObserver {
            NotificationCenter.default.removeObserver(obs)
        }
        statusObserver = NotificationCenter.default.addObserver(
            forName: .NEVPNStatusDidChange,
            object: manager.connection,
            queue: .main
        ) { [weak self] note in
            guard let conn = note.object as? NEVPNConnection else { return }
            Task { @MainActor in
                self?.syncStatus(conn.status)
            }
        }
    }

    private func syncStatus(_ status: NEVPNStatus) {
        switch status {
        case .connected:
            state = .connected
            startStatsTimer()
        case .connecting, .reasserting:
            state = .connecting
        case .disconnecting, .disconnected, .invalid:
            statsTimer?.invalidate()
            statsTimer = nil
            uptimeSeconds = 0
            state = .disconnected
        @unknown default:
            state = .disconnected
        }
        refreshLogs()
    }

    private func startSystemVpn() {
        state = .connecting
        let settingsData = try? JSONEncoder().encode(settings)
        let settingsJson = settingsData.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"

        let manager = tunnelManager ?? NETunnelProviderManager()
        let proto = NETunnelProviderProtocol()
        let mainId = Bundle.main.bundleIdentifier ?? "com.ryandpi.ios"
        proto.providerBundleIdentifier = "\(mainId).PacketTunnel"
        proto.serverAddress = "RyanDPI Unbound (127.0.0.1)"
        proto.providerConfiguration = ["settingsJson": settingsJson]

        manager.protocolConfiguration = proto
        manager.localizedDescription = "RyanDPI — Unbound"
        manager.isEnabled = true

        manager.saveToPreferences { [weak self] saveError in
            Task { @MainActor in
                guard let self = self else { return }
                if let saveError = saveError {
                    let msg = "VPN profile permission denied or unavailable: \(saveError.localizedDescription)"
                    RyanDpiLogStore.shared.error(msg)
                    self.state = .error(msg)
                    self.refreshLogs()
                    return
                }
                manager.loadFromPreferences { loadError in
                    Task { @MainActor in
                        if let loadError = loadError {
                            RyanDpiLogStore.shared.error(loadError.localizedDescription)
                            self.state = .error(loadError.localizedDescription)
                            self.refreshLogs()
                            return
                        }
                        self.tunnelManager = manager
                        self.observeStatus(for: manager)
                        do {
                            try manager.connection.startVPNTunnel()
                            RyanDpiLogStore.shared.info("Requested iOS PacketTunnel start...")
                            self.refreshLogs()
                        } catch {
                            RyanDpiLogStore.shared.error("startVPNTunnel failed: \(error.localizedDescription)")
                            self.state = .error(error.localizedDescription)
                            self.refreshLogs()
                        }
                    }
                }
            }
        }
    }

    private func stopSystemVpn() {
        tunnelManager?.connection.stopVPNTunnel()
        statsTimer?.invalidate()
        statsTimer = nil
        state = .disconnected
        RyanDpiLogStore.shared.info("Disconnected RyanDPI iOS VPN.")
        refreshLogs()
    }

    private func startStatsTimer() {
        statsTimer?.invalidate()
        statsTimer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.pollExtensionStats()
            }
        }
    }

    private func pollExtensionStats() {
        guard let session = tunnelManager?.connection as? NETunnelProviderSession else { return }
        let ping = Data("stats".utf8)
        try? session.sendProviderMessage(ping) { [weak self] response in
            guard let data = response,
                  let msg = try? JSONDecoder().decode(TrafficStatsMessage.self, from: data) else { return }
            Task { @MainActor in
                guard let self = self else { return }
                self.txBytes = msg.txBytes
                self.rxBytes = msg.rxBytes
                self.uptimeSeconds = msg.uptimeSeconds
                if !msg.logs.isEmpty {
                    self.logs = msg.logs
                }
            }
        }
    }
    #endif
}
