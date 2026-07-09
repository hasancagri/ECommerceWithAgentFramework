global using Xunit;
global using Shouldly;
global using Common;
global using Basket.Api.Domains.Baskets;
global using Basket.Api.Domains.Baskets.Entities;
// Kok namespace 'Basket' ile aggregate tipi 'Basket' cakisiyor; tipe alias veriyoruz.
global using BasketAggregate = Basket.Api.Domains.Baskets.Basket;