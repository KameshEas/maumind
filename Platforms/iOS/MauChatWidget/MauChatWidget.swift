import WidgetKit
import SwiftUI

@main
struct MauChatWidgetBundle: WidgetBundle {
    var body: some Widget {
        MauChatWidget()
    }
}

struct MauChatWidget: Widget {
    let kind: String = "MauChatWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: MauChatProvider()) { entry in
            MauChatWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("MauChat")
        .description("Quick access to MauMind AI Assistant")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

struct MauChatProvider: TimelineProvider {
    func placeholder(in context: Context) -> MauChatEntry {
        MauChatEntry(date: Date())
    }

    func getSnapshot(in context: Context, completion: @escaping (MauChatEntry) -> ()) {
        let entry = MauChatEntry(date: Date())
        completion(entry)
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<MauChatEntry>) -> ()) {
        let entry = MauChatEntry(date: Date())
        let timeline = Timeline(entries: [entry], policy: .never)
        completion(timeline)
    }
}

struct MauChatEntry: TimelineEntry {
    let date: Date
}

struct MauChatWidgetEntryView: View {
    var entry: MauChatProvider.Entry

    @Environment(\.widgetFamily) var family

    var body: some View {
        switch family {
        case .systemSmall:
            SmallWidgetView()
        case .systemMedium:
            MediumWidgetView()
        default:
            SmallWidgetView()
        }
    }
}

struct SmallWidgetView: View {
    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: "brain")
                .font(.system(size: 32))
                .foregroundColor(.blue)
            
            Text("MauMind")
                .font(.headline)
                .foregroundColor(.primary)
            
            Link(destination: URL(string: "maumind://chat")!) {
                HStack {
                    Image(systemName: "message.fill")
                    Text("New Chat")
                }
                .font(.caption)
                .padding(.horizontal, 12)
                .padding(.vertical, 6)
                .background(Color.blue)
                .foregroundColor(.white)
                .cornerRadius(8)
            }
        }
        .padding()
        .containerBackground(.fill.tertiary, for: .widget)
    }
}

struct MediumWidgetView: View {
    var body: some View {
        HStack(spacing: 16) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Image(systemName: "brain")
                        .font(.title)
                        .foregroundColor(.blue)
                    
                    Text("MauMind")
                        .font(.headline)
                        .foregroundColor(.primary)
                }
                
                Text("Your AI Assistant")
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                Link(destination: URL(string: "maumind://chat")!) {
                    HStack {
                        Image(systemName: "message.fill")
                        Text("New Chat")
                    }
                    .font(.caption)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(Color.blue)
                    .foregroundColor(.white)
                    .cornerRadius(8)
                }
            }
            
            Divider()
            
            VStack(alignment: .leading, spacing: 8) {
                Link(destination: URL(string: "maumind://documents")!) {
                    HStack {
                        Image(systemName: "doc.fill")
                        Text("Documents")
                    }
                    .font(.caption)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 8)
                    .background(Color(.systemGray6))
                    .cornerRadius(8)
                }
                
                Link(destination: URL(string: "maumind://settings")!) {
                    HStack {
                        Image(systemName: "gear")
                        Text("Settings")
                    }
                    .font(.caption)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 8)
                    .background(Color(.systemGray6))
                    .cornerRadius(8)
                }
            }
            .frame(maxWidth: .infinity)
        }
        .padding()
        .containerBackground(.fill.tertiary, for: .widget)
    }
}

#Preview(as: .systemSmall) {
    MauChatWidget()
} timeline: {
    MauChatEntry(date: .now)
}

#Preview(as: .systemMedium) {
    MauChatWidget()
} timeline: {
    MauChatEntry(date: .now)
}
