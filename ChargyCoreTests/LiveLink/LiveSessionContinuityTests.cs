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

using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using cloud.charging.open.chargy.LiveLink;

#endregion

namespace cloud.charging.open.chargy.tests.LiveLink
{

    /// <summary>
    /// The port of "LiveSessionContinuity.tests.ts" of ChargyCore.TS: a live
    /// session from waiting through start and an intermediate reading to its
    /// end, each document the next one after the one before.
    ///
    /// The four documents in "POILive" are shared byte-for-byte with ChargyCore.TS.
    /// Their "updates" references were computed by another implementation, so a
    /// transition that is accepted here also shows that both canonicalize a
    /// document to the same bytes.
    /// </summary>
    [TestFixture]
    public class LiveSessionContinuityTests : AChargyTests
    {

        #region Data

        internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-01T00:16:21Z", CultureInfo.InvariantCulture);

        internal static String File(String Name)

            => ReadTextFixture($"POILive/{Name}.json");

        #endregion


        [Test]
        public void ValidatesTheGenuineCumulativeSequenceFromZeroThroughStartIntermediateEnd()
        {

            String[] names = [ "00-waiting", "01-start", "02-intermediate", "03-completed" ];

            Assert.That(LiveSessionContinuity.ReadLiveSessionDocument(File(names[0]), Now).Readings, Is.Empty);

            for (var i = 1; i < names.Length; i++)
                Assert.That(LiveSessionContinuity.VerifyLiveSessionTransition(File(names[i - 1]), File(names[i]), Now).Readings,
                            Has.Count.EqualTo(i),
                            names[i]);

        }

        [Test]
        public void NeitherMutatedPayloadsNorImportedVerificationFlagsCanReplaceOriginalSignatureChecks()
        {

            var changed = ChargyLib.ParseJSON(File("01-start"));
            changed["lastUpdated"]            = "2026-11-01T00:05:02Z";
            changed["signatureVerification"]  = new JObject(new JProperty("status", "allValid"));

            Assert.That(() => LiveSessionContinuity.VerifyLiveSessionTransition(File("00-waiting"), changed.ToString(Formatting.None), Now),
                        Throws.InstanceOf<LiveSessionException>());

        }

        [Test]
        public void AGenuineNewerDocumentIsNotAValidUpdateOfAnUnrelatedPredecessor()
        {

            Assert.That(() => LiveSessionContinuity.VerifyLiveSessionTransition(File("00-waiting"), File("02-intermediate"), Now),
                        Throws.InstanceOf<LiveSessionException>().With.Message.Contains("predecessor"));

        }

        [Test]
        public void FutureDocumentsAndUnsupportedReferencePipelinesFailExplicitly()
        {

            Assert.That(() => LiveSessionContinuity.ReadLiveSessionDocument(File("00-waiting"), DateTimeOffset.UnixEpoch),
                        Throws.InstanceOf<LiveSessionException>().With.Message.Contains("future"));

            var changed = ChargyLib.ParseJSON(File("00-waiting"));
            changed["docRefIdGeneration"] = new JArray("MD5", "hex");

            Assert.That(() => LiveSessionContinuity.ReadLiveSessionDocument(changed.ToString(Formatting.None), Now),
                        Throws.InstanceOf<LiveSessionException>());

        }

    }

}
