using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace FashionPOS.Data
{
    public class DatabaseContext
    {
        private readonly string _connectionString;

        public DatabaseContext()
        {
            // Store database at %LOCALAPPDATA%\FashionPOS\fashionpos.db
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbPath = Path.Combine(appDataPath, "FashionPOS");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }

            string dbFile = Path.Combine(dbPath, "fashionpos.db");
            _connectionString = $"Data Source={dbFile};";
        }

        /// <summary>
        /// Creates and opens a database connection with WAL mode and foreign key constraints enabled.
        /// </summary>
        /// <returns>An open SqliteConnection ready for queries.</returns>
        public SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Enable WAL mode for better concurrency
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
                command.ExecuteNonQuery();
            }

            return connection;
        }

        /// <summary>
        /// Gets the connection string for this database.
        /// </summary>
        public string ConnectionString => _connectionString;
    }
}
