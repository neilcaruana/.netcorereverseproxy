-- Create the Instances table
CREATE TABLE IF NOT EXISTS Instances (
    InstanceId TEXT PRIMARY KEY,
    Status TEXT NOT NULL,                    
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Version TEXT NOT NULL,
    RowId INTEGER NULL UNIQUE
);

CREATE TRIGGER IF NOT EXISTS SetInstancesRowId
AFTER INSERT ON Instances
FOR EACH ROW
BEGIN
    UPDATE Instances
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM Instances)
    WHERE InstanceId = NEW.InstanceId;
END;

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

-- Create the IPAddressHistory table
CREATE TABLE IF NOT EXISTS IPAddressHistory (
    IP TEXT PRIMARY KEY,
    LastConnectionTime DATETIME NOT NULL,
    Hits INTEGER NOT NULL,
    RowId INTEGER NULL UNIQUE
);

CREATE TRIGGER IF NOT EXISTS SetIPAddressHistoryRowId
AFTER INSERT ON IPAddressHistory
FOR EACH ROW
BEGIN
    UPDATE IPAddressHistory
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM IPAddressHistory)
    WHERE IP = NEW.IP;
END;

-- Create the PortsHistory table
CREATE TABLE IF NOT EXISTS PortsHistory (
    Port INTEGER PRIMARY KEY,
    LastConnectionTime DATETIME NOT NULL,
    Hits INTEGER NOT NULL,
    RowId INTEGER NULL UNIQUE
);

CREATE TRIGGER IF NOT EXISTS SetPortsHistoryRowId
AFTER INSERT ON PortsHistory
FOR EACH ROW
BEGIN
    UPDATE PortsHistory
    SET RowId = (SELECT IFNULL(MAX(RowId),0) + 1 FROM PortsHistory)
    WHERE Port = NEW.Port;
END;


