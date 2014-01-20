using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Xml.Serialization;
using System.IO;


namespace BigData {
    public class Publication {
        public enum SearchField { ISBN, Title, Desc };

        /// <summary>
        /// Gets a publication object from XML returned by OCLC
        /// </summary>
        /// <param name="doc">XDocument response from OCLC Search API</param>
        /// <returns>A Publication object</returns>
        public static Publication FromXML(XDocument doc) {
            var pub = new Publication();
            pub.Title = GetOCLCFieldByTag(titleTag, doc);
            pub.Description = GetOCLCFieldByTag(descTag, doc);
            pub.ISBNs = GetOCLCFieldsByTag(isbnTag, doc);

            var firstAuthor = GetOCLCFieldByTag(authorTag, doc);
            var allAuthors = GetOCLCFieldsByTag(authorsTag, doc);
            allAuthors.Insert(0, firstAuthor);
            pub.Authors = allAuthors;


            return pub;
        }

        /// <summary>
        /// Returns a string representing a Publication object
        /// </summary>
        /// <returns>A Publication object as a string</returns>
        public override string ToString() {
            return String.Format("BigData.Publication<Title: {0}, ISBN: {1}>", this.Title, this.ISBNs.First());
        }

        /// <summary>
        /// The title of the Publication
        /// </summary>
        public string Title {
            get { return title; }
            set {
                var ti = new System.Globalization.CultureInfo("en-US").TextInfo;
                title = ti.ToTitleCase(value);
            }
        }

        /// <summary>
        /// The OCLC number of the Publication
        /// </summary>
        public string OCLCNumber { get; set; }

        /// <summary>
        /// The description of the Publication
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The authors of the Publication
        /// </summary>
        public List<string> Authors { get; set; }

        /// <summary>
        /// The URI of the cover image for the Publication
        /// </summary>
        public string CoverImageURI { get; set; }

        /// <summary>
        /// A BitmapImage of the cover of the Publication
        /// </summary>
        public BitmapSource CoverImage { get; set; }

        /// <summary>
        /// The ISBNs associated with the Publication
        /// </summary>
        public List<string> ISBNs {
            get { return isbns; }
            set {
                isbns = (from isbn in value
                         where isbn != null
                         select isbn.Split(new char[] { ' ' }, 2).First())
                         .ToList();
            }
        }

        /// <summary>
        /// Parses XDocument for a value represented by a specific tag
        /// </summary>
        /// <param name="tag">The tag to parse for</param>
        /// <param name="doc">The XDocument to parse</param>
        /// <returns>The data pointed to by the tag as a string</returns>
        static string GetOCLCFieldByTag(string tag, XDocument doc) {
            XNamespace ns = @"http://www.loc.gov/MARC21/slim";
            try {
                return (from datafield in doc.Descendants(ns + "datafield")
                        where datafield.Attribute("tag").Value.Equals(tag)
                        select datafield.Descendants().First().Value)
                        .First();
            } catch (InvalidOperationException) {
                return null;
            }
        }

        /// <summary>
        /// Parses XDocument for multiple values represented by a specific tag
        /// </summary>
        /// <param name="tag">The tag to parse for</param>
        /// <param name="doc">The XDocument to parse</param>
        /// <returns>A list of strings representing the fields pointed to by the tag</returns>
        static List<string> GetOCLCFieldsByTag(string tag, XDocument doc) {
            XNamespace ns = @"http://www.loc.gov/MARC21/slim";
            return (from datafield in doc.Descendants(ns + "datafield")
                    where datafield.Attribute("tag").Value.Equals(tag)
                    select datafield.Descendants().First().Value)
                    .ToList();
        }

        /// <summary>
        /// List of the isbns associated with the Publication
        /// </summary>
        List<string> isbns;

        /// <summary>
        /// The title of the Publication
        /// </summary>
        string title;

        static string formTag = "655";
        static string authorTag = "100";
        static string authorsTag = "700";
        static string isbnTag = "020";
        static string descTag = "520";
        static string contentsTag = "505";
        static string titleTag = "245";
    }
}
