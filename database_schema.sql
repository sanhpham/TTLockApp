-- Script tạo cấu trúc Database cho SQLite (TTLock Manager)

CREATE TABLE "Customers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Phone" TEXT,
    "Room" TEXT,
    "QuotaPerMonth" INTEGER NOT NULL DEFAULT 10,
    "Status" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE "CustomerCredentials" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerCredentials" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "Type" INTEGER NOT NULL,
    "CredentialId" TEXT NOT NULL,
    "Label" TEXT,
    "IsActiveOnLock" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_CustomerCredentials_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MonthlyUsages" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MonthlyUsages" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "Month" INTEGER NOT NULL,
    "Year" INTEGER NOT NULL,
    "Quota" INTEGER NOT NULL,
    "UsedCount" INTEGER NOT NULL,
    CONSTRAINT "FK_MonthlyUsages_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "WashRecords" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WashRecords" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "RecordType" INTEGER NOT NULL,
    "KeyboardPwd" TEXT,
    "OpenedAt" TEXT NOT NULL,
    "Month" INTEGER NOT NULL,
    "Year" INTEGER NOT NULL,
    "TTLockRecordId" INTEGER NOT NULL,
    CONSTRAINT "FK_WashRecords_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

-- Tạo Index để tăng tốc độ truy vấn và tránh trùng lặp
CREATE INDEX "IX_CustomerCredentials_CredentialId" ON "CustomerCredentials" ("CredentialId");
CREATE INDEX "IX_CustomerCredentials_CustomerId" ON "CustomerCredentials" ("CustomerId");
CREATE UNIQUE INDEX "IX_MonthlyUsages_CustomerId_Month_Year" ON "MonthlyUsages" ("CustomerId", "Month", "Year");
CREATE INDEX "IX_WashRecords_CustomerId" ON "WashRecords" ("CustomerId");
CREATE UNIQUE INDEX "IX_WashRecords_TTLockRecordId" ON "WashRecords" ("TTLockRecordId");
