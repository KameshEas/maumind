using Microsoft.Data.Sqlite;
using MauMind.App.Models;

namespace MauMind.App.Data;

public class DatabaseService
{
    private readonly string _connectionString;
    
    public DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind");
        
        Directory.CreateDirectory(appDataPath);
        
        var dbPath = Path.Combine(appDataPath, "maumind.db");
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Documents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                SourceType TEXT NOT NULL,
                FilePath TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                Summary TEXT,
                SummarizedAt DATETIME
            );

            -- Add summary columns if they don't exist (migration)
            INSERT OR IGNORE INTO Documents (Id, Title, Content, SourceType, CreatedAt, UpdatedAt)
            SELECT 0, 'migration_dummy', '', 'Note', datetime('now'), datetime('now') WHERE NOT EXISTS (SELECT 1 FROM pragma_table_info('Documents') WHERE name='Summary');
        ";
        command.ExecuteNonQuery();

        // Add columns if they don't exist
        try
        {
            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Documents ADD COLUMN Summary TEXT";
            alterCmd.ExecuteNonQuery();
        }
        catch { /* Column already exists */ }

        try
        {
            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Documents ADD COLUMN SummarizedAt DATETIME";
            alterCmd.ExecuteNonQuery();
        }
        catch { /* Column already exists */ }

        // Remove migration dummy if added
        var cleanupCmd = connection.CreateCommand();
        cleanupCmd.CommandText = "DELETE FROM Documents WHERE Title = 'migration_dummy'";
        cleanupCmd.ExecuteNonQuery();

        command = connection.CreateCommand();
        command.CommandText = @"
            
            CREATE TABLE IF NOT EXISTS VectorEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                ChunkText TEXT NOT NULL,
                ChunkIndex INTEGER NOT NULL,
                Embedding BLOB NOT NULL,
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_vector_document ON VectorEntries(DocumentId);
            
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL,
                IsUser INTEGER NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Color TEXT,
                Icon TEXT,
                ParentFolderId INTEGER,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (ParentFolderId) REFERENCES Folders(Id) ON DELETE SET NULL
            );
        ";
        command.ExecuteNonQuery();

        // Add FolderId column to Documents if not exists
        try
        {
            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Documents ADD COLUMN FolderId INTEGER REFERENCES Folders(Id)";
            alterCmd.ExecuteNonQuery();
        }
        catch { /* Column already exists */ }
    }
    
    // Document Operations
    public async Task<int> InsertDocumentAsync(Document document)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Documents (Title, Content, SourceType, FilePath, CreatedAt, UpdatedAt)
            VALUES (@title, @content, @sourceType, @filePath, @createdAt, @updatedAt);
            SELECT last_insert_rowid();
        ";
        
        command.Parameters.AddWithValue("@title", document.Title);
        command.Parameters.AddWithValue("@content", document.Content);
        command.Parameters.AddWithValue("@sourceType", document.SourceType.ToString());
        command.Parameters.AddWithValue("@filePath", document.FilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", document.CreatedAt);
        command.Parameters.AddWithValue("@updatedAt", document.UpdatedAt);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    
    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        var documents = new List<Document>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Documents ORDER BY UpdatedAt DESC";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            documents.Add(new Document
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                SourceType = Enum.Parse<DocumentSourceType>(reader.GetString(3)),
                FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6),
                Summary = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null,
                SummarizedAt = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetDateTime(8) : null
            });
        }

        return documents;
    }
    
    public async Task<Document?> GetDocumentByIdAsync(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Documents WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Document
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                SourceType = Enum.Parse<DocumentSourceType>(reader.GetString(3)),
                FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6),
                Summary = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null,
                SummarizedAt = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetDateTime(8) : null
            };
        }

        return null;
    }

    public async Task DeleteDocumentAsync(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Documents WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }

    // Summary Operations
    public async Task UpdateDocumentSummaryAsync(int documentId, string summary, DateTime summarizedAt)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Documents 
            SET Summary = @summary, SummarizedAt = @summarizedAt
            WHERE Id = @documentId
        ";

        command.Parameters.AddWithValue("@summary", summary);
        command.Parameters.AddWithValue("@summarizedAt", summarizedAt);
        command.Parameters.AddWithValue("@documentId", documentId);

        await command.ExecuteNonQueryAsync();
    }
    
    // Vector Entry Operations
    public async Task<int> InsertVectorEntryAsync(VectorEntry entry)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO VectorEntries (DocumentId, ChunkText, ChunkIndex, Embedding)
            VALUES (@documentId, @chunkText, @chunkIndex, @embedding);
            SELECT last_insert_rowid();
        ";
        
        command.Parameters.AddWithValue("@documentId", entry.DocumentId);
        command.Parameters.AddWithValue("@chunkText", entry.ChunkText);
        command.Parameters.AddWithValue("@chunkIndex", entry.ChunkIndex);
        command.Parameters.AddWithValue("@embedding", entry.Embedding);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    
    public async Task<List<VectorEntry>> GetAllVectorEntriesAsync()
    {
        var entries = new List<VectorEntry>();
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT v.Id, v.DocumentId, v.ChunkText, v.ChunkIndex, v.Embedding,
                   d.Id, d.Title, d.Content, d.SourceType, d.FilePath, d.CreatedAt, d.UpdatedAt
            FROM VectorEntries v
            INNER JOIN Documents d ON v.DocumentId = d.Id
        ";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new VectorEntry
            {
                Id = reader.GetInt32(0),
                DocumentId = reader.GetInt32(1),
                ChunkText = reader.GetString(2),
                ChunkIndex = reader.GetInt32(3),
                Embedding = (byte[])reader[4],
                Document = new Document
                {
                    Id = reader.GetInt32(5),
                    Title = reader.GetString(6),
                    Content = reader.GetString(7),
                    SourceType = Enum.Parse<DocumentSourceType>(reader.GetString(8)),
                    FilePath = reader.IsDBNull(9) ? null : reader.GetString(9),
                    CreatedAt = reader.GetDateTime(10),
                    UpdatedAt = reader.GetDateTime(11)
                }
            });
        }
        
        return entries;
    }
    
    public async Task DeleteVectorEntriesByDocumentIdAsync(int documentId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VectorEntries WHERE DocumentId = @documentId";
        command.Parameters.AddWithValue("@documentId", documentId);
        
        await command.ExecuteNonQueryAsync();
    }
    
    // Chat Message Operations
    public async Task<int> InsertChatMessageAsync(ChatMessage message)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ChatMessages (Content, IsUser, Timestamp)
            VALUES (@content, @isUser, @timestamp);
            SELECT last_insert_rowid();
        ";
        
        command.Parameters.AddWithValue("@content", message.Content);
        command.Parameters.AddWithValue("@isUser", message.IsUser ? 1 : 0);
        command.Parameters.AddWithValue("@timestamp", message.Timestamp);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    
    public async Task<List<ChatMessage>> GetChatMessagesAsync(int limit = 50)
    {
        var messages = new List<ChatMessage>();
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ChatMessages ORDER BY Timestamp DESC LIMIT @limit";
        command.Parameters.AddWithValue("@limit", limit);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt32(0),
                Content = reader.GetString(1),
                IsUser = reader.GetInt32(2) == 1,
                Timestamp = reader.GetDateTime(3)
            });
        }
        
        messages.Reverse();
        return messages;
    }
    
    public async Task ClearChatMessagesAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ChatMessages";

        await command.ExecuteNonQueryAsync();
    }

    // ─── Folder Operations ─────────────────────────────────────────────────────────

    public async Task<int> InsertFolderAsync(Folder folder)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Folders (Name, Color, Icon, ParentFolderId, CreatedAt, UpdatedAt)
            VALUES (@name, @color, @icon, @parentFolderId, @createdAt, @updatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@name", folder.Name);
        command.Parameters.AddWithValue("@color", folder.Color ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@icon", folder.Icon ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@parentFolderId", folder.ParentFolderId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", folder.CreatedAt);
        command.Parameters.AddWithValue("@updatedAt", folder.UpdatedAt);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<Folder>> GetAllFoldersAsync()
    {
        var folders = new List<Folder>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Folders ORDER BY Name";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            folders.Add(new Folder
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                ParentFolderId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            });
        }

        return folders;
    }

    public async Task<Folder?> GetFolderByIdAsync(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Folders WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Folder
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                ParentFolderId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            };
        }

        return null;
    }

    public async Task UpdateFolderAsync(Folder folder)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Folders 
            SET Name = @name, Color = @color, Icon = @icon, ParentFolderId = @parentFolderId, UpdatedAt = @updatedAt
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@name", folder.Name);
        command.Parameters.AddWithValue("@color", folder.Color ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@icon", folder.Icon ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@parentFolderId", folder.ParentFolderId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@updatedAt", folder.UpdatedAt);
        command.Parameters.AddWithValue("@id", folder.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteFolderAsync(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Folders WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }

    // ─── Document-Folder Operations ───────────────────────────────────────────────

    public async Task UpdateDocumentFolderAsync(int documentId, int? folderId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Documents 
            SET FolderId = @folderId
            WHERE Id = @documentId
        ";

        command.Parameters.AddWithValue("@folderId", folderId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@documentId", documentId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Document>> GetDocumentsByFolderAsync(int? folderId)
    {
        var documents = new List<Document>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        if (folderId.HasValue)
        {
            command.CommandText = "SELECT * FROM Documents WHERE FolderId = @folderId ORDER BY UpdatedAt DESC";
            command.Parameters.AddWithValue("@folderId", folderId.Value);
        }
        else
        {
            command.CommandText = "SELECT * FROM Documents WHERE FolderId IS NULL ORDER BY UpdatedAt DESC";
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            documents.Add(new Document
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                SourceType = Enum.Parse<DocumentSourceType>(reader.GetString(3)),
                FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6),
                Summary = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null,
                SummarizedAt = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetDateTime(8) : null,
                FolderId = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetInt32(9) : null
            });
        }

        return documents;
    }
}
