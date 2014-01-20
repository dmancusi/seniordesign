using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Data.Common;

namespace BigData.OCLC {
    /// <summary>
    /// Manages access to the database.
    /// </summary>
    public class Database : PublicationSource, IDisposable {

        /// <summary>
        /// Create a database instance.
        /// </summary>
        /// <param name="key">The WSKey required to access the database.</param>
        /// <param name="feed">The rss feed that will be passed to the OCLC client.</param>
        public Database() {
            var source = String.Format(
                "Data Source = {0}; Version = 3; New = false; Compress = true",
                GetDatabasePath());
            connection = new SQLiteConnection(source);
            connection.Open();
        }

        /// <summary>
        /// Pulls all information from all publications from the database.
        /// </summary>
        /// <returns>An array of the complete list of publications.</returns>
        public async Task<IEnumerable<Publication>> GetPublications() {
            var path = GetDatabasePath();

            if (!File.Exists(path)) {
                CreateDatabase();
                await UpdateDatabase();
            }

            string query = "SELECT * FROM Publications;";
            DbDataReader reader = ExecuteSQLiteQuery(query);
            var pubs = getPublicationsFromReader(reader);
            return pubs;
        }

        /// <summary>
        /// Closes the connection to the SQLite database.
        /// </summary>
        public void Dispose() {
            if (connection.State == System.Data.ConnectionState.Open) {
                connection.Close();
            }
        }

        /// <summary>
        /// Gets publication data from the OCLC client and stores it into the database.
        /// </summary>
        /// <returns>The number of publications entered as an unsigned int.</returns>
        public async Task<uint> UpdateDatabase() {
            // First remove entries from current table
            string deleteQuery = "DELETE FROM Publications;";
            ExecuteSQLiteCommand(deleteQuery);
            deleteQuery = "DELETE FROM Authors;";
            ExecuteSQLiteCommand(deleteQuery);

            // Now insert new entries
            Client oclc = new Client();
            var pubList = await oclc.GetPublications();

            string InsertQuery;
            uint count = 0;

            foreach (var pub in pubList) {
                // Insert publication
                InsertQuery = "INSERT INTO Publications VALUES (" +
                               "(@id), " +
                               "(@isbn), " +
                               "(@title), " +
                               "(@oclc), " +
                               "(@desc), " +
                               "(@cover)" +
                               ");";

                // Adding parameters
                var command = new SQLiteCommand(InsertQuery, connection);
                command.Parameters.Add(new SQLiteParameter("@id", count));
                command.Parameters.Add(new SQLiteParameter("@isbn", pub.ISBNs[0]));
                command.Parameters.Add(new SQLiteParameter("@title", pub.Title));
                command.Parameters.Add(new SQLiteParameter("@oclc", pub.OCLCNumber));
                command.Parameters.Add(new SQLiteParameter("@desc", pub.Description));

                // Adding cover to query
                byte[] cover = BitmapToByteArray(pub.CoverImage);
                command.Parameters.Add(new SQLiteParameter("@cover", cover));
                command.ExecuteNonQuery();

                // Insert authors
                for (int j = 0; j < pub.Authors.Count; j++) {
                    string authorQuery = "INSERT INTO Authors VALUES (" +
                                         "(@id), " +
                                         "(@author));";

                    // Author parameter
                    command = new SQLiteCommand(authorQuery, connection);
                    command.Parameters.Add(new SQLiteParameter("@id", count));
                    command.Parameters.Add(new SQLiteParameter("@author", pub.Authors[j]));
                    command.ExecuteNonQuery();
                }

                count++;
            }

            Console.WriteLine("Database updated");
            return count;
        }

        SQLiteConnection connection;

        /// <summary>
        /// Returns the location of the database file in the file system.
        /// </summary>
        /// <returns>The path to the database file as a string.</returns>
        string GetDatabasePath() {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BigData");

            if (!Directory.Exists(appData)) {
                Directory.CreateDirectory(appData);
            }

            return Path.Combine(appData, "publications.db");
        }

        /// <summary>
        /// Creates the Publication and Author tables.
        /// </summary>
        void CreateDatabase() {
            // Otherwise do things
            string PubTable = "CREATE TABLE Publications(" +
                              "id INT, " +
                              "isbn TEXT, " +
                              "title TEXT, " +
                              "oclc TEXT, " +
                              "desc TEXT, " +
                              "cover BLOB" +
                              ")";
            string AuthorTable = "CREATE TABLE Authors(" +
                                 "id INT, " +
                                 "author TEXT" +
                                 ")";
            ExecuteSQLiteCommand(PubTable);
            ExecuteSQLiteCommand(AuthorTable);
        }

        /// <summary>
        /// Executes a non-query type SQLite command.
        /// </summary>
        /// <param name="cmd">The SQLite command as a string.</param>
        void ExecuteSQLiteCommand(string cmd) {
            var command = new SQLiteCommand(cmd, connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a SQLite query.
        /// </summary>
        /// <param name="query">The Sqlite query as a string</param>
        /// <returns>A SQLiteDataReader with the results from the query.</returns>
        SQLiteDataReader ExecuteSQLiteQuery(string query) {
            var command = new SQLiteCommand(query, connection);
            return command.ExecuteReader();
        }

        /// <summary>
        /// Converts a BitmapImage into an array of bytes.
        /// </summary>
        /// <param name="img">The image to be converted.</param>
        /// <returns>The resulting byte array.</returns>
        static byte[] BitmapToByteArray(BitmapSource img) {
            try {
                MemoryStream ms = new MemoryStream();
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(img));
                encoder.Save(ms);
                return ms.ToArray();
            } catch (NullReferenceException) {
                Console.WriteLine("No Image Found");
                return new byte[0];
            }
        }

        /// <summary>
        /// Parses a Sqlite data reader for the publication information and 
        /// creates a list of publications from it.
        /// </summary>
        /// <param name="reader">The SQLiteDataReader to be parsed</param>
        /// <returns>A list of publications</returns>
        IEnumerable<Publication> getPublicationsFromReader(DbDataReader reader) {
            while (reader.Read()) {
                Publication pub = new Publication();
                pub.Title = (string)reader["title"];
                pub.OCLCNumber = (string)reader["oclc"];
                pub.ISBNs = new List<string>();
                pub.ISBNs.Add((string)reader["isbn"]);
                if (reader["desc"].GetType() != typeof(DBNull)) {
                    pub.Description = (string)reader["desc"];
                }
                var id = reader["id"];

                // Get the cover
                MemoryStream ms = new MemoryStream((byte[])reader["cover"]);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                pub.CoverImage = image;

                // Get the authors
                string query = "SELECT author FROM Authors WHERE id = " + id + ";";
                DbDataReader authorReader = ExecuteSQLiteQuery(query);
                pub.Authors = new List<string>();
                while (authorReader.Read()) {
                    if (authorReader["author"].GetType() != typeof(DBNull))
                        pub.Authors.Add((string)authorReader["author"]);
                }

                yield return pub;
            }
        }
    }
}
