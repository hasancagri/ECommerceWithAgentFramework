namespace WebApp.Pages.Account.Dto;

// --- AddressBook ---
public record AddressItemDto(
    Guid Id, string Province, string District, string Street, string ZipCode, string Line, bool IsDefault);

public record AddAddressRequest(string Province, string District, string Street, string ZipCode, string Line);

public record UpdateAddressRequest(string Province, string District, string Street, string ZipCode, string Line);

// --- Wallet ---
public record CardItemDto(
    Guid Id, string Brand, string Last4, int ExpiryMonth, int ExpiryYear, string? Label, bool IsDefault);

// Ham PAN/CVV yalniz istekte; tokenize'a gecer, yaniti asla PAN/CVV tasimaz.
public record AddCardRequest(string Pan, string Cvv, int ExpiryMonth, int ExpiryYear, string? Label);