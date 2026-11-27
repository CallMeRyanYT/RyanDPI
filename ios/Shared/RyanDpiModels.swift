import Foundation

public enum RegionPreset: String, Codable, CaseIterable, Identifiable {
    case globalUniversal
    case turkey
    case russia
    case brazil

    public var id: String { rawValue }
}

public enum BypassProfile: String, Codable, CaseIterable, Identifiable {
    case autoUniversal
    case turkeyBtk
    case russiaTspu
    case classicByeDpi
    case strictIsp
    case custom

    public var id: String { rawValue }
}

public enum DohProvider: String, Codable, CaseIterable, Identifiable {
    case cloudflare
    case google
    case quad9
    case adguard

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .cloudflare: return "Cloudflare (1.1.1.1)"
        case .google: return "Google (8.8.8.8)"
        case .quad9: return "Quad9 (9.9.9.9)"
        case .adguard: return "AdGuard (94.140.14.14)"
        }
    }

    public var endpointURL: String {
        switch self {
        case .cloudflare: return "https://1.1.1.1/dns-query"
        case .google: return "https://8.8.8.8/dns-query"
        case .quad9: return "https://9.9.9.9/dns-query"
        case .adguard: return "https://94.140.14.14/dns-query"
        }
    }

    public var bootstrapIPs: [String] {
        switch self {
        case .cloudflare: return ["1.1.1.1", "1.0.0.1"]
        case .google: return ["8.8.8.8", "8.8.4.4"]
        case .quad9: return ["9.9.9.9", "149.112.112.112"]
        case .adguard: return ["94.140.14.14", "94.140.15.15"]
        }
    }
}

public struct RyanDpiSettings: Codable, Equatable {
    public static let appGroupId = "group.com.ryandpi.ios"
    public static let settingsKey = "ryandpi_ios_settings_v3"
    public static let logsKey = "ryandpi_ios_logs_v3"

    public static let gatewayPort: Int = 1080
    public static let ciadpiInternalPort: Int = 1081
    public static let localHttpProxyPort: Int = 8080
    public static let virtualTunIPv4 = "10.10.10.1"
    public static let virtualDnsIPv4 = "10.10.10.2"
    public static let tunMtu = 8500

    // iOS-native ciadpi 17.3 strategies (100% userspace Darwin-compatible — zero root/FAKE_SUPPORT flags!).
    // Dedicated --hosts :googlevideo.com fast-track FIRST (double TLS split 1+s & -5+se inside google|video.com +
    // 3-byte SNI chop) followed by proven Discord/YouTube/Roblox stages!
    public static let autoUniversalStrategy =
        "--hosts :googlevideo.com --tlsrec 1+s --tlsrec -5+se --split 2 --disorder 1+s --split 4:14:3+s " +
        "--auto=torst,redirect,ssl_err --hosts :googlevideo.com --split 2 --disoob 1+s --tlsrec -5+se --split 4:14:3+s " +
        "--auto=none --tlsrec 1+s --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --split 2 --disorder -1+s --tlsrec 1+s --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
        "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err"

    public static let turkeyBtkStrategy =
        "--tlsrec 1+s --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
        "--auto=torst,redirect,ssl_err"

    public static let russiaTspuStrategy =
        "--hosts :googlevideo.com --tlsrec 1+s --tlsrec -5+se --split 2 --disorder 1+s --split 4:14:3+s " +
        "--auto=torst,redirect,ssl_err --hosts :googlevideo.com --split 2 --disoob 1+s --tlsrec -5+se --split 4:14:3+s " +
        "--auto=torst,redirect,ssl_err --hosts :googlevideo.com --oob 1+s --tlsrec -5+se --split 4:14:3+s " +
        "--auto=none --split 2 --disorder -1+s --tlsrec 1+s --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
        "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --tlsrec -5+se --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err"

    public static let classicByeDpiStrategy =
        "--tlsrec 1+s --mod-http=h,d --auto=torst --split 1 --disorder 3+s"

    public static let strictIspStrategy =
        "--split 2 --disorder -1+s --tlsrec -5+se --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec -5+se " +
        "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --tlsrec -5+se --mod-http=h,d " +
        "--auto=torst,redirect,ssl_err"

    public var language: String = Self.detectSystemLanguage()
    public var regionConfigured: Bool = false
    public var region: RegionPreset = .globalUniversal
    public var profile: BypassProfile = .autoUniversal
    public var dohProvider: DohProvider = .cloudflare
    public var customStrategyArgs: String = RyanDpiSettings.autoUniversalStrategy
    public var udpBypass: Bool = true
    public var blockQuicUdp443: Bool = true
    public var permanentAlwaysOn: Bool = false
    public var customSocksEndpoint: String = ""
    public var customSocksUsername: String = ""
    public var customSocksPassword: String = ""

    public init() {}

    public static func sharedDefaults() -> UserDefaults {
        UserDefaults(suiteName: appGroupId) ?? .standard
    }

    public static func load() -> RyanDpiSettings {
        let defs = sharedDefaults()
        if let data = defs.data(forKey: settingsKey),
           let decoded = try? JSONDecoder().decode(RyanDpiSettings.self, from: data) {
            return decoded
        }
        return RyanDpiSettings()
    }

    public func save() {
        if let data = try? JSONEncoder().encode(self) {
            let defs = Self.sharedDefaults()
            defs.set(data, forKey: Self.settingsKey)
            defs.synchronize()
        }
    }

    public mutating func applyRegionPreset(_ selected: RegionPreset) {
        region = selected
        regionConfigured = true
        switch selected {
        case .turkey:
            profile = .turkeyBtk
            udpBypass = true
            blockQuicUdp443 = true
        case .russia:
            profile = .russiaTspu
            udpBypass = true
            blockQuicUdp443 = true
        case .brazil:
            profile = .autoUniversal
            udpBypass = true
            blockQuicUdp443 = false
        case .globalUniversal:
            profile = .autoUniversal
            udpBypass = true
            blockQuicUdp443 = true
        }
    }

    public var activeStrategyString: String {
        switch profile {
        case .autoUniversal: return Self.autoUniversalStrategy
        case .turkeyBtk: return Self.turkeyBtkStrategy
        case .russiaTspu: return Self.russiaTspuStrategy
        case .classicByeDpi: return Self.classicByeDpiStrategy
        case .strictIsp: return Self.strictIspStrategy
        case .custom:
            let trimmed = customStrategyArgs.trimmingCharacters(in: .whitespacesAndNewlines)
            return trimmed.isEmpty ? Self.autoUniversalStrategy : trimmed
        }
    }

    public func buildCiadpiArgs(listenPort: Int = RyanDpiSettings.ciadpiInternalPort) -> [String] {
        // Ultra-low-RAM iOS NetworkExtension footprint (-c 256 -b 16384 keeps RSS ~15 MB, far below Apple's 50 MB limit)
        var args = [
            "ciadpi",
            "-i", "127.0.0.1",
            "-p", String(listenPort),
            "-c", "256",
            "-b", "16384",
            "-u", "3600",
            "-T", "2",
            "-L", "3"
        ]
        if udpBypass {
            args.append(contentsOf: ["-a", "3"])
        }
        let tokens = activeStrategyString
            .replacingOccurrences(of: "\r", with: " ")
            .replacingOccurrences(of: "\n", with: " ")
            .split(whereSeparator: { $0.isWhitespace })
            .map(String.init)
        args.append(contentsOf: tokens)
        return args
    }

    public var orderedDohEndpoints: [String] {
        var list = [dohProvider.endpointURL]
        for p in DohProvider.allCases where p != dohProvider {
            list.append(p.endpointURL)
        }
        return list
    }

    public func parsedCustomSocksEndpoint() -> (host: String, port: Int)? {
        let cleaned = customSocksEndpoint
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "socks5://", with: "", options: .caseInsensitive)
            .replacingOccurrences(of: "socks5h://", with: "", options: .caseInsensitive)
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        guard let idx = cleaned.lastIndex(of: ":") else { return nil }
        let host = String(cleaned[..<idx]).trimmingCharacters(in: CharacterSet(charactersIn: " []"))
        let portStr = String(cleaned[cleaned.index(after: idx)...]).trimmingCharacters(in: .whitespaces)
        guard !host.isEmpty, let port = Int(portStr), (1...65535).contains(port) else { return nil }
        return (host, port)
    }

    public static func detectSystemLanguage() -> String {
        let code = (Locale.preferredLanguages.first ?? "en").lowercased()
        if code.hasPrefix("tr") { return "tr" }
        if code.hasPrefix("pt") { return "pt" }
        if code.hasPrefix("ru") { return "ru" }
        if code.hasPrefix("fa") || code.hasPrefix("pr") { return "fa" }
        if code.hasPrefix("id") { return "id" }
        if code.hasPrefix("zh") { return "zh" }
        if code.hasPrefix("ar") { return "ar" }
        return "en"
    }
}

public struct TrafficStatsMessage: Codable {
    public let txBytes: UInt64
    public let rxBytes: UInt64
    public let txPackets: UInt64
    public let rxPackets: UInt64
    public let uptimeSeconds: Int
    public let logs: [String]

    public init(txBytes: UInt64, rxBytes: UInt64, txPackets: UInt64, rxPackets: UInt64, uptimeSeconds: Int, logs: [String]) {
        self.txBytes = txBytes
        self.rxBytes = rxBytes
        self.txPackets = txPackets
        self.rxPackets = rxPackets
        self.uptimeSeconds = uptimeSeconds
        self.logs = logs
    }
}

public final class RyanDpiLogStore {
    public static let shared = RyanDpiLogStore()
    private let lock = NSLock()
    private var memoryLines: [String] = []
    private let maxLines = 180
    private let formatter: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "HH:mm:ss"
        return f
    }()

    public func log(_ level: String, _ message: String) {
        let stamp = formatter.string(from: Date())
        let line = "[\(stamp)] [\(level)] \(message)"
        lock.lock()
        memoryLines.append(line)
        if memoryLines.count > maxLines {
            memoryLines.removeFirst(memoryLines.count - maxLines)
        }
        let snapshot = memoryLines
        lock.unlock()
        let defs = RyanDpiSettings.sharedDefaults()
        defs.set(snapshot, forKey: RyanDpiSettings.logsKey)
    }

    public func info(_ message: String) { log("INFO", message) }
    public func warn(_ message: String) { log("WARN", message) }
    public func error(_ message: String) { log("ERR ", message) }

    public func allLines() -> [String] {
        if let saved = RyanDpiSettings.sharedDefaults().stringArray(forKey: RyanDpiSettings.logsKey), !saved.isEmpty {
            return saved
        }
        lock.lock()
        defer { lock.unlock() }
        return memoryLines
    }

    public func clear() {
        lock.lock()
        memoryLines.removeAll()
        lock.unlock()
        RyanDpiSettings.sharedDefaults().removeObject(forKey: RyanDpiSettings.logsKey)
    }
}
