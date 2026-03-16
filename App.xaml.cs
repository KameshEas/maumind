using Microsoft.Extensions.DependencyInjection;
using MauMind.App.Views;
using MauMind.App.Services;

namespace MauMind.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    
    public App(IServiceProvider services)
    {
        InitializeComponent();

        // Use the built service provider from MauiProgram
        Services = services;



        // Resolve initial page via DI so pages can also use constructor injection
        MainPage = Services.GetRequiredService<SplashPage>();
    }

    

    /// <summary>
    /// Dispose async for heavy services. Call on application shutdown.
    /// </summary>
    public async ValueTask DisposeServicesAsync()
    {
        try
        {
            // Dispose VectorStore if present
            if (Services.GetService(typeof(IVectorStore)) is IAsyncDisposable vecAsync)
            {
                await vecAsync.DisposeAsync();
            }

            // Dispose ModelManager
            if (Services.GetService(typeof(IModelManager)) is IAsyncDisposable mmAsync)
            {
                await mmAsync.DisposeAsync();
            }

            // Dispose ChatService
            if (Services.GetService(typeof(IChatService)) is IAsyncDisposable chatAsync)
            {
                await chatAsync.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing services: {ex.Message}");
        }
    }
    
    public static T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }
}
