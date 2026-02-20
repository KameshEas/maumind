# MauMind - Offline-First Local RAG Assistant

<p align="center">
  <img src="Resources/Images/logo.png" width="120" alt="MauMind Logo"/>
</p>

A privacy-centric cross-platform application built with .NET MAUI that leverages on-device AI to create a personal knowledge assistant. The app implements a Retrieval-Augmented Generation (RAG) pipeline entirely on the edge.

## 📱 Features

- **🔒 Privacy-First**: All data stays on your device - no cloud, no tracking
- **🤖 AI-Powered**: Ask questions about your documents using local AI
- **📄 Document Management**: Add notes, daily logs, and import PDFs
- **⚡ Offline**: Works completely offline with on-device AI models
- **🎨 Modern UI**: Beautiful Material Design with dark/light themes
- **📖 Onboarding**: First-launch tutorial for new users

## 🛠️ Tech Stack

- **.NET MAUI** - Cross-platform framework
- **C#** - Programming language
- **Microsoft Semantic Kernel** - AI orchestration
- **ONNX Runtime** - On-device AI inference
- **SQLite** - Local database with vector storage
- **Phi-3 Mini** - Small Language Model (SLM)

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Android SDK (for Android builds)
- Xcode (for iOS builds)

### Build

```bash
# Restore dependencies
dotnet restore

# Build for Android
dotnet build -c Release -f net8.0-android

# Build for iOS
dotnet build -c Release -f net8.0-ios

# Build for Windows
dotnet build -c Release -f net8.0-windows10.0.19041.0
```

### Run

```bash
# Android
dotnet run -f net8.0-android

# Windows
dotnet run -f net8.0-windows10.0.19041.0
```

## 📦 GitLab CI/CD

The project includes a GitLab CI/CD pipeline (`.gitlab-ci.yml`) that:

1. **Builds** the Android APK on every commit
2. **Releases** to GitLab artifacts on tags
3. **Pushes** to Indus App Store (configurable)

### CI/CD Variables

Configure these in your GitLab project settings:

| Variable | Description |
|----------|-------------|
| `INDUS_API_URL` | Indus App Store upload endpoint |
| `INDUS_API_TOKEN` | Authentication token for Indus API |
| `KEYSTORE_PASSWORD` | Android keystore password |
| `GITLAB_TOKEN` | GitLab API token for releases |

### Pipeline Stages

```
┌──────────┐     ┌─────────┐
│  Build   │────▶│ Release │
│ Android  │     │  Store  │
└──────────┘     └─────────┘
```

### Usage

1. **Automatic builds**: Push to `main` branch triggers build
2. **Release**: Create a tag to trigger release:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

## 📁 Project Structure

```
MauMind.App/
├── Models/           # Data models
├── ViewModels/      # MVVM ViewModels
├── Views/           # MAUI Pages
├── Services/        # Business logic
│   ├── ChatService.cs
│   ├── EmbeddingService.cs
│   └── ThemeService.cs
├── Data/            # Database layer
├── Helpers/         # Utilities
├── Resources/       # Assets, styles
└── Platforms/       # Platform-specific code
```

## 📄 License

MIT License - See LICENSE file for details.

## 🙏 Acknowledgments

- Microsoft Semantic Kernel
- ONNX Runtime
- Phi-3 Mini Model
- .NET MAUI Community
