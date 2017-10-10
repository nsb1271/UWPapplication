﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;
using Rothko;

namespace GitHub.VisualStudio
{
    /// <summary>
    /// Loads and saves the configured connections from/to a .json file.
    /// </summary>
    [Export(typeof(IConnectionCache))]
    public class JsonConnectionCache : IConnectionCache
    {
        const string DefaultCacheFile = "ghfvs.connections";
        readonly string cachePath;
        readonly IOperatingSystem os;

        [ImportingConstructor]
        public JsonConnectionCache(IProgram program, IOperatingSystem os)
            : this(program, os, DefaultCacheFile)
        {
        }

        public JsonConnectionCache(IProgram program, IOperatingSystem os, string cacheFile)
        {
            this.os = os;
            cachePath = Path.Combine(
                os.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                program.ApplicationName,
                cacheFile);
        }

        public Task<IEnumerable<ConnectionDetails>> Load()
        {
            if (os.File.Exists(cachePath))
            {
                try
                {
                    // TODO: Need a ReadAllTextAsync method here.
                    var data = os.File.ReadAllText(cachePath, Encoding.UTF8);
                    var result = SimpleJson.DeserializeObject<CacheData>(data);
                    return Task.FromResult(result.connections.Select(FromCache));
                }
                catch (Exception e)
                {
                    try
                    {
                        os.File.Delete(cachePath);
                    }
                    catch { }

                    VsOutputLogger.WriteLine("Failed to read connection cache from {0}: {1}", cachePath, e);
                }
            }

            return Task.FromResult(Enumerable.Empty<ConnectionDetails>());
        }

        public Task Save(IEnumerable<ConnectionDetails> connections)
        {
            var data = SimpleJson.SerializeObject(new CacheData
            {
                connections = connections.Select(ToCache).ToList(),
            });

            try
            {
                os.File.WriteAllText(cachePath, data);
            }
            catch (Exception e)
            {
                VsOutputLogger.WriteLine("Failed to write connection cache to {0}: {1}", cachePath, e);
            }

            return Task.CompletedTask;
        }

        static ConnectionDetails FromCache(ConnectionCacheItem c)
        {
            return new ConnectionDetails(HostAddress.Create(c.HostUrl), c.UserName);
        }

        static ConnectionCacheItem ToCache(ConnectionDetails c)
        {
            return new ConnectionCacheItem
            {
                HostUrl = c.HostAddress.WebUri,
                UserName = c.UserName,
            };
        }

        class CacheData
        {
            public IEnumerable<ConnectionCacheItem> connections { get; set; }
        }

        class ConnectionCacheItem
        {
            public Uri HostUrl { get; set; }
            public string UserName { get; set; }
        }
    }
}
