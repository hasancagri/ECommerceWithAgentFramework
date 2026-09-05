global using Microsoft.AspNetCore.Identity;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.AspNetCore.Mvc.RazorPages;
global using Identity.Server.Rbac;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Authorization;
global using System.Security.Claims;
global using OpenIddict.Server.AspNetCore;
global using OpenIddict.Abstractions;
global using Microsoft.AspNetCore.Authentication;
global using static OpenIddict.Abstractions.OpenIddictConstants;

// --- hoisted (2+ dosyada tekrar; using consolidation) ---
global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.AspNetCore;
global using Microsoft.IdentityModel.Tokens;
global using OpenIddict.Server;
global using System.ComponentModel.DataAnnotations;
global using System.Security.Cryptography;
global using System.Text;
