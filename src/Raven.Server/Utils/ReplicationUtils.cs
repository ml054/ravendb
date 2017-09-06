﻿using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Http;
using Raven.Client.ServerWide.Commands;
using Raven.Server.Documents;
using Sparrow.Json;

namespace Raven.Server.Utils
{
    internal static class ReplicationUtils
    {
        public static TcpConnectionInfo GetTcpInfo(string url, string databaseName, string tag, X509Certificate2 certificate)
        {
            using (var requestExecutor = ClusterRequestExecutor.CreateForSingleNode(url, certificate))
            using (requestExecutor.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            {
                var getTcpInfoCommand = new GetTcpInfoCommand(tag + "/" + databaseName);
                requestExecutor.Execute(getTcpInfoCommand, context);

                var tcpConnectionInfo = getTcpInfoCommand.Result;
                if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase) && tcpConnectionInfo.Certificate == null)
                    throw new InvalidOperationException("Getting TCP info over HTTPS but the server didn't return the expected certificate to use over TCP, invalid response, aborting");

                return tcpConnectionInfo;
            }
        }

        public static async Task<TcpConnectionInfo> GetTcpInfoAsync(string url, string databaseName, string tag, X509Certificate2 certificate)
        {
            using (var requestExecutor = ClusterRequestExecutor.CreateForSingleNode(url, certificate))
            using (requestExecutor.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            {                
                var getTcpInfoCommand = new GetTcpInfoCommand(tag ,databaseName);
                await requestExecutor.ExecuteAsync(getTcpInfoCommand, context);

                var tcpConnectionInfo = getTcpInfoCommand.Result;
                if (url.StartsWith("https",StringComparison.OrdinalIgnoreCase) && tcpConnectionInfo.Certificate == null)
                    throw new InvalidOperationException("Getting TCP info over HTTPS but the server didn't return the expected certificate to use over TCP, invalid response, aborting");
                return tcpConnectionInfo;
            }
        }

        public static void EnsureCollectionTag(BlittableJsonReaderObject obj, string collection)
        {
            if (obj.TryGet(Constants.Documents.Metadata.Key, out BlittableJsonReaderObject metadata) == false ||
                metadata.TryGet(Constants.Documents.Metadata.Collection, out string actualCollection) == false ||
                actualCollection != collection)
            {
                if (collection == CollectionName.EmptyCollection)
                    return;

                ThrowInvalidCollectionAfterResolve(collection, null);
            }
        }

        private static void ThrowInvalidCollectionAfterResolve(string collection, string actual)
        {
            throw new InvalidOperationException(
                "Resolving script did not setup the appropriate '@collection'. Expected " + collection + " but got " +
                actual);
        }
    }
}
