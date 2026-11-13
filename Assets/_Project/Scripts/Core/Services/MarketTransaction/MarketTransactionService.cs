using System;

// Compatibility facade. Wallet, cargo, market stock and save notifications are committed by one authority.
public class MarketTransactionService : IMarketTransactionService
{
    private readonly IEconomyService _economy;
    public MarketTransactionService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>()) { }
    public MarketTransactionService(IEconomyService economy) { _economy = economy; }
    public BuyItemResult BuyItem(string itemId, int quantity)
    {
        var result = _economy.BuyItem(itemId, quantity);
        var kind = result.Success ? BuyItemResultType.Success : result.ErrorCode switch {
            EconomyErrorCode.InvalidItemId => BuyItemResultType.InvalidItem,
            EconomyErrorCode.InvalidQuantity => BuyItemResultType.InvalidQuantity,
            EconomyErrorCode.NotEnoughCredits => BuyItemResultType.NotEnoughCredits,
            EconomyErrorCode.NotEnoughCargoSpace => BuyItemResultType.NotEnoughCargoSpace,
            EconomyErrorCode.NotEnoughMarketStock => BuyItemResultType.NotEnoughStock,
            _ => BuyItemResultType.InvalidPrice };
        int total = (int)Math.Min(int.MaxValue, Math.Abs((long)result.ChangedCredits));
        return BuyItemResult.Create(kind, itemId, quantity, quantity > 0 ? total / quantity : 0, total);
    }
    public SellItemResult SellItem(string itemId, int quantity)
    {
        var result = _economy.SellItem(itemId, quantity);
        var kind = result.Success ? SellItemResultType.Success : result.ErrorCode switch {
            EconomyErrorCode.InvalidItemId => SellItemResultType.InvalidItem,
            EconomyErrorCode.InvalidQuantity => SellItemResultType.InvalidQuantity,
            EconomyErrorCode.ItemNotOwned => SellItemResultType.ItemNotOwned,
            EconomyErrorCode.NotEnoughItemsToSell => SellItemResultType.NotEnoughQuantity,
            _ => SellItemResultType.InvalidPrice };
        int total = Math.Max(0, result.ChangedCredits);
        return SellItemResult.Create(kind, itemId, quantity, quantity > 0 ? total / quantity : 0, total);
    }
}
