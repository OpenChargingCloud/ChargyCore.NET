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

using System.Runtime.CompilerServices;
using System.Text;

#endregion

namespace cloud.charging.open.chargy.LiveLink
{

    /// <summary>
    /// One event of a server-sent event stream.
    /// </summary>
    /// <param name="Data">What the event carries.</param>
    /// <param name="EventType">What kind of event this is, when the server said.</param>
    /// <param name="Id">The identification of the event, when the server gave one.</param>
    /// <param name="Retry">How long the server asks a client to wait before reconnecting.</param>
    public readonly record struct ServerSentEvent(String     Data,
                                                  String?    EventType  = null,
                                                  String?    Id         = null,
                                                  TimeSpan?  Retry      = null);


    /// <summary>
    /// Reads a server-sent event stream.
    ///
    /// The framing is the one the HTML standard defines and every browser
    /// implements: lines of "field: value", an empty line ending an event, and
    /// lines beginning with a colon being comments — which is what a server sends
    /// to keep a connection alive through a proxy that would otherwise time it
    /// out.
    ///
    /// It is worth reading events as they arrive rather than waiting for the
    /// response to finish, which is the whole point of the transport: a charging
    /// session that is still running has no end, so a client that waited for one
    /// would show an EV driver nothing at all.
    /// </summary>
    public static class ServerSentEvents
    {

        #region Read(Stream, CancellationToken = default)

        /// <summary>
        /// Read the events of a server-sent event stream, as they arrive.
        /// </summary>
        /// <param name="Stream">The body of a "text/event-stream" response.</param>
        /// <param name="CancellationToken">A token to stop listening.</param>
        public static async IAsyncEnumerable<ServerSentEvent> Read(Stream                                      Stream,
                                                                   [EnumeratorCancellation] CancellationToken  CancellationToken = default)
        {

            using var reader = new StreamReader(Stream, Encoding.UTF8, false, -1, leaveOpen: true);

            var data       = new StringBuilder();
            var eventType  = (String?)   null;
            var id         = (String?)   null;
            var retry      = (TimeSpan?) null;
            var hasData    = false;

            while (!CancellationToken.IsCancellationRequested)
            {

                var line = await reader.ReadLineAsync(CancellationToken).ConfigureAwait(false);

                // The stream ended. An event still being assembled is dispatched
                // rather than dropped: a server that closed the connection after
                // its last event still sent that event.
                if (line is null)
                {

                    if (hasData)
                        yield return new ServerSentEvent(data.ToString(), eventType, id, retry);

                    yield break;

                }

                #region An empty line ends the event

                if (line.Length == 0)
                {

                    if (hasData)
                    {

                        yield return new ServerSentEvent(data.ToString(), eventType, id, retry);

                        data.Clear();
                        eventType  = null;
                        hasData    = false;

                        // The identification and the retry interval deliberately
                        // survive: the standard makes them properties of the
                        // stream, which a client has to remember in order to
                        // resume where it left off.

                    }

                    continue;

                }

                #endregion

                #region A colon at the start is a comment, and usually a heartbeat

                if (line[0] == ':')
                    continue;

                #endregion

                var colon  = line.IndexOf(':');
                var field  = colon < 0 ? line : line[..colon];

                // "field: value" — exactly one leading space of the value belongs
                // to the framing, and any further whitespace belongs to the value.
                var value  = colon < 0
                                 ? ""
                                 : line[(colon + 1)..] is var raw && raw.StartsWith(' ')
                                       ? raw[1..]
                                       : raw;

                switch (field)
                {

                    case "data":
                        // Several "data" lines are one payload split across lines,
                        // joined by the newlines that separated them.
                        if (hasData)
                            data.Append('\n');
                        data.Append(value);
                        hasData = true;
                        break;

                    case "event":
                        eventType = value;
                        break;

                    case "id":
                        // A null character in an id is the one thing the standard
                        // says to ignore outright.
                        if (!value.Contains('\0'))
                            id = value;
                        break;

                    case "retry":
                        if (UInt64.TryParse(value, out var milliseconds))
                            retry = TimeSpan.FromMilliseconds(milliseconds);
                        break;

                }

            }

        }

        #endregion

    }

}
