namespace AegisTune.Core;

public interface IApplicationReviewHandoffService
{
    ApplicationReviewHandoffRequest? PeekPendingRequest();

    void SetPendingRequest(ApplicationReviewHandoffRequest request);

    void Clear();
}
