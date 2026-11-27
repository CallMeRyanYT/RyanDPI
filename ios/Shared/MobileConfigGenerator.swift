import Foundation

/// Generates Apple-native iOS Encrypted DNS (DoH) .mobileconfig Configuration Profiles.
/// Works on EVERY iPhone & iPad (iOS 14+) with ZERO sideloading, ZERO jailbreak, and ZERO VPN slot!
public enum MobileConfigGenerator {
    public static func generateDohProfileXml(provider: DohProvider) -> String {
        let ipsXml = provider.bootstrapIPs.map { "                <string>\($0)</string>" }.joined(separator: "\n")
        return """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>PayloadContent</key>
    <array>
        <dict>
            <key>DNSSettings</key>
            <dict>
                <key>DNSProtocol</key>
                <string>HTTPS</string>
                <key>ServerAddresses</key>
                <array>
\(ipsXml)
                </array>
                <key>ServerURL</key>
                <string>\(provider.endpointURL)</string>
            </dict>
            <key>PayloadDescription</key>
            <string>System-wide Encrypted DNS-over-HTTPS via \(provider.displayName) by RyanDPI Unbound.</string>
            <key>PayloadDisplayName</key>
            <string>RyanDPI Encrypted DNS (\(provider.displayName))</string>
            <key>PayloadIdentifier</key>
            <string>com.ryandpi.ios.dns.\(provider.rawValue)</string>
            <key>PayloadType</key>
            <string>com.apple.dnsSettings.managed</string>
            <key>PayloadUUID</key>
            <string>A8E4C712-9F3B-4D21-B91E-320000000001</string>
            <key>PayloadVersion</key>
            <integer>1</integer>
        </dict>
    </array>
    <key>PayloadDescription</key>
    <string>Activates Apple-native Encrypted DNS-over-HTTPS (DoH) on Wi-Fi and 5G/LTE to bypass ISP DNS poisoning and blocks without needing a VPN.</string>
    <key>PayloadDisplayName</key>
    <string>RyanDPI Encrypted DNS (\(provider.displayName))</string>
    <key>PayloadIdentifier</key>
    <string>com.ryandpi.ios.profile.\(provider.rawValue)</string>
    <key>PayloadOrganization</key>
    <string>RyanDPI — Unbound</string>
    <key>PayloadRemovalDisallowed</key>
    <false/>
    <key>PayloadType</key>
    <string>Configuration</string>
    <key>PayloadUUID</key>
    <string>A8E4C712-9F3B-4D21-B91E-320000000002</string>
    <key>PayloadVersion</key>
    <integer>1</integer>
</dict>
</plist>
"""
    }
}
