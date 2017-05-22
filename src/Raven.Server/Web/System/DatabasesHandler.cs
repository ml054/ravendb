﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Http;
using Raven.Client.Server;
using Raven.Client.Server.Operations;
using Raven.Client.Util;
using Raven.Server.Config;
using Raven.Server.Documents;
using Raven.Server.Extensions;
using Raven.Server.Json;
using Raven.Server.Rachis;
using Raven.Server.Routing;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using DatabaseInfo = Raven.Client.Server.Operations.DatabaseInfo;

namespace Raven.Server.Web.System
{
    public class DatabasesHandler : RequestHandler
    {
        [RavenAction("/databases", "GET")]
        public Task Databases()
        {
            var dbName = GetQueryStringValue("info");
            if (dbName != null)
                return DbInfo(dbName);

            var namesOnly = GetBoolValueQueryString("namesOnly", required: false) ?? false;

            //TODO: fill all required information (see: RavenDB-5438) - return Raven.Client.Data.DatabasesInfo
            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            {
                context.OpenReadTransaction();
                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteStartObject();

                    var items = ServerStore.Cluster.ItemsStartingWith(context, Constants.Documents.Prefix, GetStart(), GetPageSize());
                    writer.WriteArray(context, nameof(DatabasesInfo.Databases), items, (w, c, dbDoc) =>
                    {
                        var databaseName = dbDoc.Item1.Substring(Constants.Documents.Prefix.Length);
                        if (namesOnly)
                        {
                            w.WriteString(databaseName);
                            return;
                        }

                        WriteDatabaseInfo(databaseName, dbDoc.Item2, context, w);
                    });
                    writer.WriteEndObject();
                }
            }

            return Task.CompletedTask;
        }

        [RavenAction("/topology", "GET", "/topology?name={databaseName:string}&url={url:string}")]
        public Task GetTopology()
        {
            var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");
            var url = GetStringQueryString("url", false);
            
            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            {
                var dbId = Constants.Documents.Prefix + name;
                using (context.OpenReadTransaction())
                using (var dbBlit = ServerStore.Cluster.Read(context, dbId, out long etag))
                {
                    if (dbBlit == null)
                    {
                        HttpContext.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                        using (var writer = new BlittableJsonTextWriter(context, HttpContext.Response.Body))
                        {
                            context.Write(writer,
                                new DynamicJsonValue
                                {
                                    ["Type"] = "Error",
                                    ["Message"] = "Database " + name + " wasn't found"
                                });
                        }
                        return Task.CompletedTask;
                    }

                    var clusterTopology = ServerStore.GetClusterTopology(context);
                    var dbRecord = JsonDeserializationCluster.DatabaseRecord(dbBlit);
                    using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                    {
                        context.Write(writer, new DynamicJsonValue
                        {
                            [nameof(Topology.Nodes)] = new DynamicJsonArray(
                                dbRecord.Topology.Members.Select(x => new DynamicJsonValue
                                {
                                    [nameof(ServerNode.Url)] = GetUrl(x, url, clusterTopology),
                                    [nameof(ServerNode.ClusterTag)] = x.NodeTag,
                                    [nameof(ServerNode.Database)] = dbRecord.DatabaseName,
                                })
                            ),
                            [nameof(Topology.ReadBehavior)] =
                            ReadBehavior.CurrentNodeWithFailoverWhenRequestTimeSlaThresholdIsReached.ToString(),
                            [nameof(Topology.WriteBehavior)] = WriteBehavior.LeaderOnly.ToString(),
                            [nameof(Topology.SLA)] = new DynamicJsonValue
                            {
                                [nameof(TopologySla.RequestTimeThresholdInMilliseconds)] = 100,
                            },
                            [nameof(Topology.Etag)] = etag,
                        });
                    }
                }
            }

            return Task.CompletedTask;
        }

        private string GetUrl(DatabaseTopologyNode node, string clientUrl, ClusterTopology clusterTopology)
        {
            var url = (Server.ServerStore.NodeTag == node.NodeTag ? clientUrl : null) ??  clusterTopology.GetUrlFromTag(node.NodeTag);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                switch (uri.Host)
                {
                    case "::":
                    case "::0":
                    case "0.0.0.0":
                        url = new UriBuilder(uri)
                        {
                            Host = Environment.MachineName
                        }.Uri.ToString();
                        break;
                }
            }
            return url.TrimEnd('/');
        }

        private Task DbInfo(string dbName)
        {
            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            {
                context.OpenReadTransaction();
                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    var dbId = Constants.Documents.Prefix + dbName;
                    using (var dbDoc = ServerStore.Cluster.Read(context, dbId, out long etag))
                    {
                        WriteDatabaseInfo(dbName, dbDoc, context, writer);
                        return Task.CompletedTask;
                    }
                }
            }
        }

        private void WriteDatabaseInfo(string databaseName, BlittableJsonReaderObject data,
            TransactionOperationContext context, BlittableJsonTextWriter writer)
        {
            var online = ServerStore.DatabasesLandlord.DatabasesCache.TryGetValue(databaseName, out Task<DocumentDatabase> dbTask) &&
                         dbTask != null && 
                         dbTask.IsCompleted;

            // Check for exceptions
            if (dbTask != null && dbTask.IsFaulted)
            {
                WriteFaultedDatabaseInfo(context, writer, dbTask, databaseName);
                return;
            }
           
            var db = online ? dbTask.Result : null;

            IndexRunningStatus indexingStatus = db?.IndexStore.Status ?? IndexRunningStatus.Running;

            // Looking for disabled indexing flag inside the database settings for offline database status
            if (data.TryGet("Settings", out BlittableJsonReaderObject settings) && settings != null)
            {

                if (settings.TryGet(RavenConfiguration.GetKey(x => x.Indexing.Disabled), out bool indexingDisable) &&
                    indexingDisable)
                {
                    indexingStatus = IndexRunningStatus.Disabled;
                }
            }

            data.TryGet("Disabled", out bool disabled);

            data.TryGet("Topology", out BlittableJsonReaderObject topology);
            NodesTopology nodesTopology = new NodesTopology();

            if (topology != null)
            {
                if (topology.TryGet("Members", out BlittableJsonReaderArray members))
                {
                    foreach (BlittableJsonReaderObject member in members)
                    {
                        nodesTopology.Members.Add(GetNodeId(member));
                    }
                }
                if (data.TryGet("Promotables", out BlittableJsonReaderArray promotables))
                {
                    foreach (BlittableJsonReaderObject promotable in promotables)
                    {
                        nodesTopology.Promotables.Add(GetNodeId(promotable));
                    }
                }
                if (data.TryGet("Watchers", out BlittableJsonReaderArray watchers))
                {
                    foreach (BlittableJsonReaderObject watcher in watchers)
                    {
                        nodesTopology.Watchers.Add(GetNodeId(watcher));
                    }
                }
            }


            if (online == false)
            {
                // If state of database is found in the cache we can continue
                if (ServerStore.DatabaseInfoCache.TryWriteOfflineDatabaseStatusToRequest(
                    context, writer, databaseName, disabled, indexingStatus, nodesTopology))
                {
                    return;
                }
                // We won't find it if it is a new database or after a dirty shutdown, so just report empty values then
            }

            var size = new Size(GetTotalSize(db));

           

            DatabaseInfo databaseInfo = new DatabaseInfo
            {
                Name = databaseName,
                Disabled = disabled,
                TotalSize = size,

                IsAdmin = true, //TODO: implement me!
                UpTime = online ? (TimeSpan?)GetUptime(db) : null,
                BackupInfo = GetBackupInfo(db),
                
                Alerts = online ? db.NotificationCenter.GetAlertCount() : 0,
                RejectClients = false, //TODO: implement me!
                LoadError = null,
                IndexingErrors = online ? db.IndexStore.GetIndexes().Sum(index => index.GetErrorCount()) : 0,

                DocumentsCount = online ? db.DocumentsStorage.GetNumberOfDocuments() : 0,
                Bundles = GetBundles(db),
                IndexesCount = online ? db.IndexStore.GetIndexes().Count() : 0,
                IndexingStatus = indexingStatus,

                NodesTopology = nodesTopology
            };

            var doc = databaseInfo.ToJson();
            context.Write(writer, doc);
        }

        private void WriteFaultedDatabaseInfo(TransactionOperationContext context, BlittableJsonTextWriter writer, Task<DocumentDatabase> dbTask, string databaseName)
        {
            var exception = dbTask.Exception;

            var doc = new DynamicJsonValue
            {
                [nameof(DatabaseInfo.Name)] = databaseName,
                [nameof(DatabaseInfo.LoadError)] = exception.ExtractSingleInnerException().Message
            };

            context.Write(writer, doc);
        }
        
        private BackupInfo GetBackupInfo(DocumentDatabase db)
        {
            var periodicExportRunner = db?.BundleLoader.PeriodicExportRunner;

            if (periodicExportRunner == null)
            {
                return null;
            }

            return new BackupInfo()
            {
                IncrementalBackupInterval = periodicExportRunner.IncrementalInterval,
                FullBackupInterval = periodicExportRunner.FullExportInterval,
                LastIncrementalBackup = periodicExportRunner.ExportTime,
                LastFullBackup = periodicExportRunner.FullExportTime
            };
        }

        private TimeSpan GetUptime(DocumentDatabase db)
        {
            return SystemTime.UtcNow - db.StartTime;
        }

        private long GetTotalSize(DocumentDatabase db)
        {
            if (db == null)
                return 0;

            return
                db.GetAllStoragesEnvironment().Sum(env => env.Environment.Stats().AllocatedDataFileSizeInBytes);
        }

        private List<string> GetBundles(DocumentDatabase db)
        {
            if (db != null)
                return db.BundleLoader.GetActiveBundles();

            return new List<string>();
        }

        private NodeId GetNodeId(BlittableJsonReaderObject node)
        {
            NodeId nodeId = new NodeId();

            if (node.TryGet("NodeTag", out string nodeTag))
            {
                nodeId.NodeTag = nodeTag;
            }

            if (node.TryGet("Url", out string nodeUrl))
            {
                nodeId.NodeUrl = nodeUrl;
            }

            return nodeId;
        }
    }
}

