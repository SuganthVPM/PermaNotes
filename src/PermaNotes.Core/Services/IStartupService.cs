namespace PermaNotes.Core.Services
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        void SetStartupEnabled(bool enabled);
    }
}
