namespace PermaNotes.Core.Services
{
    public interface IClickThroughService
    {
        void SetClickThrough(object window, bool enabled, (double X, double Y, double W, double H) exemptRegion);
    }
}
