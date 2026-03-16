using Foundation;

namespace MauMind.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void DidEnterBackground(UIKit.UIApplication application)
	{
		base.DidEnterBackground(application);

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
}
