/*
 * Copyright (c) 2018-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of ChargyCore <https://github.com/OpenChargingCloud/ChargyCore.NET>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Net;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Loopback ports for the tests' own little servers.
    /// </summary>
    internal static class LoopbackPorts
    {

        #region (static) FreePort()

        /// <summary>
        /// A loopback TCP port nobody is using, asked of the operating system.
        /// It is free when asked; nothing keeps it free afterwards.
        /// </summary>
        public static Int32 FreePort()
        {

            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);

            probe.Start();

            var port = ((IPEndPoint) probe.LocalEndpoint).Port;

            probe.Stop();

            return port;

        }

        #endregion

        #region (static) StartHTTPListener(out Port)

        /// <summary>
        /// An HTTP listener started on a fresh loopback port. Another process
        /// may take a free port before the listener binds it, so a port that
        /// turns out to be taken is given up for another one.
        /// </summary>
        /// <param name="Port">The port the listener listens on.</param>
        public static HttpListener StartHTTPListener(out Int32 Port)

            => StartHTTPListener(FreePort, out Port);


        /// <summary>
        /// An HTTP listener started on the first port of the given source
        /// that is not taken.
        /// </summary>
        /// <param name="PortSource">Where the ports to try come from.</param>
        /// <param name="Port">The port the listener listens on.</param>
        internal static HttpListener StartHTTPListener(Func<Int32>  PortSource,
                                                       out Int32    Port)
        {

            for (var attempt = 1; ; attempt++)
            {

                Port          = PortSource();

                var listener  = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{Port}/");

                try
                {
                    listener.Start();
                    return listener;
                }
                catch (HttpListenerException) when (attempt < 10)
                {
                    try
                    {
                        listener.Close();
                    }
                    catch (Exception)
                    {
                        // A listener that never started has nothing to close.
                    }
                }

            }

        }

        #endregion

    }

}
