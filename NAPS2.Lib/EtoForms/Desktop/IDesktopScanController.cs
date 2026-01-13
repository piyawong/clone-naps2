using NAPS2.Scan;

namespace NAPS2.EtoForms.Desktop;

public interface IDesktopScanController
{
    bool IsScanning { get; }
    Task ScanWithDevice(string deviceID);
    Task ScanDefault();
    Task ScanWithNewProfile();
    Task ScanWithProfile(ScanProfile profile);
}