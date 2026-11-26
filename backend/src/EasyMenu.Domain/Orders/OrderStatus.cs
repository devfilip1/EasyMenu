namespace EasyMenu.Domain.Orders;

/// <summary>Order status derived from its parts (FR-026). Contract: paid | in_preparation | ready | picked_up.</summary>
public enum OrderStatus
{
    Paid,
    InPreparation,
    Ready,
    PickedUp,
}
