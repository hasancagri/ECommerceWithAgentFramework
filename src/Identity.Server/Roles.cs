namespace Identity.Server;

// Uygulama geneli rol adlari. Magic string yerine tek kaynak.
// NOT: Common.Utils.Constants.Roles ile ayni degerleri tasir (servisler onu kullanir);
// ikisi senkron kalmali. Identity.Server Common'i referans etmedigi icin ayri durur.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}