import SwiftUI

@main
struct RyanDpiApp: App {
    @StateObject private var controller = RyanDpiController()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(controller)
                .preferredColorScheme(.dark)
        }
    }
}
