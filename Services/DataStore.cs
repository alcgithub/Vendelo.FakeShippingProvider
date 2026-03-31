using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendelo.FakeShippingProvider.Models;
using Vendelo.FakeShippingProvider.Options;

namespace Vendelo.FakeShippingProvider.Services
{
    public interface IDataStore
    {
        StorageState Read();
        void Write(StorageState state);
    }

    public class JsonDataStore : IDataStore
    {
        private readonly AppOptions _options;
        private readonly ILogger<JsonDataStore> _logger;
        private readonly object _sync = new object();
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        };

        public JsonDataStore(AppOptions options, ILogger<JsonDataStore> logger)
        {
            _options = options;
            _logger = logger;
            EnsureFile();
        }

        public StorageState Read()
        {
            lock (_sync)
            {
                EnsureFile();
                var raw = File.ReadAllText(_options.Storage.DataFile);
                if (string.IsNullOrWhiteSpace(raw))
                    return CreateDefaultState();

                try
                {
                    var state = JsonSerializer.Deserialize<StorageState>(raw, _jsonOptions);
                    return state ?? CreateDefaultState();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Data file could not be parsed. Recreating.");
                    return CreateDefaultState();
                }
            }
        }

        public void Write(StorageState state)
        {
            lock (_sync)
            {
                EnsureFile();
                state.meta["updatedAtUtc"] = DateTime.UtcNow.ToString("O");
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                File.WriteAllText(_options.Storage.DataFile, json);
            }
        }

        private void EnsureFile()
        {
            var fullPath = Path.GetFullPath(_options.Storage.DataFile);
            var dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(fullPath))
            {
                var state = CreateDefaultState();
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                File.WriteAllText(fullPath, json);
            }
        }

        private StorageState CreateDefaultState()
        {
            var state = new StorageState();
            state.meta["createdAtUtc"] = DateTime.UtcNow.ToString("O");
            state.meta["updatedAtUtc"] = DateTime.UtcNow.ToString("O");
            return state;
        }
    }
}

