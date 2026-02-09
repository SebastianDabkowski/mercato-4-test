namespace SD.ProjectName.WebApp.Services;

public interface ICartIdentityService
{
    string GetOrCreateBuyerId();
    string? GetGuestBuyerId();
    void ClearGuestBuyerId();
}
