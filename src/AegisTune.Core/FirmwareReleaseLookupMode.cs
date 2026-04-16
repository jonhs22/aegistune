namespace AegisTune.Core;

public enum FirmwareReleaseLookupMode
{
    NotChecked = 0,
    DirectVendorPage = 1,
    VendorSupportSearch = 2,
    VendorToolWorkflow = 3,
    CatalogFeed = 4,
    ManualReview = 5,
    LookupFailed = 6
}
