namespace SD.ProjectName.WebApp.Identity
{
    public static class IdentityRoles
    {
        public const string Buyer = "Buyer";
        public const string Seller = "Seller";
        public const string Admin = "Admin";

        public static readonly string[] All = [Buyer, Seller, Admin];
    }
}
