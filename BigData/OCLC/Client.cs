using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using System.IO;
using HtmlAgilityPack;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace BigData.OCLC {
    /// <summary>
    /// Manage access to the OCLC APIs.
    /// </summary>
    public class Client : PublicationSource {

        /// <summary>
        /// Create a new OCLCClient.
        /// </summary>
        /// <param name="key">The WSKey to use to access OCLC APIs</param>
        /// <param name="feedUri">The RSS feed from which to fetch books</param>
        public Client() {
            wsKey = Properties.Settings.Default.WSKey;

            var baseUri = Properties.Settings.Default.RSSUri;
            feedUri = new Uri(
                System.IO.Path.Combine(baseUri, "rss?count=" + Properties.Settings.Default.Count));
            Console.WriteLine(feedUri);
        }

        /// <summary>
        /// Fetch publications from OCLC RSS
        /// </summary>
        /// <returns>An array of publications from the OCLC RSS API</returns>
        public async Task<IEnumerable<Publication>> GetPublications() {
            var request = WebRequest.CreateHttp(feedUri);

            using (var response = await request.GetResponseAsync()) {
                var doc = XDocument.Load(response.GetResponseStream());

                var tasks = from item in doc.Descendants("item")
                            let uri = new Uri(item.Element("link").Value)
                            let oclcNum = uri.Segments.Last()
                            select FetchPublicationFromOCLCNumber(oclcNum);

                var pubs = await Task.WhenAll(tasks);
                Console.WriteLine("Done loading publications from OCLC");
                return pubs;
            }
        }

        string wsKey;
        Uri feedUri;

        /// <summary>
        /// Populates a publication object from an OCLC number
        /// </summary>
        /// <param name="oclcNumber">The OCLC number representing the material</param>
        /// <returns>A publication object</returns>
        async Task<Publication> FetchPublicationFromOCLCNumber(string oclcNumber) {
            var baseUri = @"http://www.worldcat.org/webservices/catalog/content/";
            var queryURI = baseUri + oclcNumber + "?wskey=" + wsKey;

            var request = WebRequest.CreateHttp(queryURI);
            using (var response = await request.GetResponseAsync()) {
                var doc = XDocument.Load(response.GetResponseStream());
                var pub = Publication.FromXML(doc);
                pub.OCLCNumber = oclcNumber;

                try {
                    var imageUriTasks = from num in await FetchAllOCLCNumbers(oclcNumber)
                                        select GetOCLCCoverImageUriAsync(num);

                    foreach (var task in imageUriTasks) {
                        var uris = await task;
                        foreach (var uri in uris) {
                            try {
                                var image = await GetBitmapImage(uri);

                                if (image != null) {
                                    pub.CoverImage = image;
                                    return pub;
                                }
                            } catch (WebException ex) {
                                Console.WriteLine(ex.Message);
                                Console.WriteLine(ex.StackTrace);
                            }
                        }
                    }

                    foreach (var uri in await GetOCLCCoverImageUriAsync(oclcNumber)) {
                        try {
                            var image = await GetBitmapImage(uri);

                            if (image != null) {
                                pub.CoverImage = image;
                                return pub;
                            }
                        } catch (WebException ex) {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine(ex.StackTrace);
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }

                pub.CoverImage = DrawPublicationImage(pub.Title, String.Join(", ", pub.Authors));
                return pub;
            }
        }

        static async Task<IEnumerable<string>> FetchAllOCLCNumbers(string oclcNumber) {
            var baseUri = new Uri(@"http://xisbn.worldcat.org/webservices/xid/oclcnum/" + oclcNumber);

            var token = Properties.Settings.Default.Token;
            var ip = await GetIPAddress();
            var secret = Properties.Settings.Default.Secret;
            string hexDigest;

            using (var hash = MD5.Create()) {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    baseUri.ToString() + "|" +
                    ip + "|" +
                    secret
                );
                byte[] digest = hash.ComputeHash(bytes);
                hexDigest = digest
                    .Select(b => String.Format("{0:x2}", b))
                    .Aggregate("", (acc, s) => acc + s);
            }

            var queryUri = new Uri(baseUri,
                String.Format("?method=getEditions&format=xml&fl=oclcnum&token={0}&hash={1}", token, hexDigest));
            Console.WriteLine(queryUri);

            var request = WebRequest.CreateHttp(queryUri);
            using (var response = await request.GetResponseAsync()) {
                var doc = XDocument.Load(response.GetResponseStream());
                XNamespace ns = @"http://worldcat.org/xid/oclcnum/";
                return from tag in doc.Descendants(ns + "oclcnum")
                       select tag.Value;
            }
        }

        static string localIPAddress;

        struct IPResult {
            public string origin;
        }

        async static Task<string> GetIPAddress() {
            if (localIPAddress != null) {
                return localIPAddress;
            }

            var requestUri = new Uri(@"http://httpbin.org/ip");
            var request = WebRequest.CreateHttp(requestUri);

            using (var response = await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream())) {
                var result = JsonConvert.DeserializeObject<IPResult>(
                    await reader.ReadToEndAsync());
                localIPAddress = result.origin;
            }

            return localIPAddress;
        }

        /// <summary>
        /// Returns the URI of the best available cover image for the book
        /// </summary>
        /// <param name="oclcNumber">The OCLC number representing the material</param>
        /// <returns>Cover Image URI</returns>
        async static Task<Uri[]> GetOCLCCoverImageUriAsync(string oclcNumber) {
            var baseUri = new Uri(@"https://bucknell.worldcat.org/oclc/");
            var oclcUri = new Uri(baseUri, oclcNumber);
            var request = WebRequest.CreateHttp(oclcUri);

            using (var response = await request.GetResponseAsync()) {
                var doc = new HtmlDocument();
                doc.Load(response.GetResponseStream());
                var img = doc.DocumentNode.SelectSingleNode(@"//*[@id='cover']/img");

                var src = img.Attributes["src"].Value;
                Console.WriteLine(src);
                return new Uri[] {
                    new Uri(baseUri.Scheme + ":" + src.Replace("_140.jpg", "_400.jpg")),
                    new Uri(baseUri.Scheme + ":" + src),
                    new Uri(baseUri.Scheme + ":" + src.Replace("_140.jpg", "_70.jpg"))
                };
            }
        }

        /// <summary>
        /// Returns the BitMapImage of the cover image
        /// </summary>
        /// <param name="imageUri">URI of the cover image</param>
        /// <returns>Bitmap image of the cover</returns>
        static async Task<BitmapSource> GetBitmapImage(Uri imageUri) {
            var request = WebRequest.CreateHttp(imageUri);

            var ms = new MemoryStream();
            var response = await request.GetResponseAsync();
            await response.GetResponseStream().CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();

            int stride = (image.Format.BitsPerPixel / 8) * image.PixelWidth;
            var pixels = new byte[stride * image.PixelHeight];
            image.CopyPixels(pixels, stride, 0);

            var average = pixels.Average(b => (decimal?)b);
            if (average < 20 || average > 230) {
                return null;
            } else {
                return image;
            }
        }

        static BitmapSource DrawPublicationImage(string title, string author) {
            var size = new Size(800, 1200);

            var titleText = new FormattedText(title,
                new System.Globalization.CultureInfo("en-us"),
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                72,
                Brushes.CornflowerBlue);
            titleText.MaxTextWidth = size.Width;
            titleText.TextAlignment = TextAlignment.Center;

            var authorText = new FormattedText(author,
                new System.Globalization.CultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                48,
                Brushes.CornflowerBlue);
            authorText.MaxTextWidth = size.Width;
            authorText.TextAlignment = TextAlignment.Center;

            var visual = new DrawingVisual();

            var ctx = visual.RenderOpen();
            ctx.DrawRectangle(Brushes.White, null, new Rect(size));
            ctx.DrawText(titleText, new Point(0, 100));
            ctx.DrawText(authorText, new Point(0, 100 + titleText.Height + 20));
            ctx.Close();

            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 92, 92, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
