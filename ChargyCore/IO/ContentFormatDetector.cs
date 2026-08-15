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

using System.Xml.Linq;

using Newtonsoft.Json.Linq;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// Works out what an EV driver actually handed over, and turns it into
    /// something Chargy can verify.
    ///
    /// This is the front door of the library. Whatever arrives — a ZIP download,
    /// a photograph of a QR code, a PDF invoice, an OCMF string pasted from an
    /// e-mail, a bare URL, or a public key file on its own — ends up here, and
    /// none of it announces what it is in a way worth trusting. So the content is
    /// unpacked, sniffed and tried against the known data formats, in an order
    /// that matters: several charge transparency formats would happily accept
    /// another's data and produce a plausible but wrong reading of a charging
    /// session.
    ///
    /// The pipeline is: PDF attachments out, containers unpacked (repeatedly,
    /// because archives nest), QR codes read, public keys collected, and only
    /// then each remaining file handed to the format it looks like.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="Formats">The charge transparency data formats to try.</param>
    /// <param name="PDFAttachmentExtractor">An optional reader for PDF/A-3 attachments.</param>
    /// <param name="QRCodeDecoder">An optional reader for QR code images.</param>
    /// <param name="URLResolver">
    /// An optional resolver for URLs. Without one a URL is reported as a URL and
    /// nothing is fetched, which is the privacy-preserving default.
    /// </param>
    public class ContentFormatDetector(I18NDictionary              I18N,
                                       ChargeTransparencyFormats?  Formats                 = null,
                                       IPDFAttachmentExtractor?    PDFAttachmentExtractor  = null,
                                       IQRCodeDecoder?             QRCodeDecoder           = null,
                                       IURLResolver?               URLResolver             = null)
    {

        #region Data

        /// <summary>
        /// How often the archive expansion may go round.
        ///
        /// Archives nest — a ChargePoint download is a ZIP of "tar.bz2" payloads —
        /// so the loop has to repeat, and therefore has to be bounded: a
        /// self-referential archive must exhaust a counter rather than the machine.
        /// </summary>
        private const Int32 MaxArchiveDepth = 8;

        #endregion

        #region Properties

        /// <summary>The dictionary used to describe what went wrong.</summary>
        public I18NDictionary              I18N                      { get; } = I18N;

        /// <summary>The charge transparency data formats to try.</summary>
        public ChargeTransparencyFormats   Formats                   { get; } = Formats ?? ChargeTransparencyFormats.None;

        /// <summary>An optional reader for PDF/A-3 attachments.</summary>
        public IPDFAttachmentExtractor?    PDFAttachmentExtractor    { get; } = PDFAttachmentExtractor;

        /// <summary>An optional reader for QR code images.</summary>
        public IQRCodeDecoder?             QRCodeDecoder             { get; } = QRCodeDecoder;

        /// <summary>An optional resolver for URLs.</summary>
        public IURLResolver?               URLResolver               { get; } = URLResolver;

        #endregion


        #region DetectAndConvertContentFormat(Files, CancellationToken = default)

        /// <summary>
        /// Work out what the given files are and convert them into charge
        /// transparency data.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        /// <param name="CancellationToken">An optional token to cancel this operation.</param>
        /// <returns>
        /// A <see cref="ChargeTransparencyRecord"/>, a
        /// <see cref="ChargeTransparencyLiveLink"/>, a <see cref="SimpleURL"/>, a
        /// <see cref="PublicKeyLookup"/>, or a <see cref="SessionCryptoResult"/>
        /// saying why it is none of those.
        /// </returns>
        public async Task<Object> DetectAndConvertContentFormat(IEnumerable<FileInfo>  Files,
                                                                CancellationToken      CancellationToken = default)
        {

            #region Initial checks

            var files = Files.ToArray();

            if (files.Length == 0)
                return new SessionCryptoResult(
                           SessionVerificationResult.NoChargeTransparencyRecordsFound,
                           I18N.GetMultilanguageText("No charge transparency records found!")
                       );

            #endregion

            var expandedFiles  = ExpandPDFAttachments(files);
                expandedFiles  = DecompressFiles     (expandedFiles);
                expandedFiles  = ExpandQRCodeImages  (expandedFiles);

            var publicKeys     = CollectPublicKeys   (expandedFiles);
            var processedFiles = new List<ExtendedFileInfo>();

            foreach (var expandedFile in expandedFiles)
                processedFiles.Add(
                    await ProcessFile(
                              expandedFile,
                              publicKeys,
                              CancellationToken
                          ).ConfigureAwait(false)
                );

            #region Were these public key files and nothing else?

            if (PublicKeyFiles.TryCreateLookup(processedFiles) is PublicKeyLookup publicKeyLookup)
                return publicKeyLookup;

            #endregion

            if (processedFiles.Count == 1)
                return processedFiles[0].Result
                           ?? new SessionCryptoResult(
                                  SessionVerificationResult.InvalidSessionFormat,
                                  I18N.GetMultilanguageText("UnknownOrInvalidChargeTransparencyRecord")
                              );

            if (processedFiles.Count > 1 &&
                MergeChargeTransparencyRecords(processedFiles) is ChargeTransparencyRecord mergedRecord)
            {
                return mergedRecord;
            }

            return new SessionCryptoResult(
                       SessionVerificationResult.InvalidSessionFormat,
                       I18N.GetMultilanguageText("No charge transparency records found!")
                   );

        }

        #endregion


        #region ExpandPDFAttachments(Files)

        /// <summary>
        /// Replace every PDF by the charge transparency attachments inside it.
        ///
        /// A PDF/A-3 invoice can carry the record it bills for, which is what
        /// lets an operator hand over a single document that is both readable and
        /// verifiable.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        public IReadOnlyList<FileInfo> ExpandPDFAttachments(IReadOnlyList<FileInfo> Files)
        {

            var expandedFiles = new List<FileInfo>();

            foreach (var file in Files)
            {

                var isPDF = ContentTypes.Normalize(file.Type) == ContentTypes.PDF ||
                            file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                            ContentTypes.FromContent(file.Data.Span) == ContentTypes.PDF;

                if (!isPDF)
                {
                    expandedFiles.Add(file);
                    continue;
                }

                if (PDFAttachmentExtractor is null)
                {
                    // Without a reader the PDF is passed through: the format
                    // detection will report it as unreadable, which is at least
                    // an honest answer.
                    expandedFiles.Add(file);
                    continue;
                }

                try
                {
                    foreach (var attachment in PDFAttachmentExtractor.ExtractAttachments(file.Data))
                        expandedFiles.Add(
                            new FileInfo(
                                attachment.Name,
                                attachment.Data,
                                attachment.Type,
                                Files[0].Path,
                                attachment.Info
                            )
                        );
                }
                catch (Exception exception)
                {
                    expandedFiles.Add(
                        new FileInfo(
                            file.Name,
                            file.Data,
                            file.Type,
                            file.Path,
                            Error:      "Error extracting PDF/A-3 attachments!",
                            Exception:  exception
                        )
                    );
                }

            }

            return expandedFiles;

        }

        #endregion

        #region DecompressFiles     (Files)

        /// <summary>
        /// Unpack every container, and keep going until nothing is left to unpack.
        ///
        /// A ChargePoint download is a ZIP holding one "tar.bz2" per charging
        /// session, so a single pass would leave the actual data still packed.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        public IReadOnlyList<FileInfo> DecompressFiles(IReadOnlyList<FileInfo> Files)
        {

            var currentFiles = Files;

            for (var round = 0; round < MaxArchiveDepth; round++)
            {

                var archiveFound   = false;
                var expandedFiles  = new List<FileInfo>();

                foreach (var file in currentFiles)
                {

                    if (file.Data.Length == 0)
                        continue;

                    var detectedType  = ContentTypes.FromContent(file.Data.Span);
                    var mimeType      = ContentTypes.ForQRCodeImage(file, detectedType);

                    #region A QR code image, a ".chargy" file, XML or JSON: keep as is

                    if (ContentTypes.IsQRCodeImage(mimeType))
                    {
                        expandedFiles.Add(new FileInfo(file.Name, file.Data, file.Type ?? mimeType, file.Path, "QR code image file"));
                        continue;
                    }

                    if (file.Name.EndsWith(".chargy", StringComparison.OrdinalIgnoreCase))
                    {
                        expandedFiles.Add(new FileInfo(file.Name, file.Data, file.Type, file.Path, ".chargy file"));
                        continue;
                    }

                    if (detectedType == ContentTypes.XML)
                    {
                        expandedFiles.Add(new FileInfo(file.Name, file.Data, file.Type, file.Path, "XML file"));
                        continue;
                    }

                    if (detectedType == ContentTypes.JSON)
                    {
                        expandedFiles.Add(new FileInfo(file.Name, file.Data, file.Type, file.Path, "JSON file"));
                        continue;
                    }

                    #endregion

                    #region An archive: unpack it

                    if (ContentTypes.IsArchive(detectedType))
                    {

                        var entries = ArchiveReader.Extract(file.Name, file.Data, detectedType!);

                        if (entries.Count == 0)
                            continue;

                        archiveFound = true;

                        // A single entry keeps the archive's own name, because a
                        // plain ".gz" holds one file that never had a name of its own.
                        if (entries.Count == 1)
                        {

                            var lastDot = file.Name.LastIndexOf('.');

                            expandedFiles.Add(
                                new FileInfo(
                                    lastDot > 0 ? file.Name[..lastDot] : file.Name,
                                    entries[0].Data,
                                    Path: file.Path
                                )
                            );

                            continue;

                        }

                        // A ChargePoint archive holds the record and its detached
                        // signature as two separate files, which only mean
                        // something together.
                        if (TryCombineChargePointArchive(file, entries) is FileInfo combined)
                        {
                            expandedFiles.Add(combined);
                            continue;
                        }

                        foreach (var entry in entries)
                            expandedFiles.Add(
                                new FileInfo(
                                    entry.Name,
                                    entry.Data,
                                    Path: file.Path
                                )
                            );

                        continue;

                    }

                    #endregion

                    expandedFiles.Add(
                        new FileInfo(
                            file.Name,
                            file.Data,
                            file.Type,
                            file.Path,
                            file.Info,
                            file.Error ?? "Unknown file type!"
                        )
                    );

                }

                currentFiles = expandedFiles;

                if (!archiveFound)
                    break;

            }

            return currentFiles;

        }

        #endregion

        #region ExpandQRCodeImages  (Files)

        /// <summary>
        /// Replace every QR code image by the text it holds.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        public IReadOnlyList<FileInfo> ExpandQRCodeImages(IReadOnlyList<FileInfo> Files)
        {

            var expandedFiles = new List<FileInfo>();

            foreach (var file in Files)
            {

                var mimeType = ContentTypes.ForQRCodeImage(
                                   file,
                                   ContentTypes.FromContent(file.Data.Span)
                               );

                if (file.Data.Length == 0 ||
                   !ContentTypes.IsQRCodeImage(mimeType))
                {
                    expandedFiles.Add(file);
                    continue;
                }

                // Without a decoder the image is passed through untouched, exactly
                // as ChargyCore.TS does when its optional image modules are absent.
                if (QRCodeDecoder is null)
                {
                    expandedFiles.Add(file);
                    continue;
                }

                var qrText = QRCodeDecoder.DecodeQRCode(file.Data, mimeType);

                if (qrText is null)
                {
                    expandedFiles.Add(
                        new FileInfo(
                            file.Name,
                            file.Data,
                            file.Type,
                            file.Path,
                            Error: "No QR code with charge transparency data found!"
                        )
                    );
                    continue;
                }

                expandedFiles.Add(
                    new FileInfo(
                        TextFileNameForQRCodeContent(file.Name, qrText),
                        System.Text.Encoding.UTF8.GetBytes(qrText),
                        ContentTypes.Text,
                        file.Path,
                        "Text extracted from QR code image"
                    )
                );

            }

            return expandedFiles;

        }

        #endregion

        #region CollectPublicKeys   (Files)

        /// <summary>
        /// Collect the public keys that arrived alongside the charging data,
        /// keyed by the identifier their file names carry.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        public IReadOnlyDictionary<String, String> CollectPublicKeys(IReadOnlyList<FileInfo> Files)
        {

            var publicKeys = new Dictionary<String, String>(StringComparer.Ordinal);

            foreach (var file in Files)
                if (PublicKeyFiles.TryGetPublicKeyHEX(file.Name, file.AsText()) is String publicKeyHEX)
                    publicKeys[PublicKeyFiles.IdFromFileName(file.Name)] = publicKeyHEX;

            return publicKeys;

        }

        #endregion


        #region (private) ProcessFile(File, PublicKeys, CancellationToken)

        /// <summary>
        /// Work out what a single file is.
        ///
        /// The order of the cases below is the order of ChargyCore.TS and is part
        /// of the behaviour: an OCMF document and a public key file are both text,
        /// and a JSON container may match several formats at once.
        /// </summary>
        /// <param name="File">A file.</param>
        /// <param name="PublicKeys">The public keys that arrived alongside the charging data.</param>
        /// <param name="CancellationToken">An optional token to cancel this operation.</param>
        private async Task<ExtendedFileInfo> ProcessFile(FileInfo                             File,
                                                         IReadOnlyDictionary<String, String>  PublicKeys,
                                                         CancellationToken                    CancellationToken)
        {

            var processedFile  = new ExtendedFileInfo(File);
            var text           = File.AsText();

            #region XML

            if (text.StartsWith("<?xml", StringComparison.Ordinal) ||
                text.StartsWith("<",     StringComparison.Ordinal))
            {
                processedFile.Result = ProcessXML(text);
                return processedFile;
            }

            #endregion

            #region OCMF

            if (text.StartsWith("OCMF", StringComparison.Ordinal))
            {
                processedFile.Result = TryParseText(Formats.OCMF, text, File, PublicKeys);
                return processedFile;
            }

            if (text.StartsWith("\"OCMF", StringComparison.Ordinal) &&
                text.EndsWith  ("\"",     StringComparison.Ordinal))
            {
                processedFile.Result = TryParseText(Formats.OCMF, Unquote(text), File, PublicKeys);
                return processedFile;
            }

            #endregion

            #region PCDF

            if (Formats.PCDF?.CanParse(text) == true)
            {
                processedFile.Result = TryParseText(Formats.PCDF, text, File, PublicKeys);
                return processedFile;
            }

            #endregion

            #region Alfen

            if (text.StartsWith("AP;", StringComparison.Ordinal))
            {
                processedFile.Result = TryParseText(Formats.Alfen, text, File, PublicKeys);
                return processedFile;
            }

            if (text.StartsWith("\"AP;", StringComparison.Ordinal) &&
                text.EndsWith  ("\"",    StringComparison.Ordinal))
            {
                processedFile.Result = TryParseText(Formats.Alfen, Unquote(text), File, PublicKeys);
                return processedFile;
            }

            #endregion

            #region A public key, as PEM or as hexadecimal DER

            if (text.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) &&
                text.EndsWith  ("-----END PUBLIC KEY-----",   StringComparison.Ordinal))
            {
                processedFile.Result = ProcessPublicKey(File.Name, text);
                return processedFile;
            }

            if (PublicKeyParser.LooksLikeAPublicKeyFile(File.Name, text))
            {
                processedFile.Result = ProcessPublicKey(File.Name, text);
                return processedFile;
            }

            #endregion

            #region JSON

            if (text.StartsWith("{", StringComparison.Ordinal) &&
                text.EndsWith  ("}", StringComparison.Ordinal))
            {
                processedFile.Result = ProcessJSON(text);
                return processedFile;
            }

            #endregion

            #region A bare URL

            if (SimpleURL.IsValidURL(text))
            {

                var url = new SimpleURL(text);

                processedFile.Result = URLResolver is not null
                                           ? await URLResolver.ResolveURL(url, CancellationToken).ConfigureAwait(false)
                                           : url;

                return processedFile;

            }

            #endregion

            return processedFile;

        }

        #endregion

        #region (private) ProcessXML     (Text)

        /// <summary>
        /// Work out which XML charge transparency format a document is written in.
        ///
        /// The SAFE transparency software v1.0 does not declare its own XML
        /// namespace, so a document without one has to be guessed at by its root
        /// element — which is exactly the kind of thing that makes a second
        /// implementation of this library worth having.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        private Object ProcessXML(String Text)
        {

            XDocument document;

            try
            {
                document = XDocument.Parse(Text, LoadOptions.PreserveWhitespace);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidXMLChargeTransparencyFormat"),
                           Exception: exception
                       );
            }

            var rootElement    = document.Root;
            var xmlNamespace   = rootElement?.Name.NamespaceName ?? "";
            var rootLocalName  = rootElement?.Name.LocalName     ?? "";

            switch (xmlNamespace)
            {

                case "http://www.mennekes.de/Mennekes.EdlVerification.xsd":
                    return TryParseXML(Formats.Mennekes, document);

                case "http://transparenz.software/schema/2018/07":
                case "https://open.charging.cloud/CTR/2020/01":
                    return TryParseXML(Formats.SAFEXML, document);

                default:
                    {

                        if (rootLocalName is "ChargingProcess" or "Billing")
                            return TryParseXML(Formats.Mennekes, document);

                        var result = TryParseXML(Formats.SAFEXML, document);

                        // Maybe it is the generic XML container format instead? A
                        // container has no "chargingStation" element anywhere.
                        if (LooksUnreadable(result) &&
                            !document.Descendants().Any(element => element.Name.LocalName == "chargingStation"))
                        {
                            return TryParseXML(Formats.XMLContainer, document);
                        }

                        return result;

                    }

            }

        }

        #endregion

        #region (private) ProcessJSON    (Text)

        /// <summary>
        /// Work out which JSON charge transparency format a document is written in.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        private Object ProcessJSON(String Text)
        {

            JObject json;

            try
            {
                json = JObject.Parse(Text);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidJSONChargeTransparencyFormat"),
                           Exception: exception
                       );
            }

            #region A charge transparency live link

            if (ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(json))
            {

                // A live link without a timestamp is stamped on arrival, so that
                // an application can tell how old the information it shows is.
                json["timestamp"] ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                if (ChargeTransparencyLiveLink.TryParse(json, out var liveLink) &&
                    liveLink is not null)
                {
                    return liveLink;
                }

            }

            #endregion

            #region The PTB container format

            if (json["format"]?.Value<String>() == "ptb")
                return TryParseJSON(Formats.PTB, json);

            #endregion

            #region A format that names itself through its JSON-LD context

            var context = json["@context"]?.Value<String>();

            if (context is not null)
            {

                if (context.StartsWith("https://open.charging.cloud/contexts/CTR+json", StringComparison.Ordinal))
                    return ChargeTransparencyRecord.TryParse(json, out var record) && record is not null
                               ? record
                               : new SessionCryptoResult(
                                     SessionVerificationResult.InvalidSessionFormat,
                                     I18N.GetMultilanguageText("UnknownOrInvalidJSONChargeTransparencyFormat")
                                 );

                if (context.StartsWith("https://open.charging.cloud/contexts/publicKey+json", StringComparison.Ordinal))
                    return PublicKey.TryParse(json, out var publicKey) && publicKey is not null
                               ? publicKey
                               : new SessionCryptoResult(
                                     SessionVerificationResult.InvalidPublicKey,
                                     I18N.GetMultilanguageText("UnknownOrInvalidPublicKeyFormat")
                                 );

                if (context.StartsWith("https://www.lichtblick.de/contexts/charging-station-json",         StringComparison.Ordinal) ||
                    context.StartsWith("https://www.eneco.com/contexts/charging-station-json",             StringComparison.Ordinal) ||
                    context.StartsWith("https://www.chargeit-mobility.com/contexts/charging-station-json", StringComparison.Ordinal))
                {
                    return TryParseJSON(Formats.ChargeIT, json);
                }

                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidJSONChargeTransparencyFormat")
                       );

            }

            #endregion

            #region ..., or a format that identifies itself only by being readable

            // Some formats carry no marker at all, so each is asked in turn and
            // the most confident answer wins.
            var candidates = new[] {
                                 TryParseJSON(Formats.ChargeIT,    json),
                                 TryParseJSON(Formats.ChargePoint, json),
                                 TryParseJSON(Formats.OCPI,        json)
                             };

            var best = candidates.OrderByDescending(CertaintyOf).FirstOrDefault();

            return best ?? new SessionCryptoResult(
                               SessionVerificationResult.InvalidSessionFormat,
                               I18N.GetMultilanguageText("UnknownOrInvalidJSONChargeTransparencyFormat")
                           );

            #endregion

        }

        #endregion

        #region (private) ProcessPublicKey(FileName, Text)

        /// <summary>
        /// Read a public key file.
        /// </summary>
        /// <param name="FileName">The name of the file.</param>
        /// <param name="Text">The contents of the file.</param>
        private Object ProcessPublicKey(String  FileName,
                                        String  Text)
        {

            var parsed = PublicKeyParser.TryParse(Text);

            if (parsed is null)
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidPublicKey,
                           I18N.GetMultilanguageText("UnknownOrInvalidPublicKeyFormat")
                       );

            var keyId     = PublicKeyFiles.IdFromFileName(FileName);
            var publicKey = new PublicKey(
                                parsed.ValueHEX,
                                new OIDInfo(parsed.Algorithm, parsed.AlgorithmOID),
                                Context:   [ "https://open.charging.cloud/contexts/publicKey+json" ],
                                Subject:   keyId,
                                Type:      new OIDInfo(parsed.KeyType),
                                Encoding:  "hex",
                                Certainty: 0
                            );

            return new PublicKeyLookup([ publicKey ]);

        }

        #endregion


        #region (private) TryParseXML  (Format, Document)

        /// <summary>
        /// Hand an XML document to a data format, or explain that Chargy was not
        /// built with that format.
        /// </summary>
        /// <param name="Format">A data format, when one is registered.</param>
        /// <param name="Document">An XML document.</param>
        private Object TryParseXML(IXMLChargeTransparencyFormat?  Format,
                                   XDocument                      Document)
        {

            if (Format is null)
                return UnsupportedFormat("UnknownOrInvalidXMLChargeTransparencyFormat");

            try
            {
                return Format.TryParseXML(Document);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidXMLChargeTransparencyFormat"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region (private) TryParseText (Format, Text, File, PublicKeys)

        /// <summary>
        /// Hand text to a data format, together with whichever public key belongs
        /// to it.
        ///
        /// When exactly one key was handed over it is used regardless of its file
        /// name: a user who provides one record and one key has said everything
        /// there is to say about which belongs to which.
        /// </summary>
        /// <param name="Format">A data format, when one is registered.</param>
        /// <param name="Text">The contents of a file.</param>
        /// <param name="File">The file the text came from.</param>
        /// <param name="PublicKeys">The public keys that arrived alongside the charging data.</param>
        private Object TryParseText(ITextChargeTransparencyFormat?       Format,
                                    String                               Text,
                                    FileInfo                             File,
                                    IReadOnlyDictionary<String, String>  PublicKeys)
        {

            if (Format is null)
                return UnsupportedFormat("UnknownOrInvalidChargeTransparencyRecord");

            var publicKeyHEX = PublicKeys.TryGetValue(PublicKeyFiles.IdFromFileName(File.Name), out var byName)
                                   ? byName
                                   : PublicKeys.Count == 1
                                         ? PublicKeys.Values.First()
                                         : null;

            try
            {
                return Format.TryParseText(Text, publicKeyHEX);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidChargeTransparencyRecord"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region (private) TryParseJSON (Format, JSON)

        /// <summary>
        /// Hand a JSON object to a data format, or explain that Chargy was not
        /// built with that format.
        /// </summary>
        /// <param name="Format">A data format, when one is registered.</param>
        /// <param name="JSON">A JSON object.</param>
        private Object TryParseJSON(IJSONChargeTransparencyFormat?  Format,
                                    JObject                         JSON)
        {

            if (Format is null)
                return UnsupportedFormat("UnknownOrInvalidJSONChargeTransparencyFormat");

            try
            {
                return Format.TryParseJSON(JSON);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidJSONChargeTransparencyFormat"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region (private) UnsupportedFormat(MessageKey)

        /// <summary>
        /// Report that Chargy recognised the data format but was not built with it.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the message.</param>
        private SessionCryptoResult UnsupportedFormat(String MessageKey)

            => new (
                   SessionVerificationResult.UnknownCTRFormat,
                   I18N.GetMultilanguageText(MessageKey)
               );

        #endregion


        #region (private) TryCombineChargePointArchive(File, Entries)

        /// <summary>
        /// Combine the two files a ChargePoint archive holds into one.
        ///
        /// ChargePoint ships the record as "secrrct" and its signature as
        /// "secrrct.sign", and the signature was computed over the record's exact
        /// bytes — whitespace and all. So the original text is carried along
        /// verbatim, base64 encoded, because re-serialising the parsed JSON would
        /// destroy the very thing that was signed.
        /// </summary>
        /// <param name="File">The archive.</param>
        /// <param name="Entries">The files inside the archive.</param>
        private static FileInfo? TryCombineChargePointArchive(FileInfo                    File,
                                                              IReadOnlyList<ArchiveEntry> Entries)
        {

            var record     = Entries.FirstOrDefault(entry => entry.Path == "secrrct");
            var signature  = Entries.FirstOrDefault(entry => entry.Path == "secrrct.sign");

            if (record.Path is null || signature.Path is null)
                return null;

            try
            {

                var recordText  = System.Text.Encoding.UTF8.GetString(record.Data.Span);
                var json        = JObject.Parse(recordText);

                json["original"]   = Convert.ToBase64String(record.Data.Span);
                json["signature"]  = Convert.ToHexStringLower(signature.Data.Span);

                return new FileInfo(
                           File.Name,
                           System.Text.Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)),
                           Path: File.Path
                       );

            }
            catch (Exception)
            {
                // Not a ChargePoint archive after all; its files are handed on
                // individually instead.
                return null;
            }

        }

        #endregion

        #region (private) MergeChargeTransparencyRecords(ProcessedFiles)

        /// <summary>
        /// Merge several charge transparency records into one.
        ///
        /// An EV driver may well drop in a whole folder: one record per charging
        /// session, plus the operator's public key. What they want to see is one
        /// account of their charging, so the records are merged and everything
        /// that is not a record is kept aside as evidence or as a complaint.
        /// </summary>
        /// <param name="ProcessedFiles">The files Chargy has made sense of.</param>
        private static ChargeTransparencyRecord? MergeChargeTransparencyRecords(IReadOnlyList<ExtendedFileInfo> ProcessedFiles)
        {

            var records = ProcessedFiles.Select(file => file.Result).OfType<ChargeTransparencyRecord>().ToArray();

            if (records.Length == 0)
                return null;

            #region The overall time span is the earliest begin and the latest end

            String? begin = null;
            String? end   = null;

            foreach (var record in records)
            {

                if (record.Begin is not null && record.Begin.Length > 0 &&
                   (begin is null || begin.Length == 0 || String.CompareOrdinal(begin, record.Begin) > 0))
                    begin = record.Begin;

                if (record.End is not null && record.End.Length > 0 &&
                   (end is null || end.Length == 0 || String.CompareOrdinal(end, record.End) < 0))
                    end = record.End;

            }

            #endregion

            var merged = new ChargeTransparencyRecord(
                             records[0].Id,
                             records[0].Context,
                             begin,
                             end,
                             records.Select(record => record.Description).FirstOrDefault(description => description is not null)
                         );

            foreach (var processedFile in ProcessedFiles)
                switch (processedFile.Result)
                {

                    case ChargeTransparencyRecord record:
                        {

                            foreach (var chargingStationOperator in record.ChargingStationOperators)  merged.AddChargingStationOperator(chargingStationOperator);
                            foreach (var chargingPool            in record.ChargingPools)             merged.AddChargingPool           (chargingPool);
                            foreach (var chargingStation         in record.ChargingStations)          merged.AddChargingStation        (chargingStation);
                            foreach (var chargingTariff          in record.ChargingTariffs)           merged.AddChargingTariff         (chargingTariff);
                            foreach (var chargingSession         in record.ChargingSessions)          merged.AddChargingSession        (chargingSession);
                            foreach (var eMobilityProvider       in record.EMobilityProviders)        merged.AddEMobilityProvider      (eMobilityProvider);
                            foreach (var mediationService        in record.MediationServices)         merged.AddMediationService       (mediationService);
                            foreach (var contract                in record.Contracts)                 merged.AddContract               (contract);
                            foreach (var publicKey               in record.PublicKeys)                merged.AddPublicKey              (publicKey);

                        }
                        break;

                    case PublicKey publicKey:
                        merged.AddPublicKey(publicKey);
                        break;

                    case PublicKeyLookup publicKeyLookup:
                        foreach (var publicKey in publicKeyLookup.PublicKeys)
                            merged.AddPublicKey(publicKey);
                        break;

                    default:
                        merged.AddInvalidDataSet(processedFile);
                        break;

                }

            return merged;

        }

        #endregion


        #region (private, static) TextFileNameForQRCodeContent(FileName, QRCodeText)

        /// <summary>
        /// Name the text taken out of a QR code after what it turned out to hold,
        /// so that the format detection sees a sensible file name rather than
        /// "photo.png".
        /// </summary>
        /// <param name="FileName">The name of the image file.</param>
        /// <param name="QRCodeText">The text the QR code held.</param>
        private static String TextFileNameForQRCodeContent(String  FileName,
                                                           String  QRCodeText)
        {

            var lastDot   = FileName.LastIndexOf('.');
            var baseName  = lastDot > 0 ? FileName[..lastDot] : FileName;
            var text      = QRCodeText.TrimStart();

            if (text.StartsWith("<?xml", StringComparison.Ordinal) || text.StartsWith("<", StringComparison.Ordinal))
                return baseName + ".xml";

            if (text.StartsWith("{",     StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal))
                return baseName + ".json";

            if (text.StartsWith("OCMF|", StringComparison.Ordinal))
                return baseName + ".ocmf";

            return baseName + ".txt";

        }

        #endregion

        #region (private, static) CertaintyOf                (Result)

        /// <summary>
        /// How confident a data format was that the data was its own.
        /// </summary>
        /// <param name="Result">What a data format made of a file.</param>
        private static Double CertaintyOf(Object? Result)

            => Result switch {
                   ChargeTransparencyRecord record  => record.Certainty,
                   SessionCryptoResult      result  => result.Certainty,
                   null                             => -1,
                   _                                => 0
               };

        #endregion

        #region (private, static) LooksUnreadable            (Result)

        /// <summary>
        /// Whether a data format failed to make sense of a document, which is the
        /// signal to try the next candidate format.
        /// </summary>
        /// <param name="Result">What a data format made of a file.</param>
        private static Boolean LooksUnreadable(Object? Result)

            => Result is null ||
              (Result is SessionCryptoResult result &&
               result.Status != SessionVerificationResult.Unvalidated);

        #endregion

        #region (private, static) Unquote                    (Text)

        /// <summary>
        /// Strip the surrounding quotation marks a JSON-encoded payload arrives with.
        /// </summary>
        /// <param name="Text">A quoted text.</param>
        private static String Unquote(String Text)

            => Text.Length >= 2
                   ? Text[1..^1]
                   : Text;

        #endregion


    }

}
