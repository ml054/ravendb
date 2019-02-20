using System;

namespace Raven.Client.Documents.Smuggler
{
    [Flags]
    public enum DatabaseItemType
    {
        None = 0,

        Documents = 1 << 0,
        RevisionDocuments = 1 << 1,
        Indexes = 1 << 2,
        Identities = 1 << 4,
        Tombstones = 1 << 5,
        LegacyAttachments = 1 << 6,
        Conflicts = 1 << 7,
        CompareExchange = 1 << 8,
        LegacyDocumentDeletions = 1 << 9,
        LegacyAttachmentDeletions = 1 << 10,
        DatabaseRecord = 1 << 11,
        Unknown = 1 << 12,

        [Obsolete("DatabaseItemType.Counters is not supported anymore. Will be removed in next major version of the product.")]
        Counters = 1 << 13,

        Attachments = 1 << 14,
        CounterGroups = 1 << 15

    }
}
