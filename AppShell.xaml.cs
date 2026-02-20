namespace MauMind.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		
		// Set up page transitions
		Routing.RegisterRoute("chat", typeof(Views.ChatPage));
		Routing.RegisterRoute("documents", typeof(Views.DocumentsPage));
		Routing.RegisterRoute("settings", typeof(Views.SettingsPage));
	}
}
