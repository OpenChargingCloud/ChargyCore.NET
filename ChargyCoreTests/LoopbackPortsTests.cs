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
    /// Tests of the loopback ports the tests' own little servers listen on.
    /// </summary>
    [TestFixture]
    public class LoopbackPortsTests
    {

        #region A_port_taken_before_the_listener_binds_it_is_given_up()

        /// <summary>
        /// Another process may take a free port between asking for it and
        /// binding it. The listener then starts on the next port instead of
        /// failing the test that wanted it.
        /// </summary>
        [Test]
        public void A_port_taken_before_the_listener_binds_it_is_given_up()
        {

            var taken  = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            taken.Start();

            try
            {

                var takenPort  = ((IPEndPoint) taken.LocalEndpoint).Port;
                var asked      = 0;

                using var listener = LoopbackPorts.StartHTTPListener(
                                         () => ++asked == 1
                                                   ? takenPort
                                                   : LoopbackPorts.FreePort(),
                                         out var port
                                     );

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(listener.IsListening,  Is.True);
                    Assert.That(port,                  Is.Not.EqualTo(takenPort));
                    Assert.That(asked,                 Is.EqualTo(2));
                }

            }
            finally
            {
                taken.Stop();
            }

        }

        #endregion

    }

}
