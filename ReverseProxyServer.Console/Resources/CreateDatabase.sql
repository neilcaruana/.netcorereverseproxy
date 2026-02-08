PRAGMA journal_mode=WAL;

-- Create the Instances table
CREATE TABLE IF NOT EXISTS Instances (
    InstanceId TEXT PRIMARY KEY,
    Status TEXT NOT NULL,                    
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Version TEXT NOT NULL,
    RowId INTEGER NULL UNIQUE
);

-- Create the Connections table
CREATE TABLE IF NOT EXISTS Connections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    InstanceId TEXT NOT NULL,
    SessionId TEXT NOT NULL,
    ConnectionTime DATETIME NOT NULL,
    ProxyType TEXT NOT NULL,
    LocalAddress TEXT NOT NULL,
    LocalPort INTEGER NOT NULL,
    TargetHost TEXT NOT NULL,
    TargetPort INTEGER NOT NULL,
    RemoteAddress TEXT NOT NULL,
    RemotePort INTEGER NOT NULL,
    FOREIGN KEY (InstanceId) REFERENCES Instances(InstanceId)
);

-- Create the ConnectionsData table
CREATE TABLE IF NOT EXISTS ConnectionsData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL,
    CommunicationDirection TEXT NOT NULL,
    [Data] TEXT
);

-- Create the PortsHistory table
CREATE TABLE IF NOT EXISTS PortsHistory (
    Port INTEGER PRIMARY KEY,
    LastConnectionTime DATETIME NOT NULL,
    Hits INTEGER NOT NULL,
    RowId INTEGER NULL UNIQUE
);

-- Create the IPAddressHistory table
CREATE TABLE IF NOT EXISTS IPAddressHistory (
    IPAddress TEXT PRIMARY KEY,
    LastConnectionTime DATETIME NOT NULL,
    Hits INTEGER NOT NULL,
    IsBlacklisted BOOLEAN NOT NULL,
    RowId INTEGER NULL UNIQUE
);

-- Create the AbuseIPDB_CheckedIPS table
CREATE TABLE IF NOT EXISTS AbuseIPDB_CheckedIPS (
    IPAddress TEXT NOT NULL PRIMARY KEY,
    IsPublic BOOLEAN NOT NULL,
    IPVersion INTEGER NOT NULL,
    IsWhitelisted BOOLEAN,
    AbuseConfidence INTEGER NOT NULL,
    CountryCode TEXT NULL,
    CountryName TEXT NULL,
    UsageType TEXT NULL,
    ISP TEXT NULL,
    Domain TEXT NULL,
    Hostnames TEXT NOT NULL, 
    TotalReports INTEGER NOT NULL,
    DistinctUserCount INTEGER NOT NULL,
    LastReportedAt DATETIME NULL,
    LastCheckedAt DATETIME NULL,
    RowId INTEGER NULL UNIQUE
);

--Triggers
CREATE TRIGGER IF NOT EXISTS SetInstancesRowId
AFTER INSERT ON Instances
FOR EACH ROW
BEGIN
    UPDATE Instances
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM Instances)
    WHERE InstanceId = NEW.InstanceId;
END;

CREATE TRIGGER IF NOT EXISTS SetIPAddressHistoryRowId
AFTER INSERT ON IPAddressHistory
FOR EACH ROW
BEGIN
    UPDATE IPAddressHistory
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM IPAddressHistory)
    WHERE IPAddress = NEW.IPAddress;
END;

CREATE TRIGGER IF NOT EXISTS SetPortsHistoryRowId
AFTER INSERT ON PortsHistory
FOR EACH ROW
BEGIN
    UPDATE PortsHistory
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM PortsHistory)
    WHERE Port = NEW.Port;
END;

CREATE TRIGGER IF NOT EXISTS SetAbuseIPDB_CheckedIPSId_Insert
AFTER INSERT ON AbuseIPDB_CheckedIPS
FOR EACH ROW
BEGIN
    UPDATE AbuseIPDB_CheckedIPS
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM AbuseIPDB_CheckedIPS)
    WHERE IPAddress = NEW.IPAddress;
END;

CREATE TRIGGER SetAbuseIPDB_CheckedIPSId_Update
AFTER UPDATE ON AbuseIPDB_CheckedIPS
FOR EACH ROW
BEGIN
    UPDATE AbuseIPDB_CheckedIPS
    SET RowId = (SELECT IFNULL(MAX(RowId), 0) + 1 FROM AbuseIPDB_CheckedIPS)
    WHERE IPAddress = NEW.IPAddress;
END;

--Indexes
CREATE INDEX IF NOT EXISTS IX_Connections_RemoteAddress ON Connections (
    RemoteAddress
);

CREATE INDEX IF NOT EXISTS IX_Connections_InstanceID ON Connections (
    InstanceId
);

CREATE INDEX IF NOT EXISTS IX_Connections_ID ON Connections (
    Id
);

CREATE INDEX IF NOT EXISTS IX_Connections_SessionID ON Connections (
    SessionID
);

CREATE INDEX IF NOT EXISTS IX_Connections_ConnectionTime ON Connections (
    ConnectionTime
);

CREATE INDEX IF NOT EXISTS IX_Connections_ConnectionTime_Desc ON Connections (
    ConnectionTime DESC
);

CREATE INDEX IF NOT EXISTS IX_Connections_ProxyType ON Connections (
    ProxyType
);

CREATE INDEX IF NOT EXISTS IX_ConnectionData_SessionID ON ConnectionsData (
    SessionId
);