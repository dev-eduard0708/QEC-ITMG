using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2_01_03_AddMissingNumberSequenceAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "plt");

            migrationBuilder.Sql(@"
IF OBJECT_ID('[plt].[NumberSequence]', 'U') IS NULL
BEGIN
    CREATE TABLE [plt].[NumberSequence](
        [SequenceKey] nvarchar(64) NOT NULL,
        [Year] int NOT NULL,
        [NextValue] bigint NOT NULL,
        CONSTRAINT [PK_NumberSequence] PRIMARY KEY ([SequenceKey], [Year])
    );
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID('[plt].[AttachmentMetadata]', 'U') IS NULL
BEGIN
    CREATE TABLE [plt].[AttachmentMetadata](
        [Id] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(256) NOT NULL,
        [StorageKey] nvarchar(128) NOT NULL,
        [ContentType] nvarchar(128) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [UploadedAtUtc] datetimeoffset NOT NULL,
        [ScanStatus] int NOT NULL,
        [ScanProvider] nvarchar(64) NULL,
        [ScanMessage] nvarchar(2048) NULL,
        [ScannedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AttachmentMetadata] PRIMARY KEY ([Id])
    );
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
