import Foundation
import SwiftUI
import WidgetKit
import AppKit

struct Todo: Identifiable, Codable {
    var id = UUID()
    var title: String
    var completed = false
}

enum Palette {
    private static func adaptive(_ light: NSColor, _ dark: NSColor) -> Color {
        Color(nsColor: NSColor(name: nil) { appearance in
            appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? dark : light
        })
    }
    static let accent = adaptive(NSColor(srgbRed: 0.20, green: 0.48, blue: 0.36, alpha: 1), NSColor(srgbRed: 0.72, green: 0.65, blue: 0.86, alpha: 1))
    static let background = adaptive(NSColor(srgbRed: 0.967, green: 0.976, blue: 0.969, alpha: 1), NSColor(srgbRed: 0.105, green: 0.109, blue: 0.117, alpha: 1))
    static let card = adaptive(.white, NSColor(srgbRed: 0.15, green: 0.157, blue: 0.17, alpha: 1))
    static let ink = adaptive(NSColor(srgbRed: 0.15, green: 0.20, blue: 0.17, alpha: 1), NSColor(srgbRed: 0.91, green: 0.92, blue: 0.94, alpha: 1))
    static let secondary = adaptive(NSColor(srgbRed: 0.44, green: 0.51, blue: 0.47, alpha: 1), NSColor(srgbRed: 0.59, green: 0.61, blue: 0.65, alpha: 1))
    static let border = adaptive(NSColor(srgbRed: 0.85, green: 0.90, blue: 0.87, alpha: 1), NSColor(srgbRed: 0.24, green: 0.25, blue: 0.27, alpha: 1))
    static let outline = adaptive(NSColor(srgbRed: 0.60, green: 0.71, blue: 0.65, alpha: 1), NSColor(srgbRed: 0.43, green: 0.45, blue: 0.49, alpha: 1))
    static let checkInk = adaptive(.white, NSColor(srgbRed: 0.15, green: 0.17, blue: 0.23, alpha: 1))

}

enum TaskDisk {
    // Both targets must use the same registered App Group.
    static let group = "group.com.example.Daylight"
    static func url() throws -> URL {
        guard let directory = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: group) else {
            throw CocoaError(.fileNoSuchFile)
        }
        return directory.appendingPathComponent("todos.json")
    }
    static func read() throws -> [Todo] {
        let file = try url()
        guard FileManager.default.fileExists(atPath: file.path) else { return [] }
        return try JSONDecoder().decode([Todo].self, from: Data(contentsOf: file))
    }
    static func write(_ items: [Todo]) throws {
        try JSONEncoder().encode(items).write(to: url(), options: .atomic)
    }
}

@MainActor final class TaskStore: ObservableObject {
    @Published private(set) var items: [Todo] = []
    @Published var error: String?
    private var canWrite = true
    init() {
        do { items = try TaskDisk.read() }
        catch {
            canWrite = false
            self.error = "无法读取待办数据，请检查 App Group 配置。为保护原有数据，暂时禁止写入。\n\(error.localizedDescription)"
        }
    }
    private func commit(_ updated: [Todo]) {
        guard canWrite else { return }
        do {
            try TaskDisk.write(updated)
            items = updated
            WidgetCenter.shared.reloadAllTimelines()
        } catch { self.error = "保存失败：\(error.localizedDescription)" }
    }
    func add(_ title: String) {
        let clean = title.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return }
        commit(items + [Todo(title: String(clean.prefix(300)))])
    }
    func toggle(_ id: UUID) {
        var next = items
        guard let index = next.firstIndex(where: { $0.id == id }) else { return }
        next[index].completed.toggle()
        commit(next)
    }
    func delete(_ id: UUID) { commit(items.filter { $0.id != id }) }
}
