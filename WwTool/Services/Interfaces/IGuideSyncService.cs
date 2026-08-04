namespace WwTool.Services.Interfaces;

public interface IGuideSyncService
{
    Task CaptureSessionAsync(string cUid, string cName, string accessToken, string language, CancellationToken cancellationToken = default);
    Task SyncAsync(string uid, string language, CancellationToken cancellationToken = default);
}
