-- ============================================================================
-- Fdb Studio: SQLite Database Startup & Dialect Keywords Initialization Script
-- ============================================================================

-- [STEP 1/4] Core Schema & Tables
CREATE TABLE IF NOT EXISTS DialectKeywords (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Dialect TEXT NOT NULL,
    Keyword TEXT NOT NULL,
    Category TEXT NOT NULL DEFAULT 'Keyword',
    UNIQUE(Dialect, Keyword)
);

CREATE TABLE IF NOT EXISTS DialectSnippets (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Dialect TEXT NOT NULL,
    Keyword TEXT NOT NULL,
    Snippet TEXT NOT NULL,
    Description TEXT NOT NULL,
    UNIQUE(Dialect, Keyword)
);

CREATE TABLE IF NOT EXISTS Connections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Server TEXT NOT NULL,
    Port INTEGER NOT NULL,
    DatabaseName TEXT NOT NULL,
    AuthType TEXT NOT NULL,
    Username TEXT,
    Password TEXT DEFAULT '',
    DatabaseType TEXT NOT NULL DEFAULT 'MSSQL',
    LastConnectedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS QueryHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    QueryText TEXT NOT NULL,
    ExecutedAt TEXT NOT NULL,
    ExecutionTime TEXT NOT NULL,
    RowCount INTEGER NOT NULL,
    Status TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS SavedQueries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    QueryText TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS QueryResults (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RowIndex INTEGER NOT NULL,
    RecordId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Email TEXT NOT NULL,
    Role TEXT NOT NULL,
    Status TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('Theme', 'Light', datetime('now'));
INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('KeywordColor', 'Blue', datetime('now'));
INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('DatabaseInitialized', 'true', datetime('now'));

CREATE INDEX IF NOT EXISTS idx_dialect_keywords ON DialectKeywords(Dialect, Keyword);
CREATE INDEX IF NOT EXISTS idx_dialect_snippets ON DialectSnippets(Dialect, Keyword);

-- [STEP 2/4] MSSQL & ANSI Standard Keywords
INSERT OR IGNORE INTO DialectKeywords (Dialect, Keyword, Category) VALUES
('General', 'SELECT', 'Clause'),
('General', 'FROM', 'Clause'),
('General', 'WHERE', 'Clause'),
('General', 'INSERT', 'DML'),
('General', 'INTO', 'DML'),
('General', 'VALUES', 'DML'),
('General', 'UPDATE', 'DML'),
('General', 'SET', 'DML'),
('General', 'DELETE', 'DML'),
('General', 'CREATE', 'DDL'),
('General', 'ALTER', 'DDL'),
('General', 'DROP', 'DDL'),
('General', 'TRUNCATE', 'DDL'),
('General', 'TABLE', 'DDL'),
('General', 'VIEW', 'DDL'),
('General', 'INDEX', 'DDL'),
('General', 'DATABASE', 'DDL'),
('General', 'SCHEMA', 'DDL'),
('General', 'PROCEDURE', 'DDL'),
('General', 'FUNCTION', 'DDL'),
('General', 'TRIGGER', 'DDL'),
('General', 'JOIN', 'Join'),
('General', 'INNER', 'Join'),
('General', 'LEFT', 'Join'),
('General', 'RIGHT', 'Join'),
('General', 'FULL', 'Join'),
('General', 'CROSS', 'Join'),
('General', 'ON', 'Clause'),
('General', 'GROUP', 'Clause'),
('General', 'ORDER', 'Clause'),
('General', 'BY', 'Clause'),
('General', 'HAVING', 'Clause'),
('General', 'ASC', 'Modifier'),
('General', 'DESC', 'Modifier'),
('General', 'DISTINCT', 'Modifier'),
('General', 'UNION', 'Set'),
('General', 'ALL', 'Set'),
('General', 'EXCEPT', 'Set'),
('General', 'INTERSECT', 'Set'),
('General', 'AND', 'Operator'),
('General', 'OR', 'Operator'),
('General', 'NOT', 'Operator'),
('General', 'IN', 'Operator'),
('General', 'EXISTS', 'Operator'),
('General', 'BETWEEN', 'Operator'),
('General', 'LIKE', 'Operator'),
('General', 'IS', 'Operator'),
('General', 'NULL', 'Value'),
('General', 'AS', 'Alias'),
('General', 'CASE', 'Conditional'),
('General', 'WHEN', 'Conditional'),
('General', 'THEN', 'Conditional'),
('General', 'ELSE', 'Conditional'),
('General', 'END', 'Conditional'),
('General', 'BEGIN', 'Transaction'),
('General', 'COMMIT', 'Transaction'),
('General', 'ROLLBACK', 'Transaction'),
('General', 'TRANSACTION', 'Transaction'),
('General', 'PRIMARY', 'Constraint'),
('General', 'KEY', 'Constraint'),
('General', 'FOREIGN', 'Constraint'),
('General', 'REFERENCES', 'Constraint'),
('General', 'CONSTRAINT', 'Constraint'),
('General', 'UNIQUE', 'Constraint'),
('General', 'CHECK', 'Constraint'),
('General', 'DEFAULT', 'Constraint'),
('General', 'CAST', 'Function'),
('General', 'COUNT', 'Aggregate'),
('General', 'SUM', 'Aggregate'),
('General', 'AVG', 'Aggregate'),
('General', 'MIN', 'Aggregate'),
('General', 'MAX', 'Aggregate'),
('General', 'COALESCE', 'Function'),
('General', 'INT', 'Type'),
('General', 'BIGINT', 'Type'),
('General', 'SMALLINT', 'Type'),
('General', 'VARCHAR', 'Type'),
('General', 'CHAR', 'Type'),
('General', 'DATE', 'Type'),
('General', 'TIME', 'Type');

-- MSSQL Specific Keywords
INSERT OR IGNORE INTO DialectKeywords (Dialect, Keyword, Category) VALUES
('MSSQL', 'TOP', 'Clause'),
('MSSQL', 'IDENTITY', 'Modifier'),
('MSSQL', 'EXEC', 'Execution'),
('MSSQL', 'EXECUTE', 'Execution'),
('MSSQL', 'DECLARE', 'Variable'),
('MSSQL', 'CONVERT', 'Function'),
('MSSQL', 'ISNULL', 'Function'),
('MSSQL', 'NULLIF', 'Function'),
('MSSQL', 'ROW_NUMBER', 'Window'),
('MSSQL', 'OVER', 'Window'),
('MSSQL', 'PARTITION', 'Window'),
('MSSQL', 'GETDATE', 'Function'),
('MSSQL', 'SYSUTCDATETIME', 'Function'),
('MSSQL', 'DATEDIFF', 'Function'),
('MSSQL', 'DATEADD', 'Function'),
('MSSQL', 'TINYINT', 'Type'),
('MSSQL', 'BIT', 'Type'),
('MSSQL', 'DECIMAL', 'Type'),
('MSSQL', 'NUMERIC', 'Type'),
('MSSQL', 'FLOAT', 'Type'),
('MSSQL', 'REAL', 'Type'),
('MSSQL', 'NVARCHAR', 'Type'),
('MSSQL', 'NCHAR', 'Type'),
('MSSQL', 'DATETIME2', 'Type'),
('MSSQL', 'CROSS APPLY', 'Join'),
('MSSQL', 'OUTER APPLY', 'Join'),
('MSSQL', 'WAITFOR', 'Flow'),
('MSSQL', 'PRINT', 'Debug');

-- [STEP 3/4] PostgreSQL, MySQL & SQLite Dialect Keywords
-- PostgreSQL Keywords
INSERT OR IGNORE INTO DialectKeywords (Dialect, Keyword, Category) VALUES
('PostgreSQL', 'LIMIT', 'Clause'),
('PostgreSQL', 'OFFSET', 'Clause'),
('PostgreSQL', 'RETURNING', 'DML'),
('PostgreSQL', 'SERIAL', 'Type'),
('PostgreSQL', 'BIGSERIAL', 'Type'),
('PostgreSQL', 'JSONB', 'Type'),
('PostgreSQL', 'JSON', 'Type'),
('PostgreSQL', 'BOOLEAN', 'Type'),
('PostgreSQL', 'TIMESTAMPTZ', 'Type'),
('PostgreSQL', 'ILIKE', 'Operator'),
('PostgreSQL', 'SIMILAR TO', 'Operator'),
('PostgreSQL', 'NOW', 'Function'),
('PostgreSQL', 'GENERATE_SERIES', 'Function'),
('PostgreSQL', 'ARRAY', 'Type'),
('PostgreSQL', 'UUID', 'Type'),
('PostgreSQL', 'CONFLICT', 'Clause'),
('PostgreSQL', 'DO NOTHING', 'Clause'),
('PostgreSQL', 'DO UPDATE', 'Clause');

-- MySQL Keywords
INSERT OR IGNORE INTO DialectKeywords (Dialect, Keyword, Category) VALUES
('MySQL', 'LIMIT', 'Clause'),
('MySQL', 'OFFSET', 'Clause'),
('MySQL', 'AUTO_INCREMENT', 'Modifier'),
('MySQL', 'SHOW', 'Administrative'),
('MySQL', 'DESCRIBE', 'Administrative'),
('MySQL', 'EXPLAIN', 'Administrative'),
('MySQL', 'ENUM', 'Type'),
('MySQL', 'TINYINT', 'Type'),
('MySQL', 'MEDIUMINT', 'Type'),
('MySQL', 'LONGTEXT', 'Type'),
('MySQL', 'IFNULL', 'Function'),
('MySQL', 'NOW', 'Function'),
('MySQL', 'CONCAT', 'Function');

-- SQLite Keywords
INSERT OR IGNORE INTO DialectKeywords (Dialect, Keyword, Category) VALUES
('SQLite', 'AUTOINCREMENT', 'Modifier'),
('SQLite', 'LIMIT', 'Clause'),
('SQLite', 'OFFSET', 'Clause'),
('SQLite', 'PRAGMA', 'Administrative'),
('SQLite', 'GLOB', 'Operator'),
('SQLite', 'ROWID', 'Column'),
('SQLite', 'STRFTIME', 'Function'),
('SQLite', 'VACUUM', 'Administrative'),
('SQLite', 'ATTACH', 'Database'),
('SQLite', 'DETACH', 'Database');

-- Dialect Snippets
INSERT OR IGNORE INTO DialectSnippets (Dialect, Keyword, Snippet, Description) VALUES
('General', 'SELECT * FROM', 'SELECT * FROM ', 'Select all columns from a table'),
('General', 'INSERT INTO VALUES', 'INSERT INTO TableName (col1, col2)\nVALUES (''val1'', ''val2'');', 'Insert a new row'),
('General', 'UPDATE SET WHERE', 'UPDATE TableName\nSET col1 = ''val1''\nWHERE id = 1;', 'Update rows matching condition'),
('General', 'DELETE FROM WHERE', 'DELETE FROM TableName\nWHERE id = 1;', 'Delete rows matching condition'),
('General', 'INNER JOIN ON', 'INNER JOIN Table2 ON Table1.id = Table2.table1_id', 'Inner join on foreign key'),
('General', 'LEFT JOIN ON', 'LEFT JOIN Table2 ON Table1.id = Table2.table1_id', 'Left outer join on foreign key'),

('MSSQL', 'SELECT TOP 100', 'SELECT TOP 100 * FROM ', 'Select top 100 rows (MSSQL)'),
('MSSQL', 'CREATE TABLE', 'CREATE TABLE TableName (\n    id INT PRIMARY KEY IDENTITY(1,1),\n    name NVARCHAR(100) NOT NULL,\n    created_at DATETIME2 DEFAULT SYSUTCDATETIME()\n);', 'Create table schema with identity (MSSQL)'),

('PostgreSQL', 'SELECT LIMIT 100', 'SELECT * FROM TableName LIMIT 100;', 'Select first 100 rows (PostgreSQL)'),
('PostgreSQL', 'CREATE TABLE SERIAL', 'CREATE TABLE TableName (\n    id SERIAL PRIMARY KEY,\n    name VARCHAR(100) NOT NULL,\n    created_at TIMESTAMPTZ DEFAULT NOW()\n);', 'Create table with auto-increment serial (PostgreSQL)'),
('PostgreSQL', 'INSERT RETURNING', 'INSERT INTO TableName (col1) VALUES (''val1'') RETURNING id;', 'Insert and return generated ID (PostgreSQL)'),

('MySQL', 'SELECT LIMIT 100', 'SELECT * FROM TableName LIMIT 100;', 'Select first 100 rows (MySQL)'),
('MySQL', 'SHOW TABLES', 'SHOW TABLES;', 'List all tables in database (MySQL)'),

('SQLite', 'SELECT LIMIT 50', 'SELECT * FROM TableName LIMIT 50;', 'Select first 50 rows (SQLite)'),
('SQLite', 'PRAGMA TABLE INFO', 'PRAGMA table_info(TableName);', 'Inspect table columns metadata (SQLite)');

-- [STEP 4/4] Seed Initial App Data & Default Session
-- Connections table starts completely clean and empty for new users (no developer connections)

INSERT OR IGNORE INTO QueryHistory (Id, QueryText, ExecutedAt, ExecutionTime, RowCount, Status)
VALUES 
(1, 'SELECT user_id, username, email FROM dbo.Users WHERE is_active = 1 ORDER BY created_at DESC;', datetime('now', '-10 minutes'), '14 ms', 5, 'Success'),
(2, 'SELECT name, create_date, modify_date FROM sys.tables ORDER BY name ASC;', datetime('now', '-25 minutes'), '8 ms', 12, 'Success');

INSERT OR IGNORE INTO QueryResults (Id, RowIndex, RecordId, Name, Email, Role, Status, CreatedAt)
VALUES 
(1, 1, 101, 'Alice Johnson', 'alice@example.com', 'DBA', 'Active', '2026-01-15 08:30:00'),
(2, 2, 102, 'Bob Smith', 'bob@example.com', 'Developer', 'Active', '2026-02-10 11:15:22'),
(3, 3, 103, 'Charlie Davis', 'charlie@example.com', 'Analyst', 'Pending', '2026-03-01 14:45:00'),
(4, 4, 104, 'Dana White', 'dana@example.com', 'Admin', 'Active', '2026-03-12 09:20:11'),
(5, 5, 105, 'Evan Wright', 'evan@example.com', 'Developer', 'Inactive', '2026-04-05 16:00:45');
