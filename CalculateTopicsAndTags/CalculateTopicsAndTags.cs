﻿using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure;
using YInsights.Shared;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using YInsights.Shared.Poco;
using YInsights.Shared.AI;
using StackExchange.Redis;

namespace CalculateTopicsAndTags
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class CalculateTopicsAndTags : StatelessService
    {
        string EndpointUri = CloudConfigurationManager.GetSetting("DocumentDBUri");
        string PrimaryKey = CloudConfigurationManager.GetSetting("DocumentDBKey");
        string RedisConnection = CloudConfigurationManager.GetSetting("RedisConnection");
        IDatabase Database;
        public CalculateTopicsAndTags(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {

            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(RedisConnection);
            Database = redis.GetDatabase();

            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CalculateTopics();
                    var minutes = 15;
                    await Task.Delay(TimeSpan.FromMinutes(minutes), cancellationToken);
                }
                catch (Exception ex)
                {
                    ApplicationInsightsClient.LogException(ex);
                    ServiceEventSource.Current.ServiceMessage(this, ex.Message);

                }
            }
        }

        private async void CalculateTopics()
        {
            var docclient = new DocumentClient(new Uri(EndpointUri), PrimaryKey, new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp
            });

            await docclient.OpenAsync();
            var articleExistQuery = docclient.CreateDocumentQuery<Article>(
                UriFactory.CreateDocumentCollectionUri("articles", "article")).Where(f => f.processed == true);


            var tags = new Dictionary<string, int>();
            foreach (var article in articleExistQuery)
            {
                foreach (var tag in article.tags)
                {
                    if (tag.Contains("_"))
                        continue;
                    if (tags.ContainsKey(tag))
                    {
                        tags[tag]++;
                    }
                    else
                    {
                        tags.Add(tag, 1);
                    }
                }

                foreach (var topic in article.topics)
                {
                    if (topic.Contains("_"))
                        continue;
                    if (tags.ContainsKey(topic))
                    {
                        tags[topic]++;
                    }
                    else
                    {
                        tags.Add(topic, 1);
                    }
                }
            }

            try
            {
               var listTags =tags.OrderByDescending(x => x.Value);

                var list = new List<dynamic>();
                foreach (var tag in listTags)
                {
                    list.Add(new { topic = tag.Key, count = tag.Value });
                }
              
                await Database.StringSetAsync("Topics", Newtonsoft.Json.JsonConvert.SerializeObject(list));
                await Database.StringSetAsync("WordCloudTopics", Newtonsoft.Json.JsonConvert.SerializeObject(list.Take(1000)));

                ServiceEventSource.Current.ServiceMessage(this, $"Commited {tags.Count} Topics");
                ApplicationInsightsClient.LogEvent($"Commited Topics", tags.Count.ToString());     
            }
            catch (Exception ex)
            {
                ApplicationInsightsClient.LogException(ex);

                ServiceEventSource.Current.ServiceMessage(this, ex.Message);

            }

        }

    }
}