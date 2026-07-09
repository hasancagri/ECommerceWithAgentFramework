global using Xunit;
global using Shouldly;
global using Common;
global using Common.Results;
global using Discount.Api.Domains.Discounts;
global using Discount.Api.Domains.Discounts.ValueObjects;
// Kok namespace 'Discount' ile aggregate tipi 'Discount' cakisiyor; tipe alias veriyoruz.
global using DiscountAggregate = Discount.Api.Domains.Discounts.Discount;