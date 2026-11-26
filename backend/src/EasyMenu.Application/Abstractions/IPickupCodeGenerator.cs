namespace EasyMenu.Application.Abstractions;

/// <summary>Generates pickup codes (RandomPickupCodeGenerator in production; replaceable in tests).</summary>
public interface IPickupCodeGenerator
{
    string Generate();
}
