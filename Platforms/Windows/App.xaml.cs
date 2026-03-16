using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MauMind.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();

		// Register for Suspending event to ensure services are disposed on shutdown
		this.Suspending += OnSuspending;
	}

	private void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
	{
		try
		{
			var mauiApp = Microsoft.Maui.Controls.Application.Current as MauMind.App.App;
			if (mauiApp != null)
			{
				_ = mauiApp.DisposeServicesAsync();
			}
		}
		catch { }
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

