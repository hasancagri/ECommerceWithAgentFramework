global using Asp.Versioning.Builder;
global using Asp.Versioning;
global using Customer.Api.Dependencies;
global using Customer.Api.Domains.AddressBooks;
global using Customer.Api.Domains.AddressBooks.Features.Agents;
global using Customer.Api.Domains.AddressBooks.ValueObjects;
global using Common.Domains;
global using Common.Dependencies;
global using Common.Exceptions;
global using Common.Extensions;
global using Common;
global using Common.Results;
global using Marten;
global using Marten.Newtonsoft;
global using Weasel.Core;
global using Microsoft.AspNetCore.Mvc;
global using Newtonsoft.Json;
global using System.Reflection;
global using System;
global using Wolverine.Attributes;
global using Wolverine.Marten;
global using Wolverine;
global using Common.Utils.Constants;
global using Customer.Api.Constants;
global using Common.Auths;
global using Common.Utils.Authorization;
global using Common.Utils.Caching;
global using Shared.Utils.Constants;
global using Customer.Api.Domains.AddressBooks.Entities;
global using System.ComponentModel;
global using ModelContextProtocol.Server;

// --- hoisted (sade using'ler dosyalardan taşındı) ---
global using Customer.Api.Domains.MerchantInformations;
global using Customer.Api.Extensions;

// --- hoisted (2+ dosyada tekrar; using consolidation) ---
global using System.Text.Json;
// --- 070: admin MCP yüzeyi (tool adı sabitleri + iz) ---
global using Shared;

global using Microsoft.Extensions.Logging.Abstractions;
global using ModelContextProtocol.Client;
global using ModelContextProtocol.Protocol;
