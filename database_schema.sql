-- Script tạo cấu trúc Database cho SQLite (TTLock Manager) - Cập nhật mới nhất từ EF Core DbContext

CREATE TABLE "AppUsers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AppUsers" PRIMARY KEY AUTOINCREMENT,
    "TTLockUsername" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "AccessToken" TEXT NOT NULL,
    "RefreshToken" TEXT NOT NULL,
    "TokenExpiresAt" TEXT NOT NULL,
    "LastLoginAt" TEXT NOT NULL
);

CREATE TABLE "Customers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Phone" TEXT NOT NULL,
    "Room" TEXT NOT NULL,
    "QuotaPerMonth" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "AppUserId" INTEGER NOT NULL,
    "LockId" INTEGER NULL,
    CONSTRAINT "FK_Customers_AppUsers_AppUserId" FOREIGN KEY ("AppUserId") REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "DeviceLocks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DeviceLocks" PRIMARY KEY AUTOINCREMENT,
    "LockId" INTEGER NOT NULL,
    "LockName" TEXT NOT NULL,
    "LockAlias" TEXT NOT NULL,
    "AppUserId" INTEGER NOT NULL,
    "SyncedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DeviceLocks_AppUsers_AppUserId" FOREIGN KEY ("AppUserId") REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "CustomerCredentials" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerCredentials" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "Type" INTEGER NOT NULL,
    "CredentialId" TEXT NOT NULL,
    "Label" TEXT NOT NULL,
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
    "KeyboardPwd" TEXT NOT NULL,
    "OpenedAt" TEXT NOT NULL,
    "Month" INTEGER NOT NULL,
    "Year" INTEGER NOT NULL,
    "TTLockRecordId" INTEGER NOT NULL,
    CONSTRAINT "FK_WashRecords_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_CustomerCredentials_CredentialId" ON "CustomerCredentials" ("CredentialId");
CREATE INDEX "IX_CustomerCredentials_CustomerId" ON "CustomerCredentials" ("CustomerId");
CREATE INDEX "IX_Customers_AppUserId" ON "Customers" ("AppUserId");
CREATE INDEX "IX_DeviceLocks_AppUserId" ON "DeviceLocks" ("AppUserId");
CREATE UNIQUE INDEX "IX_MonthlyUsages_CustomerId_Month_Year" ON "MonthlyUsages" ("CustomerId", "Month", "Year");
CREATE INDEX "IX_WashRecords_CustomerId" ON "WashRecords" ("CustomerId");
CREATE UNIQUE INDEX "IX_WashRecords_TTLockRecordId" ON "WashRecords" ("TTLockRecordId");
