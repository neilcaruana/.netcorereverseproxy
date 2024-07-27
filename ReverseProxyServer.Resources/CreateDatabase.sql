-- Create the Instances table
CREATE TABLE IF NOT EXISTS Instances (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    InstanceId UNIQUE,
    Status TEXT NOT NULL,                    
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Version TEXT NOT NULL
);

-- Create the Connections table
CREATE TABLE IF NOT EXISTS Connections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId UNIQUE NOT NULL,
    InstanceId TEXT NOT NULL,
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
    Data TEXT
);

-- Create the IPAddressHistory table
CREATE TABLE IF NOT EXISTS IPAddressHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LastConnectionTime DATETIME NOT NULL,
    IP TEXT NOT NULL,
    Hits INTEGER NOT NULL
);

-- Create the PortsHistory table
CREATE TABLE IF NOT EXISTS PortsHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LastConnectionTime DATETIME NOT NULL,
    Port INTEGER NOT NULL,
    Hits INTEGER NOT NULL
);