namespace EasyMenu.Domain.Orders;

/// <summary>Status of the part of an order prepared by one station (FR-025). Contract: received | in_preparation | ready.</summary>
public enum PartStatus
{
    Received,
    InPreparation,
    Ready,
}
