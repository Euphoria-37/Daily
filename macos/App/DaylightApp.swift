import SwiftUI
import AppKit
import WidgetKit

@main struct DaylightApp: App {
    @StateObject private var store = TaskStore()
    @StateObject private var quotes = QuoteStore()
    @Environment(\.openWindow) private var openWindow
    var body: some Scene {
        WindowGroup("Daily", id: "main") {
            TodoView().environmentObject(store).environmentObject(quotes)
                .frame(minWidth: 280, minHeight: 320)
                .onAppear { WidgetCenter.shared.reloadAllTimelines() }
                .onOpenURL { _ in NSApp.activate(ignoringOtherApps: true) }
        }
        .defaultSize(width: 480, height: 660)
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.automatic)
        MenuBarExtra("Daily", systemImage: "checkmark.circle") {
            VStack(spacing: 0) {
                TodoView(compact: true).environmentObject(store).environmentObject(quotes)
                Divider()
                HStack {
                    Button("打开主窗口") {
                        openWindow(id: "main")
                        NSApp.activate(ignoringOtherApps: true)
                    }
                    Spacer()
                    Button("退出") { NSApp.terminate(nil) }
                }.padding(14)
            }.frame(width: 360, height: 480)
        }.menuBarExtraStyle(.window)
    }
}

struct TodoView: View {
    @EnvironmentObject private var store: TaskStore
    @EnvironmentObject private var quotes: QuoteStore
    var compact = false
    @State private var text = ""
    @FocusState private var inputFocused: Bool
    private var tasks: [Todo] { store.items }
    private var done: Int { tasks.filter(\.completed).count }
    var body: some View {
        GeometryReader { geometry in
            let tight = compact || geometry.size.width < 400 || geometry.size.height < 460
            TimelineView(.periodic(from: .now, by: 60)) { clock in
                content(date: clock.date, tight: tight)
                    .task(id: Calendar.current.startOfDay(for: clock.date)) {
                        await quotes.refresh(for: clock.date)
                    }
            }
        }
    }
    private func content(date: Date, tight: Bool) -> some View {
        VStack(alignment: .leading, spacing: tight ? 12 : 24) {
            VStack(alignment: .leading, spacing: 9) {
                Text(date.formatted(.dateTime.month().day().weekday(.wide)))
                    .font(.system(size: 12, weight: .regular))
                    .foregroundColor(Palette.secondary)
                HStack(alignment: .firstTextBaseline) {
                    Text("Daily").font(.system(size: tight ? 23 : 27, weight: .semibold))
                    Spacer()
                }
                quoteView(tight: tight)
            }
            HStack(spacing: 10) {
                Image(systemName: "plus").font(.system(size: 15, weight: .regular)).foregroundColor(Palette.secondary)
                TextField("添加待办…", text: $text)
                    .font(.system(size: 14)).textFieldStyle(.plain)
                    .focused($inputFocused).onSubmit(add)
                Button(action: add) {
                    Image(systemName: "arrow.turn.down.left")
                        .font(.system(size: 12)).foregroundColor(Palette.secondary)
                        .frame(width: 26, height: 26).contentShape(Rectangle())
                }.buttonStyle(.plain)
                    .disabled(text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                    .accessibilityLabel("添加待办")
            }.padding(.horizontal, 14).padding(.vertical, 10)
                .background(Palette.card, in: RoundedRectangle(cornerRadius: 12))
                .overlay(RoundedRectangle(cornerRadius: 12).stroke(Palette.border, lineWidth: 0.7))
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text("全部事项")
                    Spacer()
                }.font(.system(size: 11)).foregroundColor(Palette.secondary)
                    .padding(.horizontal, 2)
                ScrollView {
                    VStack(alignment: .leading, spacing: 16) {
                    LazyVStack(spacing: 0) {
                        if tasks.isEmpty {
                            Text("暂无待办")
                                .font(.system(size: 13)).foregroundColor(Palette.secondary)
                                .frame(maxWidth: .infinity).padding(.vertical, 42)
                        }
                        ForEach(Array(tasks.enumerated()), id: \.element.id) { index, task in
                            HStack(alignment: .top, spacing: 11) {
                                Button {
                                    withAnimation(.easeInOut(duration: 0.18)) { store.toggle(task.id) }
                                } label: {
                                    ZStack {
                                        Circle().fill(task.completed ? Palette.accent : .clear)
                                        Circle().stroke(task.completed ? Palette.accent : Palette.outline, lineWidth: 1.3)
                                        if task.completed {
                                            Image(systemName: "checkmark")
                                                .font(.system(size: 10, weight: .semibold)).foregroundColor(Palette.checkInk)
                                        }
                                    }.frame(width: 20, height: 20).padding(4)
                                        // Keep the clear interior clickable when unchecked.
                                        .contentShape(Rectangle())
                                }.buttonStyle(.plain)
                                    .accessibilityLabel(task.completed ? "标为未完成" : "标为已完成")
                                    .accessibilityValue(task.title)
                                Text(task.title).font(.system(size: 14))
                                    .strikethrough(task.completed)
                                    .foregroundColor(task.completed ? Palette.secondary : Palette.ink)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .fixedSize(horizontal: false, vertical: true)
                                    .padding(.top, 3)
                                Button { store.delete(task.id) } label: {
                                    Image(systemName: "xmark").font(.system(size: 10))
                                        .foregroundColor(Palette.secondary).frame(width: 22, height: 24).contentShape(Rectangle())
                                }.buttonStyle(.plain).accessibilityLabel("删除：" + task.title)
                            }.padding(.horizontal, 12).padding(.vertical, tight ? 9 : 13)
                            if index < tasks.count - 1 {
                                Rectangle().fill(Palette.border).frame(height: 0.5).padding(.leading, 47)
                            }
                        }
                    }.background(Palette.card, in: RoundedRectangle(cornerRadius: 12))
                    }
                }
            }.frame(maxHeight: .infinity)
            VStack(spacing: 10) {
                HStack {
                    Text("已完成 \(done) / \(tasks.count)").monospacedDigit()
                    Spacer()
                }.font(.system(size: 11)).foregroundColor(Palette.secondary)
                GeometryReader { geometry in
                    ZStack(alignment: .leading) {
                        Capsule().fill(Palette.border.opacity(0.75))
                        Capsule().fill(Palette.accent)
                            .frame(width: geometry.size.width * (tasks.isEmpty ? 0 : CGFloat(done) / CGFloat(tasks.count)))
                    }
                }.frame(height: 4)
            }.accessibilityElement(children: .ignore)
                .accessibilityLabel("完成进度")
                .accessibilityValue("\(done) / \(tasks.count)")
        }.padding(tight ? 16 : 28)
            .background(Palette.background).foregroundColor(Palette.ink)
            .alert("需要留意", isPresented: Binding(get: { store.error != nil }, set: { if !$0 { store.error = nil } })) {
                Button("好") { store.error = nil }
            } message: { Text(store.error ?? "") }
    }
    private func quoteView(tight: Bool) -> some View {
            VStack(alignment: .leading, spacing: 7) {
                Text(quotes.text)
                    .font(.system(size: tight ? 11 : 12, weight: .regular))
                    .lineSpacing(3).lineLimit(tight ? 2 : 3)
                    .foregroundColor(Palette.secondary)
                if let source = quotes.source {
                    Text("— " + source).font(.system(size: 10))
                        .foregroundColor(Palette.secondary.opacity(0.8)).lineLimit(1)
                }
            }.help(quotes.text + (quotes.source.map { " — " + $0 } ?? ""))
    }
    private func add() {
        store.add(text)
        if store.error == nil { text = "" }
        inputFocused = true
    }
}

// A single daily request shared by the main window and menu bar; no task content is sent.
@MainActor final class QuoteStore: ObservableObject {
    @Published var text = "把平凡的小事，做成今天的光。"
    @Published var source: String?
    private var loading = false
    private struct Quote: Codable {
        let hitokoto: String
        let from: String?
        let from_who: String?
    }
    private struct Cache: Codable {
        let day: Date
        let quote: Quote
    }
    init() {
        if let data = UserDefaults.standard.data(forKey: "dailyQuote"),
           let cache = try? JSONDecoder().decode(Cache.self, from: data) {
            show(cache.quote)
        }
    }
    private func show(_ quote: Quote) {
        text = quote.hitokoto
        source = [quote.from_who, quote.from].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: " · ")
        if source?.isEmpty == true { source = nil }
    }
    func refresh(for date: Date) async {
        guard !loading else { return }
        if let data = UserDefaults.standard.data(forKey: "dailyQuote"),
           let cache = try? JSONDecoder().decode(Cache.self, from: data),
           Calendar.current.isDate(cache.day, inSameDayAs: date) { return }
        loading = true
        defer { loading = false }
        do {
            let url = URL(string: "https://v1.hitokoto.cn/?c=d&c=i&c=k&min_length=8&max_length=45")!
            var request = URLRequest(url: url)
            request.timeoutInterval = 12
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else { return }
            let quote = try JSONDecoder().decode(Quote.self, from: data)
            guard !quote.hitokoto.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  quote.hitokoto.count <= 120 else { return }
            let cache = Cache(day: date, quote: quote)
            UserDefaults.standard.set(try JSONEncoder().encode(cache), forKey: "dailyQuote")
            show(quote)
        } catch { /* Keep the cached sentence or local fallback while offline. */ }
    }
}
