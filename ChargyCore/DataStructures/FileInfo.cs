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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// One piece of input handed to Chargy: a file an EV driver dropped onto the
    /// application, or a file extracted from an archive, a PDF/A-3 attachment or
    /// a QR code image.
    /// </summary>
    /// <param name="Name">The name of the file.</param>
    /// <param name="Data">The contents of the file.</param>
    /// <param name="Type">An optional MIME type, as reported by whoever handed the file over.</param>
    /// <param name="Path">An optional path of the file.</param>
    /// <param name="Info">
    /// An optional note about where this file came from, e.g. "extracted from a
    /// PDF/A-3 attachment". Shown to the user, because a file Chargy produced
    /// itself should not look like one they provided.
    /// </param>
    /// <param name="Error">An optional error that occurred while reading the file.</param>
    /// <param name="Exception">An optional exception that occurred while reading the file.</param>
    public class FileInfo(String                Name,
                          ReadOnlyMemory<Byte>  Data,
                          String?               Type       = null,
                          String?               Path       = null,
                          String?               Info       = null,
                          String?               Error      = null,
                          Exception?            Exception  = null)
    {

        #region Properties

        /// <summary>The name of the file.</summary>
        public String                Name         { get; } = Name;

        /// <summary>The contents of the file.</summary>
        public ReadOnlyMemory<Byte>  Data         { get; } = Data;

        /// <summary>An optional MIME type, as reported by whoever handed the file over.</summary>
        public String?               Type         { get; } = Type;

        /// <summary>An optional path of the file.</summary>
        public String?               Path         { get; } = Path;

        /// <summary>An optional note about where this file came from.</summary>
        public String?               Info         { get; } = Info;

        /// <summary>An optional error that occurred while reading the file.</summary>
        public String?               Error        { get; } = Error;

        /// <summary>An optional exception that occurred while reading the file.</summary>
        public Exception?            Exception    { get; } = Exception;

        #endregion


        #region AsText()

        /// <summary>
        /// The contents of this file as UTF-8 text, with a leading byte order mark
        /// and surrounding whitespace removed.
        ///
        /// A UTF-8 BOM in front of an OCMF or XML payload would otherwise make
        /// every format detection fail on the very first character.
        /// </summary>
        public String AsText()
        {

            var text = System.Text.Encoding.UTF8.GetString(Data.Span);

            if (text.Length > 0 && text[0] == '﻿')
                text = text[1..];

            return text.Trim();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this file.
        /// </summary>
        public override String ToString()

            => $"{Name} ({Data.Length} byte(s){(Type is not null ? $", {Type}" : "")})";

        #endregion


    }


    /// <summary>
    /// A file together with whatever Chargy managed to make of it: a charge
    /// transparency record, a URL, a public key, or the reason why none of those
    /// worked out.
    /// </summary>
    /// <param name="FileInfo">The file this result belongs to.</param>
    /// <param name="Result">What Chargy made of the file.</param>
    public class ExtendedFileInfo(FileInfo  FileInfo,
                                  Object?   Result = null)
    {

        #region Properties

        /// <summary>The file this result belongs to.</summary>
        public FileInfo  FileInfo    { get; }      = FileInfo;

        /// <summary>
        /// What Chargy made of the file: a <see cref="ChargeTransparencyRecord"/>,
        /// a <see cref="ChargeTransparencyLiveLink"/>, a <see cref="SimpleURL"/>,
        /// a <see cref="PublicKey"/>, a <see cref="PublicKeyLookup"/> or a
        /// <see cref="SessionCryptoResult"/> describing what went wrong.
        /// </summary>
        public Object?   Result      { get; set; } = Result;

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this file result.
        /// </summary>
        public override String ToString()

            => $"{FileInfo.Name}: {Result?.GetType().Name ?? "<no result>"}";

        #endregion


    }

}
