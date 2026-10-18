using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockNotice
{
    public interface IMarketQuoteProvider
    {
        string Name { get; }
        Task<ProviderResult> GetQuotesAsync(List<StockConfig> stocks);
    }
}
