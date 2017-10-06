﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Commands;
using Raven.Client.ServerWide.Tcp;
using Raven.Server.Json;
using Raven.Server.Routing;
using Raven.Server.Utils;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Logging;
using Sparrow.Utils;

namespace Raven.Server.Web.System
{
    public class TestConnectionHandler : RequestHandler
    {
        [RavenAction("/admin/test-connection", "POST", AuthorizationStatus.Operator)]
        public async Task TestConnection()
        {
            var url = GetQueryStringValueAndAssertIfSingleAndNotEmpty("url");
            DynamicJsonValue result;

            try
            {
                var timeout = TimeoutManager.WaitFor(ServerStore.Configuration.Cluster.OperationTimeout.AsTimeSpan);
                var connectionInfo = ReplicationUtils.GetTcpInfoAsync(url, null, "Test-Connection", Server.ClusterCertificateHolder.Certificate);
                if (await Task.WhenAny(timeout, connectionInfo) == timeout)
                {
                    throw new TimeoutException($"Waited for {ServerStore.Configuration.Cluster.OperationTimeout.AsTimeSpan} to receive tcp info from {url} and got no response");
                }
                result = await ConnectToClientNodeAsync(connectionInfo.Result, ServerStore.Engine.TcpConnectionTimeout, LoggingSource.Instance.GetLogger("testing-connection", "testing-connection"));
            }
            catch (Exception e)
            {
                result = new DynamicJsonValue
                {
                    [nameof(NodeConnectionTestResult.Success)] = false,
                    [nameof(NodeConnectionTestResult.Error)] = $"An exception was thrown while trying to connect to {url} : {e}"
                };
            }

            using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                context.Write(writer, result);
            }
        }


        private async Task<(TcpClient TcpClient, Stream Connection)> ConnectAndGetNetworkStreamAsync(TcpConnectionInfo tcpConnectionInfo, TimeSpan timeout, Logger log)
        {
            var tcpClient = await TcpUtils.ConnectSocketAsync(tcpConnectionInfo, timeout, log);
            var connection = await TcpUtils.WrapStreamWithSslAsync(tcpClient, tcpConnectionInfo, Server.ClusterCertificateHolder.Certificate);
            return (tcpClient, connection);
        }

        private async Task<DynamicJsonValue> ConnectToClientNodeAsync(TcpConnectionInfo tcpConnectionInfo, TimeSpan timeout, Logger log)
        {
            var (tcpClient, connection) = await ConnectAndGetNetworkStreamAsync(tcpConnectionInfo, timeout, log);
            using (tcpClient)
            {
                var result = new DynamicJsonValue();

                using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext ctx))
                using (var writer = new BlittableJsonTextWriter(ctx, connection))
                {
                    WriteOperationHeaderToRemote(writer);
                    using (var responseJson = await ctx.ReadForMemoryAsync(connection, $"TestConnectionHandler/{tcpConnectionInfo.Url}/Read-Handshake-Response"))
                    {
                        var headerResponse = JsonDeserializationServer.TcpConnectionHeaderResponse(responseJson);
                        switch (headerResponse.Status)
                        {
                            case TcpConnectionStatus.Ok:
                                result["Success"] = true;
                                break;
                            case TcpConnectionStatus.AuthorizationFailed:
                                result["Success"] = false;
                                result["Error"] = $"Connection to {tcpConnectionInfo.Url} failed because of authorization failure: {headerResponse.Message}";
                                break;
                            case TcpConnectionStatus.TcpVersionMismatch:
                                result["Success"] = false;
                                result["Error"] = $"Connection to {tcpConnectionInfo.Url} failed because of mismatching tcp version {headerResponse.Message}";
                                break;
                        }
                    }

                }
                return result;
            }
        }

        private void WriteOperationHeaderToRemote(BlittableJsonTextWriter writer)
        {
            writer.WriteStartObject();
            {
                writer.WritePropertyName(nameof(TcpConnectionHeaderMessage.Operation));
                writer.WriteString(TcpConnectionHeaderMessage.OperationTypes.Heartbeats.ToString());
                writer.WritePropertyName(nameof(TcpConnectionHeaderMessage.OperationVersion));
                writer.WriteInteger(TcpConnectionHeaderMessage.HeartbeatsTcpVersion);
                writer.WritePropertyName(nameof(TcpConnectionHeaderMessage.DatabaseName));
                writer.WriteNull();
            }
            writer.WriteEndObject();
            writer.Flush();
        }
    }

    public class NodeConnectionTestResult
    {
        public bool Success;
        public string Error;
    }
}
