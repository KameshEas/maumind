using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MauMind.App.Data;
using MauMind.App.Models;
using MauMind.App.Services;
using MauMind.App.ViewModels;
using MauMind.App.Views;
using CommunityToolkit.Mvvm.Messaging;

namespace MauMind.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register services and app components
		// Data
		builder.Services.AddSingleton<DatabaseService>();

		// Services
		builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
		builder.Services.AddSingleton<IMemoryService, MemoryService>();
		builder.Services.AddSingleton<IAccessibilityService, AccessibilityService>();
		builder.Services.AddSingleton<IVectorStore, VectorStore>();
		builder.Services.AddSingleton<IDocumentService, DocumentService>();
		builder.Services.AddSingleton<IDocumentScanService, DocumentScanService>();
		builder.Services.AddSingleton<ISecretModeService, SecretModeService>();
		builder.Services.AddSingleton<IChatService, HybridChatService>();
		builder.Services.AddSingleton<IVoiceService, VoiceService>();
		builder.Services.AddSingleton<IThemeService, ThemeService>();
		builder.Services.AddSingleton<IDataService, DataService>();
		builder.Services.AddSingleton<IAnimationService, AnimationService>();
		builder.Services.AddSingleton<ISkeletonService, SkeletonService>();
		builder.Services.AddSingleton<IFirstLaunchService, FirstLaunchService>();
		builder.Services.AddSingleton<IModelManager, ModelManager>();
		builder.Services.AddSingleton<IWebSearchService, WebSearchService>();
		builder.Services.AddSingleton<IFollowUpService, FollowUpService>();
		builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
		builder.Services.AddSingleton<IFolderService, FolderService>();
		builder.Services.AddSingleton<IWritingAssistantService, WritingAssistantService>();

		// ── Auth + Billing ─────────────────────────────────────────────────────
		// Load backend keys from app-data JSON (empty keys → offline/local mode).
		var backendConfig = BackendConfig.Load();
		builder.Services.AddSingleton(backendConfig);

		// Dedicated HttpClient instances so headers never bleed between services.
		builder.Services.AddSingleton<IFirebaseAuthClient>(_ =>
			new FirebaseAuthClient(new HttpClient(), backendConfig.FirebaseWebApiKey));

		// RemoteAuthService uses Firebase when configured,
		// otherwise falls back to local-only auth automatically.
		builder.Services.AddSingleton<IAuthService, RemoteAuthService>();

		// ViewModels - use per-page / transient lifetimes to avoid singleton stateful VMs
		builder.Services.AddTransient<NoteEditorViewModel>();
		builder.Services.AddTransient<ChatViewModel>();
		builder.Services.AddTransient<DocumentsViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		// Messaging (CommunityToolkit) - register the WeakReference messenger as IMessenger
		builder.Services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);

		// Pages / Views
		builder.Services.AddTransient<SplashPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<DocumentsPage>();
		builder.Services.AddTransient<ModelPickerPage>();
		builder.Services.AddTransient<NoteEditorPage>();
		builder.Services.AddTransient<OnboardingPage>();
		builder.Services.AddTransient<PinEntryPage>();
		builder.Services.AddTransient<ScanPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
