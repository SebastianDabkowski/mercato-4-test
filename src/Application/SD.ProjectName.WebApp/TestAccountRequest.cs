using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp;

internal record TestAccountRequest(AccountType AccountType, string Email, string Password, bool EmailConfirmed, bool EnableTwoFactor = false, bool IsAdmin = false);

internal record TwoFactorCodeRequest(string Email);
