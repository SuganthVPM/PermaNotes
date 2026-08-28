namespace PermaNotes.Core.Services
{
    public interface IDesktopPinService
    {
        bool Initialize();
        bool Attach(object window);
        void Detach(object window);
        bool Reinitialize();
    }
}
