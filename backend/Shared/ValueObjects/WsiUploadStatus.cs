namespace HiveOrders.Api.Features.Wsi;

/// <summary>WSI upload lifecycle status. Uploading = S3 PUT in progress; Ready = confirmed and available for analysis.</summary>
public static class WsiUploadStatusValues
{
    public const string Uploading = "Uploading";
    public const string Ready = "Ready";
}
