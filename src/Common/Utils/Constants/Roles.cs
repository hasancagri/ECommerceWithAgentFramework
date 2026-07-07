namespace Common.Utils.Constants;

// Uygulama geneli rol adlari. AuthorizationScopes'un yanindaki auth-sabit ailesi.
// NOT: Identity.Server/Roles.cs ile ayni degerleri tasir; ikisi senkron kalmali.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}