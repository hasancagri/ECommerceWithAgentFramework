namespace Identity.Server.Options;

// Ic introspection (resolve) paylasimli gizli deger — section "ApiKeyAuth". ApiKeyEndpoints
// bununla X-Internal-Secret header'ini dogrular (config[...] magic-string yerine).
public class ApiKeyAuth
{
    public string? InternalSecret { get; set; }
}
