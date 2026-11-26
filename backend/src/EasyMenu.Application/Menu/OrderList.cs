using EasyMenu.Domain.Common;

namespace EasyMenu.Application.Menu;

/// <summary>Reordering by a complete list of ids: position = index in the list (data-model, FR-011).</summary>
internal static class OrderList
{
    public const string InvalidOrderList = "invalid_order_list";

    /// <summary>
    /// Applies the order in <paramref name="ids"/> to <paramref name="entities"/>. The list must contain every id
    /// exactly once (no missing, duplicated, foreign or unknown ids); otherwise nothing changes and
    /// <c>invalid_order_list</c> is thrown.
    /// </summary>
    public static void Apply<T>(IReadOnlyList<Guid> ids, IReadOnlyCollection<T> entities, Func<T, Guid> idOf, Action<T, int> moveTo)
    {
        var byId = entities.ToDictionary(idOf);
        if (ids.Count != byId.Count || ids.Distinct().Count() != ids.Count || !ids.All(byId.ContainsKey))
        {
            throw new DomainException(InvalidOrderList);
        }

        for (var position = 0; position < ids.Count; position++)
        {
            moveTo(byId[ids[position]], position);
        }
    }
}
