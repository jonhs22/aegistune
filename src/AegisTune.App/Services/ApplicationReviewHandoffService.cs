using AegisTune.Core;

namespace AegisTune.App.Services;

public sealed class ApplicationReviewHandoffService : IApplicationReviewHandoffService
{
    private ApplicationReviewHandoffRequest? _pendingRequest;

    public ApplicationReviewHandoffRequest? PeekPendingRequest() => _pendingRequest;

    public void SetPendingRequest(ApplicationReviewHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _pendingRequest = request;
    }

    public void Clear()
    {
        _pendingRequest = null;
    }
}
