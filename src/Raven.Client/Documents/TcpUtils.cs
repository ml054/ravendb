﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Raven.Client.ServerWide.Commands;
using Sparrow.Logging;

namespace Raven.Client.Documents
{
    internal static class TcpUtils
    {
        private static void SetTimeouts(TcpClient client, TimeSpan timeout)
        {
            client.SendTimeout = (int)timeout.TotalMilliseconds;
            client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
        }

        internal static async Task ConnectSocketAsync(TcpConnectionInfo connection, TcpClient tcpClient, Logger log)
        {
            try
            {
                await ConnectAsync(tcpClient, connection.Url).ConfigureAwait(false);
            }
            catch (AggregateException ae) when (ae.InnerException is SocketException)
            {
                if (log.IsInfoEnabled)
                    log.Info(
                        $"Failed to connect to remote replication destination {connection.Url}. Socket Error Code = {((SocketException)ae.InnerException).SocketErrorCode}",
                        ae.InnerException);
                throw;
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                if (log.IsInfoEnabled)
                    log.Info(
                        $@"Tried to connect to remote replication destination {connection.Url}, but the operation was aborted. 
                            This is not necessarily an issue, it might be that replication destination document has changed at 
                            the same time we tried to connect. We will try to reconnect later.",
                        ae.InnerException);
                throw;
            }
            catch (OperationCanceledException e)
            {
                if (log.IsInfoEnabled)
                    log.Info(
                        $@"Tried to connect to remote replication destination {connection.Url}, but the operation was aborted. 
                            This is not necessarily an issue, it might be that replication destination document has changed at 
                            the same time we tried to connect. We will try to reconnect later.",
                        e);
                throw;
            }
            catch (Exception e)
            {
                if (log.IsInfoEnabled)
                    log.Info($"Failed to connect to remote replication destination {connection.Url}", e);
                throw;
            }
        }

        public static Task ConnectAsync(TcpClient tcpClient, string url)
        {
            var uri = new Uri(url);
            if (uri.HostNameType == UriHostNameType.IPv6) 
            {
                var ipAddress = IPAddress.Parse(uri.Host);
                return tcpClient.ConnectAsync(ipAddress, uri.Port);
            }

            return tcpClient.ConnectAsync(uri.Host, uri.Port);
        }

        internal static async Task<Stream> WrapStreamWithSslAsync(TcpClient tcpClient, TcpConnectionInfo info, X509Certificate2 storeCertificate)
        {
            Stream stream = tcpClient.GetStream();
            if (info.Certificate == null)
                return stream;

            var expectedCert = new X509Certificate2(Convert.FromBase64String(info.Certificate));
            var sslStream = new SslStream(stream, false, (sender, actualCert, chain, errors) => expectedCert.Equals(actualCert));

            await sslStream.AuthenticateAsClientAsync(new Uri(info.Url).Host, new X509CertificateCollection(new X509Certificate[]{storeCertificate}), SslProtocols.Tls12, false).ConfigureAwait(false);
            stream = sslStream;
            return stream;
        }

        internal static TcpClient NewTcpClient(TimeSpan? timeout)
        {
            var tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
            tcpClient.Client.DualMode = true;

            if (timeout.HasValue)
                SetTimeouts(tcpClient, timeout.Value);

            Debug.Assert(tcpClient.Client != null);
            return tcpClient;
        }
    }
}
