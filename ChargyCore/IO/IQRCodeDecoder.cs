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

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// Reads the text out of a QR code image.
    ///
    /// Many charging stations print the charge transparency record as a QR code
    /// on the receipt, or show it on their display, so a photograph of that code
    /// is a perfectly ordinary way for an EV driver to arrive at Chargy.
    ///
    /// Decoding an image means decoding PNG, JPEG, GIF, WEBP, BMP and SVG, which
    /// is a far heavier dependency than anything else in this library. It is
    /// therefore an interface: "cloud.charging.open.chargy.qrcodes" provides an
    /// implementation, and a consumer that never sees QR codes — a server
    /// verifying OCMF strings, say — does not have to carry the weight.
    ///
    /// Without a decoder Chargy passes QR code images through untouched, exactly
    /// as ChargyCore.TS does when its optional image modules are absent.
    /// </summary>
    public interface IQRCodeDecoder
    {

        /// <summary>
        /// Read the text of the QR code in the given image, or return null when
        /// the image holds none.
        /// </summary>
        /// <param name="Data">The bytes of an image.</param>
        /// <param name="MIMEType">The type of the image, when known.</param>
        String? DecodeQRCode(ReadOnlyMemory<Byte>  Data,
                             String?               MIMEType = null);

    }

}
