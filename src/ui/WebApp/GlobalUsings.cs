global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Text.Json;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.RazorPages;
global using Microsoft.AspNetCore.Authentication;
global using Refit;
global using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
global using WebApp.Services;
global using WebApp.Services.Refit;
global using WebApp.Extensions;
global using WebApp.Dto;

// --- hoisted (2+ dosyada tekrar; using consolidation) ---
global using Duende.IdentityModel.Client;
global using Microsoft.AspNetCore.Authentication.Cookies;
global using Microsoft.AspNetCore.Authentication.OpenIdConnect;
global using Microsoft.IdentityModel.Protocols.OpenIdConnect;
global using WebApp.Pages.Admin.Dto;
