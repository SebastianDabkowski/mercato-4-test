namespace SD.ProjectName.WebApp.Pages.Seller
{
    internal static class PayoutMasking
    {
        public static string? MaskAccountNumber(string? accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return null;
            }

            var trimmed = accountNumber.Trim();
            if (trimmed.Length <= 4)
            {
                return new string('•', trimmed.Length);
            }

            var last4 = trimmed[^4..];
            return $"•••• {last4}";
        }
    }
}
