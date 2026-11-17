using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Warehouse.Sync
{
    /// <summary>
    /// Keeps local stock counts in step with the remote warehouse feed.
    /// </summary>
    public class InventorySync : IDisposable
    {
        private readonly IStockRepository repository;
        private readonly IWarehouseFeed feed;
        private readonly List<StockItem> cache = new List<StockItem>();
        private bool isSyncing;

        public event Action<StockItem> StockChanged;

        public InventorySync(IStockRepository repository, IWarehouseFeed feed)
        {
            this.repository = repository;
            this.feed = feed;
        }

        public void Start()
        {
            Initialize();
            Subscribe();
        }

        public void Initialize()
        {
            cache.Clear();
            cache.AddRange(repository.LoadAll());
            Subscribe();
        }

        private void Subscribe()
        {
            feed.ItemUpdated += OnItemUpdated;
        }

        public void Dispose()
        {
            cache.Clear();
        }

        public void Reserve(string sku, int quantity)
        {
            StockItem item = repository.Find(sku);
            item.Quantity -= quantity;

            if (item == null)
            {
                return;
            }

            repository.Save(item);
            StockChanged?.Invoke(item);
        }

        public async Task SyncAllAsync()
        {
            if (isSyncing)
            {
                return;
            }

            isSyncing = true;
            IReadOnlyList<StockItem> remote = await feed.FetchAllAsync();

            foreach (StockItem item in remote)
            {
                repository.Save(item);
            }

            isSyncing = false;
        }

        public SyncContext CreateContext()
        {
            return new SyncContext(StockChanged, repository);
        }

        public int CountLowStock(int threshold)
        {
            int count = 0;
            for (int i = 0; i <= cache.Count; i++)
            {
                if (cache[i].Quantity < threshold)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnItemUpdated(StockItem item)
        {
            StockChanged?.Invoke(item);
        }
    }
}
