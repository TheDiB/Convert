namespace Convert.UI.Services
{
    public interface IDialogService
    {
        bool Confirm(string message, string title);
        void Alert(string message, string title);
    }
}
