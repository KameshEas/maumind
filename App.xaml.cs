using Microsoft.Extensions.DependencyInjection;
using MauMind.App.Data;
using MauMind.App.Services;
using MauMind.App.ViewModels;
using MauMind.App.Views;

namespace MauMind.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    
    public App()
    {
        InitializeComponent();
        
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        
        // Show animated splash screen - navigation will be handled in SplashPage
        MainPage = new SplashPage();
    }
    
    private void ConfigureServices(IServiceCollection services)
    {
        // Data
        services.AddSingleton<DatabaseService>();
        
        // Services
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<IVectorStore, VectorStore>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IDocumentScanService, DocumentScanService>();
        services.AddSingleton<IChatService, HybridChatService>();
        services.AddSingleton<IVoiceService, VoiceService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IAnimationService, AnimationService>();
        services.AddSingleton<ISkeletonService, SkeletonService>();
        services.AddSingleton<IFirstLaunchService, FirstLaunchService>();
        services.AddSingleton<IModelManager, ModelManager>();
        services.AddSingleton<IWebSearchService, WebSearchService>();
        services.AddSingleton<IFollowUpService, FollowUpService>();
        services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<IWritingAssistantService, WritingAssistantService>();

        // ViewModels - Singleton to preserve loaded AI models
        services.AddTransient<NoteEditorViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<DocumentsViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
    
    public static T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }
}
