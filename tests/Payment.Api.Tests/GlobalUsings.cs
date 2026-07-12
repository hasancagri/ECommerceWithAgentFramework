global using Xunit;
global using Shouldly;
global using Common;
global using Common.Results;
global using Payment.Api.Domains.Payments;
// Kok namespace 'Payment' ile aggregate tipi 'Payment' cakisiyor; tipe alias veriyoruz.
global using PaymentAggregate = Payment.Api.Domains.Payments.Payment;