﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Smuggler;
using Raven.Client.Documents.Transformers;
using Raven.Client.Util;
using Raven.Server.Documents;
using Raven.Server.Documents.Indexes;
using Raven.Server.ServerWide.Context;
using Raven.Server.Smuggler.Documents.Data;
using Raven.Server.Smuggler.Documents.Processors;
using Sparrow.Json;
using Sparrow.Logging;
using Voron;
using Voron.Global;
using Sparrow;

namespace Raven.Server.Smuggler.Documents
{
    public class DatabaseDestination : ISmugglerDestination
    {
        private readonly DocumentDatabase _database;

        private Logger _log;
        private BuildVersionType _buildType;

        public DatabaseDestination(DocumentDatabase database)
        {
            _database = database;
            _log = LoggingSource.Instance.GetLogger<DatabaseDestination>(database.Name);
        }

        public IDisposable Initialize(DatabaseSmugglerOptions options, SmugglerResult result, long buildVersion)
        {
            _buildType = BuildVersion.Type(buildVersion);
            return null;
        }

        public IDocumentActions Documents()
        {
            return new DatabaseDocumentActions(_database, _buildType, isRevision: false, log: _log);
        }

        public IDocumentActions RevisionDocuments()
        {
            return new DatabaseDocumentActions(_database, _buildType, isRevision: true, log: _log);
        }

        public IIdentityActions Identities()
        {
            return new DatabaseIdentityActions(_database);
        }

        public IIndexActions Indexes()
        {
            return new DatabaseIndexActions(_database);
        }

        public ITransformerActions Transformers()
        {
            return new DatabaseTransformerActions(_database);
        }

        private class DatabaseTransformerActions : ITransformerActions
        {
            private readonly DocumentDatabase _database;

            public DatabaseTransformerActions(DocumentDatabase database)
            {
                _database = database;
            }

            public void WriteTransformer(TransformerDefinition transformerDefinition)
            {
                AsyncHelpers.RunSync(() => _database.TransformerStore.CreateTransformer(transformerDefinition));
            }

            public void Dispose()
            {
            }
        }

        private class DatabaseIndexActions : IIndexActions
        {
            private readonly DocumentDatabase _database;

            public DatabaseIndexActions(DocumentDatabase database)
            {
                _database = database;
            }

            public void WriteIndex(IndexDefinitionBase indexDefinition, IndexType indexType)
            {
                AsyncHelpers.RunSync(() => _database.IndexStore.CreateIndex(indexDefinition));
            }

            public void WriteIndex(IndexDefinition indexDefinition)
            {
                AsyncHelpers.RunSync(() => _database.IndexStore.CreateIndex(indexDefinition));
            }

            public void Dispose()
            {
            }
        }

        public class DatabaseDocumentActions : IDocumentActions
        {
            private readonly DocumentDatabase _database;
            private readonly BuildVersionType _buildType;
            private readonly bool _isRevision;
            private readonly Logger _log;
            private MergedBatchPutCommand _command;
            private MergedBatchPutCommand _prevCommand;
            private Task _prevCommandTask = Task.CompletedTask;

            private readonly Sparrow.Size _enqueueThreshold;

            public DatabaseDocumentActions(DocumentDatabase database, BuildVersionType buildType, bool isRevision, Logger log)
            {
                _database = database;
                _buildType = buildType;
                _isRevision = isRevision;
                _log = log;
                _enqueueThreshold = new Sparrow.Size(
                    (sizeof(int) == IntPtr.Size || database.Configuration.Storage.ForceUsing32BitsPager) ? 2 : 32,
                    SizeUnit.Megabytes);

                _command = new MergedBatchPutCommand(database, buildType, log)
                {
                    IsRevision = isRevision
                };
            }

            public void WriteDocument(Document document)
            {
                _command.Add(new DocumentItem
                {
                    Type = DocumentType.Document,
                    Document = document,
                });

                HandleBatchOfDocumentsIfNecessary();
            }

            public void WriteAttachment(StreamSource.AttachmentStream attachment)
            {
                _command.Add(new DocumentItem
                {
                    Type = DocumentType.Attachment,
                    Attachment = attachment,
                });
            }

            public StreamSource.AttachmentStream CreateAttachment()
            {
                var attachment = new StreamSource.AttachmentStream();
                attachment.FileDispose = _database.DocumentsStorage.AttachmentsStorage.GetTempFile(out attachment.File, "smuggler-");
                return attachment;
            }

            public DocumentsOperationContext GetContextForNewDocument()
            {
                _command.Context.CachedProperties.NewDocument();
                return _command.Context;
            }

            public void Dispose()
            {
                FinishBatchOfDocuments();
            }

            private void HandleBatchOfDocumentsIfNecessary()
            {
                var prevDoneAndHasEnough = _command.Context.AllocatedMemory > Constants.Size.Megabyte && _prevCommandTask.IsCompleted;
                var currentReachedLimit = _command.Context.AllocatedMemory > _enqueueThreshold.GetValue(SizeUnit.Bytes);

                if (currentReachedLimit == false && prevDoneAndHasEnough == false)
                    return;

                var prevCommand = _prevCommand;
                var prevCommandTask = _prevCommandTask;

                _prevCommand = _command;
                _prevCommandTask = _database.TxMerger.Enqueue(_command);

                if (prevCommand != null)
                {
                    using (prevCommand)
                    {
                        prevCommandTask.Wait();
                        Debug.Assert(prevCommand.IsDisposed == false,
                            "we rely on reusing this context on the next batch, so it has to be disposed here");
                    }
                }

                _command = new MergedBatchPutCommand(_database, _buildType, _log)
                {
                    IsRevision = _isRevision,
                };
            }

            private void FinishBatchOfDocuments()
            {
                if (_prevCommand != null)
                {
                    using (_prevCommand)
                        AsyncHelpers.RunSync(() => _prevCommandTask);

                    _prevCommand = null;
                }

                if (_command.Documents.Count > 0)
                {
                    using (_command)
                        AsyncHelpers.RunSync(() => _database.TxMerger.Enqueue(_command));
                }

                _command = null;
            }
        }

        private class DatabaseIdentityActions : IIdentityActions
        {
            private readonly DocumentDatabase _database;
            private readonly DocumentsOperationContext _context;
            private readonly Dictionary<string, long> _identities;
            private readonly IDisposable _returnContext;

            public DatabaseIdentityActions(DocumentDatabase database)
            {
                _database = database;
                _returnContext = _database.DocumentsStorage.ContextPool.AllocateOperationContext(out _context);
                _identities = new Dictionary<string, long>();
            }

            public void WriteIdentity(string key, long value)
            {
                _identities[key] = value;
            }

            public void Dispose()
            {
                try
                {
                    if (_identities.Count == 0)
                        return;

                    using (var tx = _context.OpenWriteTransaction())
                    {
                        _database.DocumentsStorage.Identities.Update(_context, _identities);

                        tx.Commit();
                    }
                }
                finally
                {
                    _returnContext?.Dispose();
                }
            }
        }

        private class MergedBatchPutCommand : TransactionOperationsMerger.MergedTransactionCommand, IDisposable
        {
            public bool IsRevision;

            private readonly DocumentDatabase _database;
            private readonly BuildVersionType _buildType;
            private readonly Logger _log;

            public readonly List<DocumentItem> Documents = new List<DocumentItem>();
            private IDisposable _resetContext;
            private bool _isDisposed;

            public bool IsDisposed => _isDisposed;

            private readonly DocumentsOperationContext _context;
            private const string PreV4RevisionsDocumentKey = "/revisions/";

            public MergedBatchPutCommand(DocumentDatabase database, BuildVersionType buildType, Logger log)
            {
                _database = database;
                _buildType = buildType;
                _log = log;
                _resetContext = _database.DocumentsStorage.ContextPool.AllocateOperationContext(out _context);
            }

            public DocumentsOperationContext Context => _context;

            public override int Execute(DocumentsOperationContext context)
            {
                if (_log.IsInfoEnabled)
                    _log.Info($"Importing {Documents.Count:#,#} documents");

                foreach (var documentType in Documents)
                {
                    if (documentType.Type == DocumentType.Attachment)
                    {
                        using (var attachment = documentType.Attachment)
                        using (Slice.From(context.Allocator, "Smuggler", out Slice tag)) // TODO: Export the tag also
                        {
                            _database.DocumentsStorage.AttachmentsStorage.PutAttachmentStream(context, tag, attachment.Base64Hash, attachment.File);
                        }
                        continue;
                    }

                    var document = documentType.Document;
                    using (document.Data)
                    {
                        var key = document.Key;

                        if (IsRevision)
                        {
                            if (_database.BundleLoader.VersioningStorage == null)
                                ThrowVersioningDisabled();

                            PutAttachments(context, document);
                            // ReSharper disable once PossibleNullReferenceException
                            _database.BundleLoader.VersioningStorage.Put(context, key, document.Data, document.Flags, document.NonPersistentFlags, document.ChangeVector, document.LastModified.Ticks);
                            continue;
                        }

                        if (IsPreV4Revision(key, document))
                        {
                            // handle old revisions
                            if (_database.BundleLoader.VersioningStorage == null)
                                ThrowVersioningDisabled();

                            var endIndex = key.IndexOf(PreV4RevisionsDocumentKey, StringComparison.OrdinalIgnoreCase);
                            var newKey = key.Substring(0, endIndex);

                            // ReSharper disable once PossibleNullReferenceException
                            _database.BundleLoader.VersioningStorage.Put(context, newKey, document.Data, document.Flags, document.NonPersistentFlags, document.ChangeVector, document.LastModified.Ticks);
                            continue;
                        }

                        PutAttachments(context, document);
                        _database.DocumentsStorage.Put(context, key, null, document.Data, nonPersistentFlags: document.NonPersistentFlags);
                    }
                }

                return Documents.Count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe void PutAttachments(DocumentsOperationContext context, Document document)
            {
                if ((document.Flags & DocumentFlags.HasAttachments) != DocumentFlags.HasAttachments)
                    return;

                if (document.Data.TryGet(Client.Constants.Documents.Metadata.Key, out BlittableJsonReaderObject metadata) == false || 
                    metadata.TryGet(Client.Constants.Documents.Metadata.Attachments, out BlittableJsonReaderArray attachments) == false)
                    return;

                foreach (BlittableJsonReaderObject attachment in attachments)
                {
                    if (attachment.TryGet(nameof(AttachmentResult.Name), out LazyStringValue name) == false || 
                        attachment.TryGet(nameof(AttachmentResult.ContentType), out LazyStringValue contentType) == false || 
                        attachment.TryGet(nameof(AttachmentResult.Hash), out LazyStringValue hash) == false)
                        throw new ArgumentException($"The attachment info in missing a mandatory value: {attachment}");

                    var type = (document.Flags & DocumentFlags.Revision) == DocumentFlags.Revision ? AttachmentType.Revision : AttachmentType.Document;
                    var attachmentsStorage = _database.DocumentsStorage.AttachmentsStorage;
                    using (DocumentKeyWorker.GetSliceFromKey(_context, document.Key, out Slice lowerDocumentId))
                    using (DocumentKeyWorker.GetLowerKeySliceAndStorageKey(_context, name, out Slice lowerName, out Slice nameSlice))
                    using (DocumentKeyWorker.GetLowerKeySliceAndStorageKey(_context, contentType, out Slice lowerContentType, out Slice contentTypeSlice))
                    using (Slice.External(_context.Allocator, hash, out Slice base64Hash))
                    using (attachmentsStorage.GetAttachmentKey(_context, lowerDocumentId.Content.Ptr, lowerDocumentId.Size, lowerName.Content.Ptr, lowerName.Size,
                        base64Hash, lowerContentType.Content.Ptr, lowerContentType.Size, type, document.ChangeVector, out Slice keySlice))
                    {
                        attachmentsStorage.PutDirect(context, keySlice, nameSlice, contentTypeSlice, base64Hash);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsPreV4Revision(string key, Document document)
            {
                if (_buildType == BuildVersionType.V3 == false)
                    return false;

                if ((document.NonPersistentFlags & NonPersistentDocumentFlags.LegacyRevision) != NonPersistentDocumentFlags.LegacyRevision)
                    return false;

                return key.Contains(PreV4RevisionsDocumentKey);
            }

            private static void ThrowVersioningDisabled()
            {
                throw new InvalidOperationException("Revision bundle needs to be enabled before import!");
            }

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;

                Documents.Clear();
                _resetContext?.Dispose();
                _resetContext = null;
            }

            public void Add(DocumentItem document)
            {
                Documents.Add(document);
            }
        }
    }
}