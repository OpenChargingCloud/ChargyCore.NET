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

using SkiaSharp;

using Svg.Skia;

using ZXing;
using ZXing.Common;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.qrcodes
{

    /// <summary>
    /// Reads the text out of a QR code image, using ZXing to decode the code and
    /// Skia to decode the image it sits in.
    ///
    /// Many charging stations print the charge transparency record as a QR code
    /// on the receipt or show it on their display, so a photograph of that code
    /// is an ordinary way for an EV driver to arrive at Chargy — and a
    /// photograph is exactly the hardest case: skewed, badly lit, and sometimes
    /// inverted by a dark-mode display.
    /// </summary>
    public class QRCodeDecoder : IQRCodeDecoder
    {

        #region Data

        /// <summary>
        /// The smallest edge length an image is rendered at.
        ///
        /// A QR code delivered as SVG carries no resolution of its own: the ALFEN
        /// test data is a 101 by 101 unit drawing holding a whole XML document.
        /// Rendered at its nominal size, each module would be a single pixel and
        /// the slightest resampling would destroy it.
        /// </summary>
        private const Int32 MinimumRenderSize = 1024;

        /// <summary>
        /// The largest edge length an image is rendered at, so that a hostile or
        /// simply absurd drawing cannot exhaust memory.
        /// </summary>
        private const Int32 MaximumRenderSize = 8192;

        private readonly BarcodeReaderGeneric reader;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new QR code decoder.
        /// </summary>
        public QRCodeDecoder()
        {

            reader = new BarcodeReaderGeneric {
                         AutoRotate  = true,
                         Options     = new DecodingOptions {
                                           PossibleFormats  = [ BarcodeFormat.QR_CODE ],
                                           // A charge transparency record is a large
                                           // QR code photographed by hand, so the
                                           // slower and more forgiving search is the
                                           // one worth having.
                                           TryHarder        = true,
                                           TryInverted      = true,
                                           PureBarcode      = false
                                       }
                     };

        }

        #endregion


        #region DecodeQRCode(Data, MIMEType = null)

        /// <summary>
        /// Read the text of the QR code in the given image, or return null when
        /// the image holds none.
        /// </summary>
        /// <param name="Data">The bytes of an image.</param>
        /// <param name="MIMEType">The type of the image, when known.</param>
        public String? DecodeQRCode(ReadOnlyMemory<Byte>  Data,
                                    String?               MIMEType = null)
        {

            if (Data.Length == 0)
                return null;

            try
            {

                using var bitmap = Rasterize(Data, MIMEType);

                if (bitmap is null)
                    return null;

                var text = reader.Decode(LuminanceSourceOf(bitmap))?.Text?.Trim();

                return text is not null && text.Length > 0
                           ? text
                           : null;

            }
            catch (Exception)
            {
                // An image Chargy cannot decode is not a QR code it can read.
                // Whatever the file is, the format detection will judge it on
                // its own terms.
                return null;
            }

        }

        #endregion


        #region (private, static) Rasterize        (Data, MIMEType)

        /// <summary>
        /// Turn the bytes of an image into a bitmap.
        /// </summary>
        /// <param name="Data">The bytes of an image.</param>
        /// <param name="MIMEType">The type of the image, when known.</param>
        private static SKBitmap? Rasterize(ReadOnlyMemory<Byte>  Data,
                                           String?               MIMEType)
        {

            var normalized = ContentTypes.Normalize(MIMEType);

            if (normalized is "image/svg" or "image/svg+xml" || LooksLikeSVG(Data.Span))
                return RasterizeSVG(Data);

            using var skiaData  = SKData.CreateCopy(Data.ToArray());
            var       decoded   = SKBitmap.Decode(skiaData);

            return decoded;

        }

        #endregion

        #region (private, static) RasterizeSVG     (Data)

        /// <summary>
        /// Render an SVG drawing at a resolution a QR code decoder can work with.
        ///
        /// Antialiasing is switched off deliberately: a QR module is either black
        /// or white, and a grey edge only makes the decoder's job harder.
        /// </summary>
        /// <param name="Data">The bytes of an SVG document.</param>
        private static SKBitmap? RasterizeSVG(ReadOnlyMemory<Byte> Data)
        {

            using var stream  = new MemoryStream(Data.ToArray(), writable: false);
            using var svg     = new SKSvg();

            if (svg.Load(stream) is not SKPicture picture)
                return null;

            var bounds = picture.CullRect;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            #region Scale the drawing up to a size a decoder can read

            var scale   = Math.Max(
                              1f,
                              MinimumRenderSize / Math.Max(bounds.Width, bounds.Height)
                          );

            var width   = (Int32) Math.Ceiling(bounds.Width  * scale);
            var height  = (Int32) Math.Ceiling(bounds.Height * scale);

            if (width  > MaximumRenderSize ||
                height > MaximumRenderSize)
            {
                scale   = Math.Min(MaximumRenderSize / bounds.Width,
                                   MaximumRenderSize / bounds.Height);
                width   = (Int32) Math.Ceiling(bounds.Width  * scale);
                height  = (Int32) Math.Ceiling(bounds.Height * scale);
            }

            #endregion

            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(bitmap))
            {

                // An SVG QR code usually paints its own white background, but not
                // always; without this, a transparent background would decode as black.
                canvas.Clear(SKColors.White);

                canvas.Scale(scale);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture);

            }

            return bitmap;

        }

        #endregion

        #region (private, static) LooksLikeSVG     (Data)

        /// <summary>
        /// Whether the given bytes are an SVG document, whatever the file claims.
        /// </summary>
        /// <param name="Data">The bytes of a file.</param>
        private static Boolean LooksLikeSVG(ReadOnlySpan<Byte> Data)

            => ContentTypes.FromContent(Data) == "image/svg+xml";

        #endregion

        #region (private, static) LuminanceSourceOf(Bitmap)

        /// <summary>
        /// Present a bitmap to ZXing as a grid of brightness values.
        /// </summary>
        /// <param name="Bitmap">A bitmap.</param>
        private static LuminanceSource LuminanceSourceOf(SKBitmap Bitmap)
        {

            // Skia decodes into whatever layout suits the source image, so the
            // pixels are copied into one known format rather than guessed at.
            using var normalized = new SKBitmap(
                                       new SKImageInfo(
                                           Bitmap.Width,
                                           Bitmap.Height,
                                           SKColorType.Rgba8888,
                                           SKAlphaType.Unpremul
                                       )
                                   );

            using (var canvas = new SKCanvas(normalized))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(Bitmap, 0, 0);
            }

            return new RGBLuminanceSource(
                       normalized.Bytes,
                       normalized.Width,
                       normalized.Height,
                       RGBLuminanceSource.BitmapFormat.RGBA32
                   );

        }

        #endregion


    }

}
