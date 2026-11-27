import SwiftUI
import UIKit

struct ContentView: View {
    @EnvironmentObject var controller: RyanDpiController
    @State private var selectedTab: Int = 0
    @State private var copiedBanner: String? = nil
    @State private var shareFileURL: URL? = nil
    @State private var showingShareSheet: Bool = false

    private var s: AppStrings {
        AppStrings.forLanguage(controller.settings.language)
    }

    private let bgDark = Color(red: 0.043, green: 0.059, blue: 0.102)
    private let cardDark = Color(red: 0.082, green: 0.106, blue: 0.173)
    private let accentCyan = Color(red: 0.18, green: 0.83, blue: 0.96)
    private let accentIndigo = Color(red: 0.39, green: 0.40, blue: 0.95)
    private let accentGreen = Color(red: 0.16, green: 0.84, blue: 0.56)

    var body: some View {
        TabView(selection: $selectedTab) {
            connectTab
                .tabItem { Label(s.tabConnect, systemImage: "shield.lefthalf.filled") }
                .tag(0)

            selfTestTab
                .tabItem { Label(s.tabTest, systemImage: "checkmark.seal.fill") }
                .tag(1)

            settingsTab
                .tabItem { Label(s.tabSettings, systemImage: "slider.horizontal.3") }
                .tag(2)

            logsTab
                .tabItem { Label(s.tabLogs, systemImage: "terminal.fill") }
                .tag(3)
        }
        .accentColor(accentCyan)
        .sheet(isPresented: $showingShareSheet) {
            if let url = shareFileURL {
                ActivityShareSheet(items: [url])
            }
        }
    }

    // MARK: - Tab 1: Connect Dashboard

    private var connectTab: some View {
        NavigationView {
            ZStack {
                bgDark.ignoresSafeArea()
                ScrollView {
                    VStack(spacing: 18) {
                        headerCard
                        powerButtonSection
                        if controller.isFreeEdition {
                            freeProxySetupCard
                        }
                        statsStrip
                        regionPickerCard
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 12)
                }
            }
            .navigationBarHidden(true)
        }
        .navigationViewStyle(StackNavigationViewStyle())
    }

    private var headerCard: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 8) {
                    Text("RyanDPI")
                        .font(.system(size: 26, weight: .heavy, design: .rounded))
                        .foregroundColor(.white)
                    Text("v3.2.0")
                        .font(.caption.bold())
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(accentIndigo.opacity(0.28))
                        .foregroundColor(accentCyan)
                        .clipShape(Capsule())
                }
                Text(s.subtitle)
                    .font(.caption)
                    .foregroundColor(.gray)
            }
            Spacer()
            Button {
                let langs = AppStrings.supportedLanguages
                let idx = langs.firstIndex(of: controller.settings.language) ?? 0
                controller.settings.language = langs[(idx + 1) % langs.count]
                controller.saveSettings()
            } label: {
                Text(controller.settings.language.uppercased())
                    .font(.caption.bold())
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(cardDark)
                    .foregroundColor(.white)
                    .clipShape(Capsule())
                    .overlay(Capsule().stroke(Color.white.opacity(0.14), lineWidth: 1))
            }
        }
    }

    private var powerButtonSection: some View {
        VStack(spacing: 14) {
            Text(controller.isFreeEdition ? s.editionFreeProxy : s.editionFullVpn)
                .font(.caption.bold())
                .padding(.horizontal, 12)
                .padding(.vertical, 5)
                .background(cardDark)
                .foregroundColor(accentCyan)
                .clipShape(Capsule())
                .overlay(Capsule().stroke(accentCyan.opacity(0.35), lineWidth: 1))

            Button {
                controller.toggleConnection()
            } label: {
                ZStack {
                    Circle()
                        .fill(orbGradient)
                        .frame(width: 158, height: 158)
                        .shadow(color: orbGlowColor.opacity(0.5), radius: 24, x: 0, y: 0)

                    Circle()
                        .stroke(Color.white.opacity(0.25), lineWidth: 2)
                        .frame(width: 146, height: 146)

                    VStack(spacing: 8) {
                        Image(systemName: controller.state == .connected ? "bolt.shield.fill" : "power")
                            .font(.system(size: 46, weight: .bold))
                            .foregroundColor(.white)
                        Text(statusLabel)
                            .font(.caption.bold())
                            .foregroundColor(.white.opacity(0.95))
                    }
                }
            }
            .buttonStyle(.plain)
            .padding(.vertical, 6)

            Text(controller.state == .connected ? s.tapToDisconnect : s.tapToConnect)
                .font(.subheadline)
                .foregroundColor(.gray)

            if case .error(let msg) = controller.state {
                Text(msg)
                    .font(.caption)
                    .foregroundColor(.red)
                    .multilineTextAlignment(.center)
                    .padding(10)
                    .frame(maxWidth: .infinity)
                    .background(Color.red.opacity(0.14))
                    .cornerRadius(10)
            }
        }
        .padding(18)
        .frame(maxWidth: .infinity)
        .background(cardDark)
        .cornerRadius(20)
        .overlay(RoundedRectangle(cornerRadius: 20).stroke(Color.white.opacity(0.08), lineWidth: 1))
    }

    private var freeProxySetupCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "wifi")
                    .foregroundColor(accentCyan)
                Text(s.freeProxyGuideTitle)
                    .font(.subheadline.bold())
                    .foregroundColor(.white)
                Spacer()
                if let banner = copiedBanner {
                    Text(banner)
                        .font(.caption2.bold())
                        .foregroundColor(accentGreen)
                }
            }
            Text(s.freeProxyGuideBody)
                .font(.caption)
                .foregroundColor(.white.opacity(0.82))
            HStack(spacing: 10) {
                Button {
                    UIPasteboard.general.string = "127.0.0.1"
                    showCopied("127.0.0.1 \(s.copied)")
                } label: {
                    Text("Copy 127.0.0.1")
                        .font(.caption.bold())
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 8)
                        .background(accentIndigo.opacity(0.35))
                        .foregroundColor(.white)
                        .cornerRadius(10)
                }
                Button {
                    UIPasteboard.general.string = "\(RyanDpiSettings.localHttpProxyPort)"
                    showCopied("8080 \(s.copied)")
                } label: {
                    Text("Copy Port 8080")
                        .font(.caption.bold())
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 8)
                        .background(accentCyan.opacity(0.25))
                        .foregroundColor(.white)
                        .cornerRadius(10)
                }
            }
        }
        .padding(14)
        .background(cardDark)
        .cornerRadius(16)
        .overlay(RoundedRectangle(cornerRadius: 16).stroke(accentCyan.opacity(0.25), lineWidth: 1))
    }

    private var statsStrip: some View {
        HStack(spacing: 10) {
            statBox(title: "DOWN", value: formatBytes(controller.rxBytes), color: accentCyan)
            statBox(title: "UP", value: formatBytes(controller.txBytes), color: accentIndigo)
            statBox(title: "UPTIME", value: formatDuration(controller.uptimeSeconds), color: accentGreen)
        }
    }

    private func statBox(title: String, value: String, color: Color) -> some View {
        VStack(spacing: 4) {
            Text(title)
                .font(.caption2.bold())
                .foregroundColor(.gray)
            Text(value)
                .font(.subheadline.bold().monospacedDigit())
                .foregroundColor(color)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .background(cardDark)
        .cornerRadius(14)
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.07), lineWidth: 1))
    }

    private var regionPickerCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(s.regionTitle)
                .font(.subheadline.bold())
                .foregroundColor(.white)
            Text(s.regionHint)
                .font(.caption)
                .foregroundColor(.gray)

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                regionButton(.globalUniversal, title: s.regionGlobal, icon: "globe")
                regionButton(.turkey, title: s.regionTurkey, icon: "shield.fill")
                regionButton(.russia, title: s.regionRussia, icon: "play.rectangle.fill")
                regionButton(.brazil, title: s.regionBrazil, icon: "network")
            }
        }
        .padding(14)
        .background(cardDark)
        .cornerRadius(16)
        .overlay(RoundedRectangle(cornerRadius: 16).stroke(Color.white.opacity(0.08), lineWidth: 1))
    }

    private func regionButton(_ region: RegionPreset, title: String, icon: String) -> some View {
        let selected = controller.settings.region == region
        return Button {
            controller.selectRegion(region)
        } label: {
            HStack(spacing: 6) {
                Image(systemName: icon)
                    .font(.caption)
                Text(title)
                    .font(.caption.bold())
                    .lineLimit(1)
                    .minimumScaleFactor(0.75)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 11)
            .padding(.horizontal, 8)
            .background(selected ? accentIndigo.opacity(0.45) : bgDark.opacity(0.7))
            .foregroundColor(selected ? .white : .gray)
            .cornerRadius(12)
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(selected ? accentCyan : Color.white.opacity(0.08), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Tab 2: Live Self-Test

    private var selfTestTab: some View {
        NavigationView {
            ZStack {
                bgDark.ignoresSafeArea()
                VStack(spacing: 14) {
                    Button {
                        controller.runSelfTest()
                    } label: {
                        HStack {
                            if controller.isTesting {
                                ProgressView().tint(.white)
                            } else {
                                Image(systemName: "play.circle.fill")
                            }
                            Text(controller.isTesting ? s.testingNow : s.runSelfTest)
                                .font(.headline)
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(LinearGradient(colors: [accentIndigo, accentCyan], startPoint: .leading, endPoint: .trailing))
                        .foregroundColor(.white)
                        .cornerRadius(14)
                    }
                    .disabled(controller.isTesting)
                    .padding(.horizontal, 16)
                    .padding(.top, 12)

                    ScrollView {
                        VStack(spacing: 10) {
                            ForEach(controller.selfTestResults) { item in
                                HStack {
                                    Image(systemName: item.passed ? "checkmark.circle.fill" : "xmark.octagon.fill")
                                        .foregroundColor(item.passed ? accentGreen : .red)
                                        .font(.title3)
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(item.name)
                                            .font(.subheadline.bold())
                                            .foregroundColor(.white)
                                        Text(item.detail)
                                            .font(.caption)
                                            .foregroundColor(.gray)
                                    }
                                    Spacer()
                                    Text("\(item.latencyMs) ms")
                                        .font(.caption.bold().monospacedDigit())
                                        .foregroundColor(accentCyan)
                                }
                                .padding(12)
                                .background(cardDark)
                                .cornerRadius(12)
                            }
                        }
                        .padding(.horizontal, 16)
                    }
                }
            }
            .navigationTitle(s.tabTest)
        }
        .navigationViewStyle(StackNavigationViewStyle())
    }

    // MARK: - Tab 3: Settings & .mobileconfig Export

    private var settingsTab: some View {
        NavigationView {
            Form {
                Section(header: Text(s.dohTitle)) {
                    Picker(s.dohTitle, selection: $controller.settings.dohProvider) {
                        ForEach(DohProvider.allCases) { p in
                            Text(p.displayName).tag(p)
                        }
                    }
                    .onChange(of: controller.settings.dohProvider) { _ in controller.saveSettings() }

                    Button {
                        if let url = controller.createMobileConfigFileURL() {
                            shareFileURL = url
                            showingShareSheet = true
                        }
                    } label: {
                        Label(s.exportDnsProfile, systemImage: "square.and.arrow.up")
                            .foregroundColor(accentCyan)
                    }
                    Text(s.exportDnsHint)
                        .font(.caption2)
                        .foregroundColor(.gray)
                }

                Section(header: Text(s.profileTitle)) {
                    Picker(s.profileTitle, selection: $controller.settings.profile) {
                        Text("Auto Universal").tag(BypassProfile.autoUniversal)
                        Text("Turkey BTK").tag(BypassProfile.turkeyBtk)
                        Text("Russia TSPU + YouTube").tag(BypassProfile.russiaTspu)
                        Text("Classic ByeDPI").tag(BypassProfile.classicByeDpi)
                        Text("Strict ISP").tag(BypassProfile.strictIsp)
                        Text("Custom").tag(BypassProfile.custom)
                    }
                    .onChange(of: controller.settings.profile) { _ in controller.saveSettings() }

                    Toggle(s.quicToggle, isOn: $controller.settings.blockQuicUdp443)
                        .onChange(of: controller.settings.blockQuicUdp443) { _ in controller.saveSettings() }

                    Toggle(s.udpToggle, isOn: $controller.settings.udpBypass)
                        .onChange(of: controller.settings.udpBypass) { _ in controller.saveSettings() }

                    if controller.settings.profile == .custom {
                        TextField(s.customArgsLabel, text: $controller.settings.customStrategyArgs)
                            .font(.caption.monospaced())
                            .autocapitalization(.none)
                            .disableAutocorrection(true)
                            .onChange(of: controller.settings.customStrategyArgs) { _ in controller.saveSettings() }
                    }
                }

                if controller.settings.region == .brazil {
                    Section(header: Text(s.brazilSocksLabel)) {
                        TextField("1.2.3.4:1080", text: $controller.settings.customSocksEndpoint)
                            .autocapitalization(.none)
                            .disableAutocorrection(true)
                            .onChange(of: controller.settings.customSocksEndpoint) { _ in controller.saveSettings() }
                        TextField("Username (optional)", text: $controller.settings.customSocksUsername)
                            .autocapitalization(.none)
                            .disableAutocorrection(true)
                            .onChange(of: controller.settings.customSocksUsername) { _ in controller.saveSettings() }
                        SecureField("Password (optional)", text: $controller.settings.customSocksPassword)
                            .onChange(of: controller.settings.customSocksPassword) { _ in controller.saveSettings() }
                    }
                }
            }
            .navigationTitle(s.tabSettings)
        }
        .navigationViewStyle(StackNavigationViewStyle())
    }

    // MARK: - Tab 4: Real-Time Logs

    private var logsTab: some View {
        NavigationView {
            ZStack {
                bgDark.ignoresSafeArea()
                VStack(spacing: 10) {
                    HStack {
                        Button {
                            UIPasteboard.general.string = controller.logs.joined(separator: "\n")
                            showCopied(s.copied)
                        } label: {
                            Label(s.copyLogs, systemImage: "doc.on.doc")
                                .font(.caption.bold())
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .background(cardDark)
                                .foregroundColor(accentCyan)
                                .cornerRadius(10)
                        }

                        Button {
                            controller.clearLogs()
                        } label: {
                            Label(s.clearLogs, systemImage: "trash")
                                .font(.caption.bold())
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .background(cardDark)
                                .foregroundColor(.red)
                                .cornerRadius(10)
                        }

                        Spacer()
                        if let banner = copiedBanner {
                            Text(banner)
                                .font(.caption.bold())
                                .foregroundColor(accentGreen)
                        }
                    }
                    .padding(.horizontal, 16)
                    .padding(.top, 8)

                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: 4) {
                            ForEach(Array(controller.logs.enumerated()), id: \.offset) { _, line in
                                Text(line)
                                    .font(.system(size: 11, design: .monospaced))
                                    .foregroundColor(lineColor(line))
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                        .padding(12)
                    }
                    .background(cardDark)
                    .cornerRadius(12)
                    .padding(.horizontal, 16)
                    .padding(.bottom, 10)
                }
            }
            .navigationTitle(s.tabLogs)
        }
        .navigationViewStyle(StackNavigationViewStyle())
    }

    // MARK: - Helpers

    private var orbGradient: LinearGradient {
        switch controller.state {
        case .connected:
            return LinearGradient(colors: [accentGreen, accentCyan], startPoint: .topLeading, endPoint: .bottomTrailing)
        case .connecting:
            return LinearGradient(colors: [.orange, accentIndigo], startPoint: .topLeading, endPoint: .bottomTrailing)
        case .error:
            return LinearGradient(colors: [.red, .orange], startPoint: .topLeading, endPoint: .bottomTrailing)
        case .disconnected:
            return LinearGradient(colors: [accentIndigo, Color(red: 0.16, green: 0.24, blue: 0.45)], startPoint: .topLeading, endPoint: .bottomTrailing)
        }
    }

    private var orbGlowColor: Color {
        switch controller.state {
        case .connected: return accentGreen
        case .connecting: return .orange
        case .error: return .red
        case .disconnected: return accentIndigo
        }
    }

    private var statusLabel: String {
        switch controller.state {
        case .connected: return s.statusConnected
        case .connecting: return s.statusConnecting
        case .disconnected: return s.statusDisconnected
        case .error: return s.statusError
        }
    }

    private func lineColor(_ line: String) -> Color {
        if line.contains("[ERR") { return .red }
        if line.contains("[WARN]") { return .orange }
        return Color.white.opacity(0.85)
    }

    private func showCopied(_ text: String) {
        copiedBanner = text
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) {
            copiedBanner = nil
        }
    }

    private func formatBytes(_ bytes: UInt64) -> String {
        if bytes < 1024 { return "\(bytes) B" }
        let kb = Double(bytes) / 1024.0
        if kb < 1024.0 { return String(format: "%.0f KB", kb) }
        let mb = kb / 1024.0
        if mb < 1024.0 { return String(format: "%.1f MB", mb) }
        return String(format: "%.2f GB", mb / 1024.0)
    }

    private func formatDuration(_ sec: Int) -> String {
        let h = sec / 3600
        let m = (sec % 3600) / 60
        let s = sec % 60
        return h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%02d:%02d", m, s)
    }
}

struct ActivityShareSheet: UIViewControllerRepresentable {
    let items: [Any]
    func makeUIViewController(context: Context) -> UIActivityViewController {
        UIActivityViewController(activityItems: items, applicationActivities: nil)
    }
    func updateUIViewController(_ uiViewController: UIActivityViewController, context: Context) {}
}
