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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// A postal address, e.g. of a charging station or of a charging station operator.
    ///
    /// Every field is optional: charge transparency records in the wild range from
    /// a full street address down to nothing but a country code.
    /// </summary>
    /// <param name="Street">An optional street.</param>
    /// <param name="HouseNumber">An optional house number.</param>
    /// <param name="FloorLevel">An optional floor level.</param>
    /// <param name="PostalCode">An optional postal code.</param>
    /// <param name="City">An optional city.</param>
    /// <param name="Country">An optional country.</param>
    /// <param name="Comment">An optional multi-language comment.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class Address(String?               Street       = null,
                         String?               HouseNumber  = null,
                         String?               FloorLevel   = null,
                         String?               PostalCode   = null,
                         String?               City         = null,
                         String?               Country      = null,
                         I18NString?           Comment      = null,
                         IEnumerable<String>?  Context      = null)
    {

        #region Properties

        /// <summary>An optional street.</summary>
        public String?                Street         { get; } = Street;

        /// <summary>An optional house number.</summary>
        public String?                HouseNumber    { get; } = HouseNumber;

        /// <summary>An optional floor level.</summary>
        public String?                FloorLevel     { get; } = FloorLevel;

        /// <summary>An optional postal code.</summary>
        public String?                PostalCode     { get; } = PostalCode;

        /// <summary>An optional city.</summary>
        public String?                City           { get; } = City;

        /// <summary>An optional country.</summary>
        public String?                Country        { get; } = Country;

        /// <summary>An optional multi-language comment.</summary>
        public I18NString?            Comment        { get; } = Comment;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  Context        { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Address)

        /// <summary>
        /// Try to parse the given JSON as a postal address.
        /// </summary>
        /// <param name="JSON">A JSON representation of a postal address.</param>
        /// <param name="Address">The parsed postal address.</param>
        public static Boolean TryParse(JObject JSON, out Address? Address)
        {

            Address = new Address(
                          JSON["street"]?.     Value<String>(),
                          JSON["houseNumber"]?.Value<String>(),
                          JSON["floorLevel"]?. Value<String>(),
                          JSON["postalCode"]?. Value<String>(),
                          JSON["city"]?.       Value<String>(),
                          JSON["country"]?.    Value<String>(),
                          JSON["comment"] is JObject commentJSON
                              ? I18NString.Parse(commentJSON)
                              : null,
                          PublicKey.ParseContext(JSON["@context"])
                      );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this postal address.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Context.Count == 1)
                json.Add(new JProperty("@context",     Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",     new JArray(Context)));

            if (Street      is not null)
                json.Add(new JProperty("street",       Street));

            if (HouseNumber is not null)
                json.Add(new JProperty("houseNumber",  HouseNumber));

            if (FloorLevel  is not null)
                json.Add(new JProperty("floorLevel",   FloorLevel));

            if (PostalCode  is not null)
                json.Add(new JProperty("postalCode",   PostalCode));

            if (City        is not null)
                json.Add(new JProperty("city",         City));

            if (Country     is not null)
                json.Add(new JProperty("country",      Country));

            if (Comment.IsNotNullOrEmpty())
                json.Add(new JProperty("comment",      Comment.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this postal address.
        /// </summary>
        public override String ToString()

            => String.Join(
                   ", ",
                   new[] {
                       String.Join(" ", new[] { Street, HouseNumber }.Where(part => part is not null)),
                       String.Join(" ", new[] { PostalCode, City }.   Where(part => part is not null)),
                       Country
                   }.
                   Where(part => !String.IsNullOrWhiteSpace(part))
               );

        #endregion


    }

}
