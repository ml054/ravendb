﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Exceptions;
using Raven.Client.Documents.Operations;
using Raven.Server.Documents.Replication;
using Raven.Client.Documents.Replication.Messages;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Voron;
using Voron.Data.Tables;
using Voron.Impl;
using Sparrow;
using Sparrow.Binary;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Logging;
using Sparrow.Utils;
using ConcurrencyException = Voron.Exceptions.ConcurrencyException;

namespace Raven.Server.Documents
{
    public unsafe class AttachmentsStorage
    {
        private readonly DocumentDatabase _documentDatabase;
        private readonly DocumentsStorage _documentsStorage;
        private readonly Logger _logger;

        private static readonly Slice AttachmentsSlice;
        private static readonly Slice AttachmentsMetadataSlice;
        public static readonly Slice AttachmentsEtagSlice;
        private static readonly Slice AttachmentsHashSlice;
        private static readonly Slice AttachmentsTombstonesSlice;

        private static readonly TableSchema AttachmentsSchema = new TableSchema();
        public static readonly string AttachmentsTombstones = "Attachments.Tombstones";

        
        private enum AttachmentsTable
        {
            /* AND is a record separator. 
             * We are you using the record separator in order to avoid loading another files that has the same key prefix, 
                e.g. fitz(record-separator)profile.png and fitz0(record-separator)profile.png, without the record separator we would have to load also fitz0 and filter it. */
            LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType = 0,
            Etag = 1,
            Name = 2, // format of lazy string key is detailed in GetLowerIdSliceAndStorageKey
            ContentType = 3, // format of lazy string key is detailed in GetLowerIdSliceAndStorageKey
            Hash = 4, // base64 hash
            TransactionMarker = 5,
        }

        static AttachmentsStorage()
        {
            Slice.From(StorageEnvironment.LabelsContext, "Attachments", ByteStringType.Immutable, out AttachmentsSlice);
            Slice.From(StorageEnvironment.LabelsContext, "AttachmentsMetadata", ByteStringType.Immutable, out AttachmentsMetadataSlice);
            Slice.From(StorageEnvironment.LabelsContext, "AttachmentsEtag", ByteStringType.Immutable, out AttachmentsEtagSlice);
            Slice.From(StorageEnvironment.LabelsContext, "AttachmentsHash", ByteStringType.Immutable, out AttachmentsHashSlice);
            Slice.From(StorageEnvironment.LabelsContext, AttachmentsTombstones, ByteStringType.Immutable, out AttachmentsTombstonesSlice);

            AttachmentsSchema.DefineKey(new TableSchema.SchemaIndexDef
            {
                StartIndex = (int)AttachmentsTable.LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType,
                Count = 1,
            });
            AttachmentsSchema.DefineFixedSizeIndex(new TableSchema.FixedSizeSchemaIndexDef
            {
                StartIndex = (int)AttachmentsTable.Etag,
                Name = AttachmentsEtagSlice
            });
            AttachmentsSchema.DefineIndex(new TableSchema.SchemaIndexDef
            {
                StartIndex = (int)AttachmentsTable.Hash,
                Count = 1,
                Name = AttachmentsHashSlice
            });
        }

        public AttachmentsStorage(DocumentDatabase documentDatabase, Transaction tx)
        {
            _documentDatabase = documentDatabase;
            _documentsStorage = documentDatabase.DocumentsStorage;
            _logger = LoggingSource.Instance.GetLogger<AttachmentsStorage>(documentDatabase.Name);

            tx.CreateTree(AttachmentsSlice);
            AttachmentsSchema.Create(tx, AttachmentsMetadataSlice, 44);
            DocumentsStorage.TombstonesSchema.Create(tx, AttachmentsTombstonesSlice, 16);
        }

        public IEnumerable<ReplicationBatchItem> GetAttachmentsFrom(DocumentsOperationContext context, long etag)
        {
            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);

            foreach (var result in table.SeekForwardFrom(AttachmentsSchema.FixedSizeIndexes[AttachmentsEtagSlice], etag, 0))
            {
                var attachment = TableValueToAttachment(context, ref result.Reader);

                var stream = GetAttachmentStream(context, attachment.Base64Hash);
                if (stream == null)
                    throw new FileNotFoundException($"Attachment's stream {attachment.Name} was not found. This should never happen.");
                attachment.Stream = stream;

                yield return ReplicationBatchItem.From(attachment);
            }
        }

        private long GetCountOfAttachmentsForHash(DocumentsOperationContext context, Slice hash)
        {
            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
            return table.GetCountOfMatchesFor(AttachmentsSchema.Indexes[AttachmentsHashSlice], hash);
        }

        public AttachmentResult PutAttachment(
            DocumentsOperationContext context,
            string documentId,
            string name,
            string contentType,
            string hash,
            long? expectedEtag,
            Stream stream)
        {
            if (context.Transaction == null)
            {
                DocumentPutAction.ThrowRequiresTransaction();
                return default(AttachmentResult);// never hit
            }

            // Attachment etag should be generated before updating the document
            var attachmenEtag = _documentsStorage.GenerateNextEtag();

            DocumentIdWorker.GetSliceFromId(context, documentId, out Slice lowerDocumentId);

            // This will validate that we cannot put an attachment on a conflicted document
            var hasDoc = TryGetDocumentTableValueReaderForAttachment(context, documentId, name, lowerDocumentId, out TableValueReader tvr);
            if (hasDoc == false)
                throw new InvalidOperationException($"Cannot put attachment {name} on a non existent document '{documentId}'.");

            DocumentIdWorker.GetLowerIdSliceAndStorageKey(context, name, out Slice lowerName, out Slice namePtr);
            DocumentIdWorker.GetLowerIdSliceAndStorageKey(context, contentType, out Slice lowerContentType, out Slice contentTypePtr);

            using (Slice.From(context.Allocator, hash, out Slice base64Hash)) // Hash is a base64 string, so this is a special case that we do not need to escape
            using (GetAttachmentKey(context, lowerDocumentId.Content.Ptr, lowerDocumentId.Size, lowerName.Content.Ptr, lowerName.Size, base64Hash, 
                lowerContentType.Content.Ptr, lowerContentType.Size, AttachmentType.Document, null, out Slice keySlice))
            {
                Debug.Assert(base64Hash.Size == 44, $"Hash size should be 44 but was: {keySlice.Size}");

                var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
                {
                    void SetTableValue(TableValueBuilder tbv)
                    {
                        tbv.Add(keySlice.Content.Ptr, keySlice.Size);
                        tbv.Add(Bits.SwapBytes(attachmenEtag));
                        tbv.Add(namePtr);
                        tbv.Add(contentTypePtr);
                        tbv.Add(base64Hash.Content.Ptr, base64Hash.Size);
                        tbv.Add(context.GetTransactionMarker());
                    }

                    if (table.ReadByKey(keySlice, out TableValueReader oldValue))
                    {
                        // This is an update to the attachment with the same stream and content type
                        // Just updating the etag and casing of the name and the content type.

                        if (expectedEtag != null)
                        {
                            var oldEtag = DocumentsStorage.TableValueToEtag(1, ref oldValue);
                            if (oldEtag != expectedEtag)
                                throw new ConcurrencyException($"Attachment {name} has etag {oldEtag}, but Put was called with etag {expectedEtag}. Optimistic concurrency violation, transaction will be aborted.")
                                {
                                    ActualETag = oldEtag,
                                    ExpectedETag = expectedEtag ?? -1
                                };
                        }

                        using (table.Allocate(out TableValueBuilder tbv))
                        {
                            SetTableValue(tbv);
                            table.Update(oldValue.Id, tbv);
                        }
                    }
                    else
                    {
                        var putStream = true;

                        // We already asserted that the document is not in conflict, so we might have just one partial key, not more.
                        using (GetAttachmentPartialKey(context, keySlice, base64Hash.Size, lowerContentType.Size, out Slice partialKeySlice))
                        {
                            if (table.SeekOnePrimaryKeyPrefix(partialKeySlice, out TableValueReader partialTvr))
                            {
                                // Delete the attachment stream only if we have a different hash
                                using (DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.Hash, ref partialTvr, out Slice existingHash))
                                {
                                    putStream = existingHash.Content.Match(base64Hash.Content) == false;
                                    if (putStream)
                                    {
                                        using (DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType,
                                            ref partialTvr, out Slice existingKey))
                                        {
                                            var existingEtag = DocumentsStorage.TableValueToEtag((int)AttachmentsTable.Etag, ref partialTvr);
                                            DeleteInternal(context, existingKey, existingEtag, existingHash);
                                        }
                                    }
                                }

                                table.Delete(partialTvr.Id);
                            }
                        }

                        if (expectedEtag.HasValue && expectedEtag.Value != 0)
                        {
                            ThrowConcurrentExceptionOnMissingAttachment(documentId, name, expectedEtag.Value);
                        }

                        using (table.Allocate(out TableValueBuilder tbv))
                        {
                            SetTableValue(tbv);
                            table.Insert(tbv);
                        }

                        if (putStream)
                        {
                            PutAttachmentStream(context, keySlice, base64Hash, stream);
                        }
                    }
                }

                _documentDatabase.Metrics.AttachmentPutsPerSecond.MarkSingleThreaded(1);

                // Update the document with an etag which is bigger than the attachmenEtag
                // We need to call this after we already put the attachment, so it can version also this attachment
                _documentsStorage.UpdateDocumentAfterAttachmentChange(context, lowerDocumentId, documentId, tvr);
            }

            return new AttachmentResult
            {
                Etag = attachmenEtag,
                ContentType = contentType,
                Name = name,
                DocumentId = documentId,
                Hash = hash,
                Size = stream.Length
            };
        }

        /// <summary>
        /// Should be used only from replication or smuggler.
        /// </summary>
        public void PutDirect(DocumentsOperationContext context, Slice key, Slice name, Slice contentType, Slice base64Hash)
        {
            Debug.Assert(base64Hash.Size == 44, $"Hash size should be 44 but was: {key.Size}");

            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
            using (table.Allocate(out TableValueBuilder tbv))
            {
                tbv.Add(key.Content.Ptr, key.Size);
                tbv.Add(Bits.SwapBytes(_documentsStorage.GenerateNextEtag()));
                tbv.Add(name.Content.Ptr, name.Size);
                tbv.Add(contentType.Content.Ptr, contentType.Size);
                tbv.Add(base64Hash.Content.Ptr, base64Hash.Size);
                tbv.Add(context.GetTransactionMarker());

                table.Set(tbv);
            }

            _documentDatabase.Metrics.AttachmentPutsPerSecond.MarkSingleThreaded(1);
        }

        public void RevisionAttachments(DocumentsOperationContext context, Slice lowerId, ChangeVectorEntry[] changeVector)
        {
            using (GetAttachmentPrefix(context, lowerId.Content.Ptr, lowerId.Size, AttachmentType.Document, null, out Slice prefixSlice))
            {
                var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
                var currentAttachments = new List<(LazyStringValue name, LazyStringValue contentType, Slice base64Hash)>();
                foreach (var sr in table.SeekByPrimaryKeyPrefix(prefixSlice, Slices.Empty, 0))
                {
                    var name = DocumentsStorage.TableValueToId(context, (int)AttachmentsTable.Name, ref sr.Reader);
                    var contentType = DocumentsStorage.TableValueToId(context, (int)AttachmentsTable.ContentType, ref sr.Reader);

                    var ptr = sr.Reader.Read((int)AttachmentsTable.Hash, out int size);
                    Slice.From(context.Allocator, ptr, size, out Slice base64Hash);

                    currentAttachments.Add((name, contentType, base64Hash));
                }
                foreach (var attachment in currentAttachments)
                {
                    PutRevisionAttachment(context, lowerId.Content.Ptr, lowerId.Size, changeVector, attachment);
                    attachment.name.Dispose();
                    attachment.contentType.Dispose();
                    attachment.base64Hash.Release(context.Allocator);
                }
            }
        }

        private void PutRevisionAttachment(DocumentsOperationContext context, byte* lowerId, int lowerIdSize, 
            ChangeVectorEntry[] changeVector, (LazyStringValue name, LazyStringValue contentType, Slice base64Hash) attachment)
        {
            var attachmentEtag = _documentsStorage.GenerateNextEtag();

            DocumentIdWorker.GetLowerIdSliceAndStorageKey(context, attachment.name, out Slice lowerName, out Slice namePtr);
            DocumentIdWorker.GetLowerIdSliceAndStorageKey(context, attachment.contentType, out Slice lowerContentType, out Slice contentTypePtr);

            using (GetAttachmentKey(context, lowerId, lowerIdSize, lowerName.Content.Ptr, lowerName.Size, attachment.base64Hash, 
                lowerContentType.Content.Ptr, lowerContentType.Size,  AttachmentType.Revision, changeVector, out Slice keySlice))
            {
                var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
                using (table.Allocate(out TableValueBuilder tbv))
                {
                    tbv.Add(keySlice.Content.Ptr, keySlice.Size);
                    tbv.Add(Bits.SwapBytes(attachmentEtag));
                    tbv.Add(namePtr);
                    tbv.Add(contentTypePtr);
                    tbv.Add(attachment.base64Hash);
                    tbv.Add(context.GetTransactionMarker());
                    table.Set(tbv);
                }
            }
        }

        public void PutAttachmentStream(DocumentsOperationContext context, Slice key, Slice base64Hash, Stream stream)
        {
            Debug.Assert(stream.Position == 0);

            var tree = context.Transaction.InnerTransaction.CreateTree(AttachmentsSlice);
            var existingStream = tree.ReadStream(base64Hash);
            if (existingStream == null)
                tree.AddStream(base64Hash, stream, tag: key);

            _documentDatabase.Metrics.AttachmentBytesPutsPerSecond.MarkSingleThreaded(stream.Length);
        }

        private void DeleteAttachmentStream(DocumentsOperationContext context, Slice hash, int expectedCount = 1)
        {
            if (GetCountOfAttachmentsForHash(context, hash) == expectedCount)
            {
                var tree = context.Transaction.InnerTransaction.CreateTree(AttachmentsSlice);
                tree.DeleteStream(hash);
            }
        }

        private bool TryGetDocumentTableValueReaderForAttachment(DocumentsOperationContext context, string documentId,
            string name, Slice lowerDocumentId, out TableValueReader tvr)
        {
            bool hasDoc;
            try
            {
                hasDoc = _documentsStorage.GetTableValueReaderForDocument(context, lowerDocumentId, out tvr);
            }
            catch (DocumentConflictException e)
            {
                throw new InvalidOperationException($"Cannot put/delete an attachment {name} on a document '{documentId}' when it has an unresolved conflict.", e);
            }
            return hasDoc;
        }

        public IEnumerable<Attachment> GetAttachmentsForDocument(DocumentsOperationContext context, Slice prefixSlice)
        {
            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
            foreach (var sr in table.SeekByPrimaryKeyPrefix(prefixSlice, Slices.Empty, 0))
            {
                var attachment = TableValueToAttachment(context, ref sr.Reader);
                if (attachment == null)
                    continue;

                attachment.Size = GetAttachmentStreamLength(context, attachment.Base64Hash);

                yield return attachment;
            }
        }

        public DynamicJsonArray GetAttachmentsMetadataForDocument(DocumentsOperationContext context, Slice lowerDocumentId)
        {
            var attachments = new DynamicJsonArray();
            using (GetAttachmentPrefix(context, lowerDocumentId, AttachmentType.Document, null, out Slice prefixSlice))
            {
                foreach (var attachment in GetAttachmentsForDocument(context, prefixSlice))
                {
                    attachments.Add(new DynamicJsonValue
                    {
                        [nameof(AttachmentResult.Name)] = attachment.Name,
                        [nameof(AttachmentResult.Hash)] = attachment.Base64Hash.ToString(), // TODO: Do better than create a string
                        [nameof(AttachmentResult.ContentType)] = attachment.ContentType,
                        [nameof(AttachmentResult.Size)] = attachment.Size,
                    });
                }
            }
            return attachments;
        }

        public (long AttachmentCount, long StreamsCount) GetNumberOfAttachments(DocumentsOperationContext context)
        {
            // We count in also revision attachments

            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
            var count = table.NumberOfEntries;

            var tree = context.Transaction.InnerTransaction.CreateTree(AttachmentsSlice);
            var streamsCount = tree.State.NumberOfEntries;

            return (count, streamsCount);
        }

        public Attachment GetAttachment(DocumentsOperationContext context, string documentId, string name, 
            AttachmentType type, ChangeVectorEntry[] changeVector)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Argument is null or whitespace", nameof(documentId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument is null or whitespace", nameof(name));
            if (context.Transaction == null)
                throw new ArgumentException("Context must be set with a valid transaction before calling Get", nameof(context));
            if (type != AttachmentType.Document && changeVector == null)
                throw new ArgumentException($"Change Vector cannot be null for attachment type {type}", nameof(changeVector));

            var attachment = GetAttachmentDirect(context, documentId, name, type, changeVector);
            if (attachment == null)
                return null;

            var stream = GetAttachmentStream(context, attachment.Base64Hash);
            if (stream == null)
                throw new FileNotFoundException($"Attachment's stream {name} on {documentId} was not found. This should not happen and is likely a bug.");
            attachment.Stream = stream;

            return attachment;
        }

        private Attachment GetAttachmentDirect(DocumentsOperationContext context, string documentId, string name,
            AttachmentType type, ChangeVectorEntry[] changeVector)
        {
            using (DocumentIdWorker.GetSliceFromId(context, documentId, out Slice lowerId))
            using (DocumentIdWorker.GetSliceFromId(context, name, out Slice lowerName))
            using (GetAttachmentPartialKey(context, lowerId.Content.Ptr, lowerId.Size, lowerName.Content.Ptr, lowerName.Size, type, changeVector, out Slice partialKeySlice))
            {
                var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);

                if (table.SeekOnePrimaryKeyPrefix(partialKeySlice, out TableValueReader tvr) == false)
                    return null;

                return TableValueToAttachment(context, ref tvr);
            }
        }

        public Stream GetAttachmentStream(DocumentsOperationContext context, Slice hashSlice)
        {
            var tree = context.Transaction.InnerTransaction.ReadTree(AttachmentsSlice);
            return tree.ReadStream(hashSlice);
        }

        private long GetAttachmentStreamLength(DocumentsOperationContext context, Slice hashSlice)
        {
            var tree = context.Transaction.InnerTransaction.ReadTree(AttachmentsSlice);
            var info = tree.GetStreamInfo(hashSlice, false);
            if (info == null)
                return -1;
            return info->TotalSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteStringContext.InternalScope GetAttachmentKey(DocumentsOperationContext context, byte* lowerId, int lowerIdSize,
            byte* lowerName, int lowerNameSize, Slice base64Hash, byte* lowerContentTypePtr, int lowerContentTypeSize,
            AttachmentType type, ChangeVectorEntry[] changeVector, out Slice keySlice)
        {
            return GetAttachmentKeyInternal(context, lowerId, lowerIdSize, lowerName, lowerNameSize, base64Hash, lowerContentTypePtr, lowerContentTypeSize, KeyType.Key, type, changeVector, out keySlice);
        }

        // NOTE: GetAttachmentPartialKey should be called only when the document's that hold the attachment does not have a conflict.
        // In this specific case it is ensured that we have a unique partial keys.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteStringContext.InternalScope GetAttachmentPartialKey(DocumentsOperationContext context, byte* lowerId, int lowerIdSize, byte* lowerName, int lowerNameSize,
            AttachmentType type, ChangeVectorEntry[] changeVector, out Slice partialKeySlice)
        {
            return GetAttachmentKeyInternal(context, lowerId, lowerIdSize, lowerName, lowerNameSize, default(Slice), null, 0, KeyType.PartialKey, type, changeVector, out partialKeySlice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteStringContext<ByteStringMemoryCache>.ExternalScope GetAttachmentPartialKey(DocumentsOperationContext context, Slice key,
            int base64HashSize, int lowerContentTypeSize, out Slice partialKeySlice)
        {
            return Slice.External(context.Allocator, key.Content, 0, key.Size - base64HashSize - 1 - lowerContentTypeSize, out partialKeySlice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteStringContext.InternalScope GetAttachmentPrefix(DocumentsOperationContext context, byte* lowerId, int lowerIdSize, AttachmentType type, ChangeVectorEntry[] changeVector, out Slice prefixSlice)
        {
            return GetAttachmentKeyInternal(context, lowerId, lowerIdSize, null, 0, default(Slice), null, 0, KeyType.Prefix, type, changeVector, out prefixSlice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteStringContext.InternalScope GetAttachmentPrefix(DocumentsOperationContext context, Slice lowerId,
            AttachmentType type, ChangeVectorEntry[] changeVector, out Slice prefixSlice)
        {
            return GetAttachmentKeyInternal(context, lowerId.Content.Ptr, lowerId.Size, null, 0, default(Slice), null, 0, KeyType.Prefix, type, changeVector, out prefixSlice);
        }

        /*
        // Document key: {lowerDocumentId|d|lowerName|hash|lowerContentType}
        // Revision key: {lowerDocumentId|r|changeVector|lowerName|hash|lowerContentType}
        // 
        // Document partial key: {lowerDocumentId|d|lowerName|}
        // Revision partial key: {lowerDocumentId|r|changeVector|}
        //
        // Document prefix: {lowerDocumentId|d|}
        // Revision prefix: {lowerDocumentId|r|changeVector|}
        */

        private ByteStringContext.InternalScope GetAttachmentKeyInternal(DocumentsOperationContext context, byte* lowerId, int lowerIdSize, 
            byte* lowerName, int lowerNameSize, Slice base64Hash, byte* lowerContentTypePtr, int lowerContentTypeSize, 
            KeyType keyType, AttachmentType type, ChangeVectorEntry[] changeVector, out Slice keySlice)
        {
            var changeVectorSize = 0;

            var size = lowerIdSize + 3;
            if (type != AttachmentType.Document)
            {
                changeVectorSize = sizeof(ChangeVectorEntry) * changeVector.Length;
                size += changeVectorSize + 1;
            }
            if (keyType == KeyType.Key)
            {
                size += lowerNameSize + 1 + base64Hash.Size + 1 + lowerContentTypeSize;
            }
            else if (keyType == KeyType.PartialKey)
            {
                size += lowerNameSize + 1;
            }

            var scope = context.Allocator.Allocate(size, out ByteString keyMem);

            Memory.Copy(keyMem.Ptr, lowerId, lowerIdSize);
            var pos = lowerIdSize;
            keyMem.Ptr[pos++] = SpecialChars.RecordSeparator;

            switch (type)
            {
                case AttachmentType.Document:
                    keyMem.Ptr[pos++] = (byte)'d';
                    break;
                case AttachmentType.Revision:
                    keyMem.Ptr[pos++] = (byte)'r';
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            keyMem.Ptr[pos++] = SpecialChars.RecordSeparator;

            if (type != AttachmentType.Document)
            {
                fixed (ChangeVectorEntry* pChangeVector = changeVector)
                {
                    Memory.Copy(keyMem.Ptr + pos, (byte*)pChangeVector, changeVectorSize);
                }
                pos += changeVectorSize;
                keyMem.Ptr[pos++] = SpecialChars.RecordSeparator;
            }

            if (keyType != KeyType.Prefix)
            {
                Memory.Copy(keyMem.Ptr + pos, lowerName, lowerNameSize);
                pos += lowerNameSize;
                keyMem.Ptr[pos++] = SpecialChars.RecordSeparator;

                if (keyType == KeyType.Key)
                {
                    base64Hash.CopyTo(keyMem.Ptr + pos);
                    pos += base64Hash.Size;
                    keyMem.Ptr[pos++] = SpecialChars.RecordSeparator;

                    Memory.Copy(keyMem.Ptr + pos, lowerContentTypePtr, lowerContentTypeSize);
                }
            }

            keySlice = new Slice(SliceOptions.Key, keyMem);
            return scope;
        }

        private enum KeyType
        {
            Key,
            PartialKey,
            Prefix
        }

        private Attachment TableValueToAttachment(DocumentsOperationContext context, ref TableValueReader tvr)
        {
            var result = new Attachment
            {
                StorageId = tvr.Id,
                Key = DocumentsStorage.TableValueToString(context, (int)AttachmentsTable.LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType, ref tvr),
                Etag = DocumentsStorage.TableValueToEtag((int)AttachmentsTable.Etag, ref tvr),
                Name = DocumentsStorage.TableValueToId(context, (int)AttachmentsTable.Name, ref tvr),
                ContentType = DocumentsStorage.TableValueToId(context, (int)AttachmentsTable.ContentType, ref tvr)
            };

            DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.Hash, ref tvr, out result.Base64Hash);

            result.TransactionMarker = *(short*)tvr.Read((int)AttachmentsTable.TransactionMarker, out int size);

            return result;
        }

        private static void ThrowConcurrentExceptionOnMissingAttachment(string documentId, string name, long expectedEtag)
        {
            throw new ConcurrencyException(
                $"Attachment {name} of '{documentId}' does not exist, but Put was called with etag {expectedEtag}. Optimistic concurrency violation, transaction will be aborted.")
            {
                ExpectedETag = expectedEtag
            };
        }

        public long? DeleteAttachment(DocumentsOperationContext context, string documentId, string name, long? expectedEtag)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Argument is null or whitespace", nameof(documentId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument is null or whitespace", nameof(name));
            if (context.Transaction == null)
                throw new ArgumentException("Context must be set with a valid transaction before calling Get", nameof(context));

            using (DocumentIdWorker.GetSliceFromId(context, documentId, out Slice lowerDocumentId))
            {
                var hasDoc = TryGetDocumentTableValueReaderForAttachment(context, documentId, name, lowerDocumentId, out TableValueReader docTvr);
                if (hasDoc == false)
                {
                    if (expectedEtag != null)
                        throw new ConcurrencyException($"Document {documentId} does not exist, " +
                                                       $"but delete was called with etag {expectedEtag} to remove attachment {name}. " +
                                                       $"Optimistic concurrency violation, transaction will be aborted.");

                    // this basically mean that we tried to delete attachment whose document doesn't exist.
                    return null;
                }

                using (DocumentIdWorker.GetSliceFromId(context, name, out Slice lowerName))
                using (GetAttachmentPartialKey(context, lowerDocumentId.Content.Ptr, lowerDocumentId.Size, lowerName.Content.Ptr, lowerName.Size, AttachmentType.Document, null, out Slice partialKeySlice))
                {
                    DeleteAttachmentDirect(context, partialKeySlice, true, name, expectedEtag);
                }

                var documentPutResult = _documentsStorage.UpdateDocumentAfterAttachmentChange(context, lowerDocumentId, documentId, docTvr);
                return documentPutResult.Etag;
            }
        }

        public void DeleteAttachmentDirect(DocumentsOperationContext context, Slice key, bool isPartialKey, string name, long? expectedEtag)
        {
            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);

            if (isPartialKey ?
                table.SeekOnePrimaryKeyPrefix(key, out TableValueReader tvr) == false :
                table.ReadByKey(key, out tvr) == false)
            {
                if (expectedEtag != null)
                    throw new ConcurrencyException($"Attachment {name} with key '{key}' does not exist, " +
                                                   $"but delete was called with etag {expectedEtag}. " +
                                                   $"Optimistic concurrency violation, transaction will be aborted.");

                // this basically means that we tried to delete attachment that doesn't exist.
                return;
            }

            var etag = DocumentsStorage.TableValueToEtag((int)AttachmentsTable.Etag, ref tvr);

            using (isPartialKey ? DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType, ref tvr, out key) : default(ByteStringContext<ByteStringMemoryCache>.ExternalScope))
            using (DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.Hash, ref tvr, out Slice hash))
            {
                if (expectedEtag != null && etag != expectedEtag)
                {
                    throw new ConcurrencyException($"Attachment {name} with key '{key}' has etag {etag}, but Delete was called with etag {expectedEtag}. Optimistic concurrency violation, transaction will be aborted.")
                    {
                        ActualETag = etag,
                        ExpectedETag = (long)expectedEtag
                    };
                }

                DeleteInternal(context, key, etag, hash);
            }

            table.Delete(tvr.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeleteInternal(DocumentsOperationContext context, Slice key, long etag, Slice hash)
        {
            CreateTombstone(context, key, etag);
            
            // we are running just before the delete, so we may still have 1 entry there, the one just
            // about to be deleted
            DeleteAttachmentStream(context, hash);
        }

        private void CreateTombstone(DocumentsOperationContext context, Slice keySlice, long attachmentEtag)
        {
            var newEtag = _documentsStorage.GenerateNextEtag();

            var table = context.Transaction.InnerTransaction.OpenTable(DocumentsStorage.TombstonesSchema, AttachmentsTombstonesSlice);
            using (table.Allocate(out TableValueBuilder tbv))
            {
                tbv.Add(keySlice.Content.Ptr, keySlice.Size);
                tbv.Add(Bits.SwapBytes(newEtag));
                tbv.Add(Bits.SwapBytes(attachmentEtag));
                tbv.Add(context.GetTransactionMarker());
                tbv.Add((byte)DocumentTombstone.TombstoneType.Attachment);
                tbv.Add(null, 0);
                tbv.Add((int)DocumentFlags.None);
                tbv.Add(null, 0);
                tbv.Add(null, 0);

                table.Insert(tbv);
            }
        }

        private void DeleteAttachmentsOfDocumentInternal(DocumentsOperationContext context, Slice prefixSlice)
        {
            var table = context.Transaction.InnerTransaction.OpenTable(AttachmentsSchema, AttachmentsMetadataSlice);
            {
                table.DeleteByPrimaryKeyPrefix(prefixSlice, before =>
                {
                    using (DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.LowerDocumentIdAndLowerNameAndTypeAndHashAndContentType, ref before.Reader, out Slice key))
                    using (DocumentsStorage.TableValueToSlice(context, (int)AttachmentsTable.Hash, ref before.Reader, out Slice hash))
                    {
                        var etag = DocumentsStorage.TableValueToEtag((int)AttachmentsTable.Etag, ref before.Reader);
                        DeleteInternal(context, key, etag, hash);
                    }
                });
            }
        }

        public void DeleteRevisionAttachments(DocumentsOperationContext context, Document revision)
        {
            using (GetAttachmentPrefix(context, revision.LowerId.Buffer, revision.LowerId.Size, AttachmentType.Revision, revision.ChangeVector, out Slice prefixSlice))
            {
                DeleteAttachmentsOfDocumentInternal(context, prefixSlice);
            }
        }

        public void DeleteAttachmentsOfDocument(DocumentsOperationContext context, Slice lowerId)
        {
            using (GetAttachmentPrefix(context, lowerId.Content.Ptr, lowerId.Size, AttachmentType.Document, null, out Slice prefixSlice))
            {
                DeleteAttachmentsOfDocumentInternal(context, prefixSlice);
            }
        }

        public ReleaseTempFile GetTempFile(out Stream file, string prefix = null)
        {
            var name = $"attachment.{Guid.NewGuid():N}.put";
            if (prefix != null)
                name = prefix + name;
            var tempPath = Path.Combine(_documentsStorage.Environment.Options.DataPager.Options.TempPath, name);

            if (_documentDatabase.DocumentsStorage.Environment.Options.EncryptionEnabled)
                file = new TempCryptoStream(tempPath);
            else
                file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.SequentialScan);

            
            return new ReleaseTempFile(tempPath, file);
        }

        public struct ReleaseTempFile : IDisposable
        {
            private readonly string _tempFile;
            private readonly Stream _file;

            public ReleaseTempFile(string tempFile, Stream file)
            {
                _tempFile = tempFile;
                _file = file;
            }

            public void Dispose()
            {
                _file.Dispose();

                // Linux does not clean the file, so we should clean it manually
                IOExtensions.DeleteFile(_tempFile);
            }
        }
    }
}