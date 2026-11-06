-- AgriStream IoT - Azure SQL Database schema (user management)
-- OPTIONAL: the web app creates these tables automatically on first start.
-- Run this script (Azure Portal > SQL database > Query editor) only if you prefer to create them yourself.
-- The table/column names match the Entity Framework model exactly.

IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND OBJECT_ID('dbo.Roles', 'U') IS NOT NULL
    PRINT 'Tables already exist.';
ELSE
BEGIN
    CREATE TABLE dbo.Roles (
        Id    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Name  NVARCHAR(50)      NOT NULL
    );
    CREATE UNIQUE INDEX IX_Roles_Name ON dbo.Roles (Name);

    CREATE TABLE dbo.Users (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username     NVARCHAR(100)     NOT NULL,
        PasswordHash NVARCHAR(500)     NOT NULL,
        RoleId       INT               NOT NULL,
        CreatedUtc   DATETIME2         NOT NULL CONSTRAINT DF_Users_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
    );
    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username);
END
GO

-- Roles (the app also seeds these at startup if missing)
INSERT INTO dbo.Roles (Name)
SELECT v.Name FROM (VALUES ('Admin'), ('Farmer'), ('Technician')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Name = v.Name);
GO

-- NOTE: the first admin user (username: admin) is created by the web app at startup
-- using the Seed:AdminPassword setting, because passwords are hashed in application code (PBKDF2).

-- Useful checks
-- SELECT u.Id, u.Username, r.Name AS Role, u.CreatedUtc FROM dbo.Users u JOIN dbo.Roles r ON r.Id = u.RoleId;
