namespace EasyMenu.Application.Abstractions;

/// <summary>Generates pickup codes (RandomPickupCodeGenerator in production; replaceable in tests).</summary>
/// <remarks>Signature created by the test author (T047) so the tests compile; implementation belongs to T049.</remarks>
public interface IPickupCodeGenerator
{
    string Generate();
}
