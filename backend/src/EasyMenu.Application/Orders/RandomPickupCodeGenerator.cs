using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Orders;

public sealed class RandomPickupCodeGenerator : IPickupCodeGenerator
{
    public string Generate() => PickupCode.Generate();
}
