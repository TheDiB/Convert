using CommunityToolkit.WinUI.Notifications;

namespace Convert.UI.Services
{
    public static class WindowsNotificationService
    {
        public static void Show(string message)
        {
            new ToastContentBuilder()
                .AddText("Convert")
                .AddText(message)
                .Show();
        }
    }
}
