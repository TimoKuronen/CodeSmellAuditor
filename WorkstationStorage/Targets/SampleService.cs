using System;
using System.Collections.Generic;

namespace SampleApp
{
    /// <summary>
    /// Manages user session state and coordinates data access.
    /// Demonstrates common architectural patterns for audit demos.
    /// </summary>
    public class SampleService
    {
        private readonly string connectionString;
        private readonly int maxRetries;
        private readonly bool enableLogging;
        private readonly ILogger logger;
        private readonly ICache cache;
        private readonly IMetrics metrics;
        private readonly IConfig config;
        private readonly IAuthProvider auth;
        private readonly IDataStore store;

        private static Dictionary<string, object> globalState = new();
        private List<string> recentActions = new();

        public SampleService(
            string conn,
            int retries,
            bool log,
            ILogger logService,
            ICache cacheService,
            IMetrics metricsService,
            IConfig appConfig,
            IAuthProvider authProvider,
            IDataStore dataStore)
        {
            connectionString = conn;
            maxRetries = retries;
            enableLogging = log;
            logger = logService;
            cache = cacheService;
            metrics = metricsService;
            config = appConfig;
            auth = authProvider;
            store = dataStore;
        }

        public string ProcessRequest(string userId, string action)
        {
            globalState[userId] = action;
            recentActions.Add(action);

            if (enableLogging)
            {
                logger.Log($"Processing {action} for {userId}");
            }

            var cached = cache.Get(userId);
            if (cached != null)
            {
                return cached.ToString();
            }

            var result = store.Fetch(userId);
            cache.Set(userId, result);
            metrics.Increment("requests");
            return result;
        }
    }

    public interface ILogger { void Log(string msg); }
    public interface ICache { object Get(string key); void Set(string key, object val); }
    public interface IMetrics { void Increment(string name); }
    public interface IConfig { }
    public interface IAuthProvider { }
    public interface IDataStore { string Fetch(string id); }
}
