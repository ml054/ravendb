//-----------------------------------------------------------------------
// <copyright file="InMemoryDocumentSessionOperations.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Identity;
using Raven.Client.Documents.Session.Operations.Lazy;
using Raven.Client.Exceptions.Documents.Session;
using Raven.Client.Extensions;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Util;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    /// Abstract implementation for in memory session operations
    /// </summary>
    public abstract partial class InMemoryDocumentSessionOperations : IDisposable
    {
        [ThreadStatic]
        private static int _clientSessionIdCounter;

        protected readonly int _clientSessionId = ++_clientSessionIdCounter;

        protected readonly RequestExecutor _requestExecutor;
        private readonly IDisposable _releaseOperationContext;
        private readonly JsonOperationContext _context;
        protected readonly List<ILazyOperation> PendingLazyOperations = new List<ILazyOperation>();
        protected readonly Dictionary<ILazyOperation, Action<object>> OnEvaluateLazy = new Dictionary<ILazyOperation, Action<object>>();
        private static int _instancesCounter;
        private readonly int _hash = Interlocked.Increment(ref _instancesCounter);
        protected bool GenerateDocumentIdsOnStore = true;
        protected SessionInfo SessionInfo;
        private BatchOptions _saveChangesOptions;
        private bool _isDisposed;

        /// <summary>
        /// The session id 
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The entities waiting to be deleted
        /// </summary>
        protected readonly HashSet<object> DeletedEntities = new HashSet<object>(ObjectReferenceEqualityComparer<object>.Default);

        public event EventHandler<BeforeStoreEventArgs> OnBeforeStore;
        public event EventHandler<AfterStoreEventArgs> OnAfterStore;
        public event EventHandler<BeforeDeleteEventArgs> OnBeforeDelete;
        public event EventHandler<BeforeQueryExecutedEventArgs> OnBeforeQueryExecuted;

        /// <summary>
        /// Entities whose id we already know do not exists, because they are a missing include, or a missing load, etc.
        /// </summary>
        protected readonly HashSet<string> KnownMissingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, object> _externalState;

        public IDictionary<string, object> ExternalState => _externalState ?? (_externalState = new Dictionary<string, object>());

        public async Task<ServerNode> GetCurrentSessionNode()
        {
            (int Index, ServerNode Node) result;
            switch (_documentStore.Conventions.ReadBalanceBehavior)
            {
                case ReadBalanceBehavior.None:
                    result = await _requestExecutor.GetPreferredNode().ConfigureAwait(false);
                    break;
                case ReadBalanceBehavior.RoundRobin:
                    result = await _requestExecutor.GetNodeBySessionId(_clientSessionId).ConfigureAwait(false);
                    break;
                case ReadBalanceBehavior.FastestNode:
                    result = await _requestExecutor.GetFastestNode().ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(_documentStore.Conventions.ReadBalanceBehavior.ToString());
            }
            return result.Node;
        }

        /// <summary>
        /// Translate between an ID and its associated entity
        /// </summary>
        internal readonly DocumentsById DocumentsById = new DocumentsById();

        /// <summary>
        /// Translate between an ID and its associated entity
        /// </summary>
        internal readonly Dictionary<string, DocumentInfo> IncludedDocumentsById = new Dictionary<string, DocumentInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// hold the data required to manage the data for RavenDB's Unit of Work
        /// </summary>
        protected internal readonly Dictionary<object, DocumentInfo> DocumentsByEntity = new Dictionary<object, DocumentInfo>(ObjectReferenceEqualityComparer<object>.Default);

        protected readonly DocumentStoreBase _documentStore;

        public string DatabaseName { get; }

        ///<summary>
        /// The document store associated with this session
        ///</summary>
        public IDocumentStore DocumentStore => _documentStore;

        public RequestExecutor RequestExecutor => _requestExecutor;

        public JsonOperationContext Context => _context;
        /// <summary>
        /// Gets the number of requests for this session
        /// </summary>
        /// <value></value>
        public int NumberOfRequests { get; private set; }

        /// <summary>
        /// Gets the number of entities held in memory to manage Unit of Work
        /// </summary>
        public int NumberOfEntitiesInUnitOfWork => DocumentsByEntity.Count;

        /// <summary>
        /// Gets the store identifier for this session.
        /// The store identifier is the identifier for the particular RavenDB instance.
        /// </summary>
        /// <value>The store identifier.</value>
        public string StoreIdentifier => _documentStore.Identifier + ";" + DatabaseName;

        /// <summary>
        /// Gets the conventions used by this session
        /// </summary>
        /// <value>The conventions.</value>
        /// <remarks>
        /// This instance is shared among all sessions, changes to the <see cref="DocumentConventions"/> should be done
        /// via the <see cref="IDocumentStore"/> instance, not on a single session.
        /// </remarks>
        public DocumentConventions Conventions => _requestExecutor.Conventions;

        /// <summary>
        /// Gets or sets the max number of requests per session.
        /// If the <see cref="NumberOfRequests"/> rise above <see cref="MaxNumberOfRequestsPerSession"/>, an exception will be thrown.
        /// </summary>
        /// <value>The max number of requests per session.</value>
        public int MaxNumberOfRequestsPerSession { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the session should use optimistic concurrency.
        /// When set to <c>true</c>, a check is made so that a change made behind the session back would fail
        /// and raise <see cref="ConcurrencyException"/>.
        /// </summary>
        /// <value></value>
        public bool UseOptimisticConcurrency { get; set; }

        protected readonly List<ICommandData> DeferredCommands = new List<ICommandData>();
        protected readonly Dictionary<(string, CommandType, string), ICommandData> DeferredCommandsDictionary = new Dictionary<(string, CommandType, string), ICommandData>();

        public int DeferredCommandsCount => DeferredCommands.Count;

        public GenerateEntityIdOnTheClient GenerateEntityIdOnTheClient { get; }
        public EntityToBlittable EntityToBlittable { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDocumentSessionOperations"/> class.
        /// </summary>
        protected InMemoryDocumentSessionOperations(
            string databaseName,
            DocumentStoreBase documentStore,
            RequestExecutor requestExecutor,
            Guid id)
        {
            Id = id;
            DatabaseName = databaseName;
            _documentStore = documentStore;
            _requestExecutor = requestExecutor;
            _releaseOperationContext = requestExecutor.ContextPool.AllocateOperationContext(out _context);
            UseOptimisticConcurrency = _requestExecutor.Conventions.UseOptimisticConcurrency;
            MaxNumberOfRequestsPerSession = _requestExecutor.Conventions.MaxNumberOfRequestsPerSession;
            GenerateEntityIdOnTheClient = new GenerateEntityIdOnTheClient(_requestExecutor.Conventions, GenerateId);
            EntityToBlittable = new EntityToBlittable(this);
            SessionInfo = new SessionInfo(_clientSessionId , false);
        }

        /// <summary>
        /// Gets the metadata for the specified entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        public IMetadataDictionary GetMetadataFor<T>(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var documentInfo = GetDocumentInfo(instance);

            if (documentInfo.MetadataInstance != null)
                return documentInfo.MetadataInstance;

            var metadataAsBlittable = documentInfo.Metadata;
            var metadata = new MetadataAsDictionary(metadataAsBlittable);
            documentInfo.MetadataInstance = metadata;
            return metadata;
        }

        /// <summary>
        /// Gets the Change Vector for the specified entity.
        /// If the entity is transient, it will load the change vector from the store
        /// and associate the current state of the entity with the change vector from the server.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        public string GetChangeVectorFor<T>(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var documentInfo = GetDocumentInfo(instance);
            if (documentInfo.Metadata.TryGet(Constants.Documents.Metadata.ChangeVector, out string changeVectorJson))
                return changeVectorJson;

            return null;
        }

        private DocumentInfo GetDocumentInfo<T>(T instance)
        {
            if (DocumentsByEntity.TryGetValue(instance, out DocumentInfo documentInfo))
                return documentInfo;

            if (GenerateEntityIdOnTheClient.TryGetIdFromInstance(instance, out string id) == false && (instance is IDynamicMetaObjectProvider == false || GenerateEntityIdOnTheClient.TryGetIdFromDynamic(instance, out id) == false))
                throw new InvalidOperationException("Could not find the document id for " + instance);

            AssertNoNonUniqueInstance(instance, id);

            throw new ArgumentException("Document " + id + " doesn't exist in the session");
        }

        /// <summary>
        /// Returns whether a document with the specified id is loaded in the 
        /// current session
        /// </summary>
        public bool IsLoaded(string id)
        {
            return IsLoadedOrDeleted(id);
        }

        internal bool IsLoadedOrDeleted(string id)
        {
            return DocumentsById.TryGetValue(id, out DocumentInfo documentInfo) && documentInfo.Document != null ||
                IsDeleted(id) ||
                IncludedDocumentsById.ContainsKey(id);
        }

        /// <summary>
        /// Returns whether a document with the specified id is deleted 
        /// or known to be missing
        /// </summary>
        public bool IsDeleted(string id)
        {
            return KnownMissingIds.Contains(id);
        }

        /// <summary>
        /// Gets the document id.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        public string GetDocumentId(object instance)
        {
            if (instance == null)
                return null;
            DocumentInfo value;
            if (DocumentsByEntity.TryGetValue(instance, out value) == false)
                return null;
            return value.Id;
        }

        public void IncrementRequestCount()
        {
            if (++NumberOfRequests > MaxNumberOfRequestsPerSession)
                throw new InvalidOperationException($@"The maximum number of requests ({MaxNumberOfRequestsPerSession}) allowed for this session has been reached.
Raven limits the number of remote calls that a session is allowed to make as an early warning system. Sessions are expected to be short lived, and 
Raven provides facilities like Load(string[] ids) to load multiple documents at once and batch saves (call SaveChanges() only once).
You can increase the limit by setting DocumentConventions.MaxNumberOfRequestsPerSession or MaxNumberOfRequestsPerSession, but it is
advisable that you'll look into reducing the number of remote calls first, since that will speed up your application significantly and result in a 
more responsive application.
");
        }

        /// <summary>
        /// Tracks the entity inside the unit of work
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documentFound">The document found.</param>
        /// <returns></returns>
        public T TrackEntity<T>(DocumentInfo documentFound)
        {
            return (T)TrackEntity(typeof(T), documentFound);
        }

        /// <summary>
        /// Tracks the entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The document id.</param>
        /// <param name="document">The document.</param>
        /// <param name="metadata">The metadata.</param>'
        /// <param name="noTracking"></param>
        /// <returns></returns>
        public T TrackEntity<T>(string id, BlittableJsonReaderObject document, BlittableJsonReaderObject metadata, bool noTracking)
        {
            var entity = TrackEntity(typeof(T), id, document, metadata, noTracking);
            try
            {
                return (T)entity;
            }
            catch (InvalidCastException e)
            {
                var actual = typeof(T).Name;
                var expected = entity.GetType().Name;
                var message = string.Format("The query results type is '{0}' but you expected to get results of type '{1}'. " +
"If you want to return a projection, you should use .ProjectFromIndexFieldsInto<{1}>() (for Query) or .SelectFields<{1}>() (for DocumentQuery) before calling to .ToList().", expected, actual);
                throw new InvalidOperationException(message, e);
            }
        }

        /// <summary>
        /// Tracks the entity inside the unit of work
        /// </summary>
        /// <param name="entityType"></param>
        /// <param name="documentFound">The document found.</param>
        /// <returns></returns>
        public object TrackEntity(Type entityType, DocumentInfo documentFound)
        {
            return TrackEntity(entityType, documentFound.Id, documentFound.Document, documentFound.Metadata, noTracking: false);
        }

        /// <summary>
        /// Tracks the entity.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="id">The document ID.</param>
        /// <param name="document">The document.</param>
        /// <param name="metadata">The metadata.</param>
        /// <param name="noTracking">Entity tracking is enabled if true, disabled otherwise.</param>
        /// <returns></returns>
        private object TrackEntity(Type entityType, string id, BlittableJsonReaderObject document, BlittableJsonReaderObject metadata, bool noTracking)
        {
            if (string.IsNullOrEmpty(id))
            {
                return DeserializeFromTransformer(entityType, null, document);
            }

            DocumentInfo docInfo;
            if (DocumentsById.TryGetValue(id, out docInfo))
            {
                // the local instance may have been changed, we adhere to the current Unit of Work
                // instance, and return that, ignoring anything new.
                if (docInfo.Entity == null)
                    docInfo.Entity = ConvertToEntity(entityType, id, document);

                if (noTracking == false)
                {
                    IncludedDocumentsById.Remove(id);
                    DocumentsByEntity[docInfo.Entity] = docInfo;
                }
                return docInfo.Entity;
            }

            if (IncludedDocumentsById.TryGetValue(id, out docInfo))
            {
                if (docInfo.Entity == null)
                    docInfo.Entity = ConvertToEntity(entityType, id, document);

                if (noTracking == false)
                {
                    IncludedDocumentsById.Remove(id);
                    DocumentsById.Add(docInfo);
                    DocumentsByEntity[docInfo.Entity] = docInfo;
                }
                return docInfo.Entity;
            }

            var entity = ConvertToEntity(entityType, id, document);

            if (metadata.TryGet(Constants.Documents.Metadata.ChangeVector, out string changeVector) == false)
                throw new InvalidOperationException("Document " + id + " must have Change Vector");

            if (noTracking == false)
            {
                var newDocumentInfo = new DocumentInfo
                {
                    Id = id,
                    Document = document,
                    Metadata = metadata,
                    Entity = entity,
                    ChangeVector = changeVector
                };

                DocumentsById.Add(newDocumentInfo);
                DocumentsByEntity[entity] = newDocumentInfo;
            }

            return entity;
        }

        /// <summary>
        /// Converts the json document to an entity.
        /// </summary>
        /// <param name="entityType"></param>
        /// <param name="id">The id.</param>
        /// <param name="documentFound">The document found.</param>
        /// <returns></returns>
        public object ConvertToEntity(Type entityType, string id, BlittableJsonReaderObject documentFound)
        {
            //TODO: consider removing this function entirely, leaving only the EntityToBlittalbe version
            return EntityToBlittable.ConvertToEntity(entityType, id, documentFound);
        }

        private void RegisterMissingProperties(object o, string id, object value)
        {
            Dictionary<string, object> dictionary;
            if (EntityToBlittable.MissingDictionary.TryGetValue(o, out dictionary) == false)
            {
                EntityToBlittable.MissingDictionary[o] = dictionary = new Dictionary<string, object>();
            }

            dictionary[id] = value;
        }

        /// <summary>
        /// Gets the default value of the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object GetDefaultValue(Type type)
        {
            return type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Marks the specified entity for deletion. The entity will be deleted when SaveChanges is called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">The entity.</param>
        public void Delete<T>(T entity)
        {
            if (ReferenceEquals(entity, null))
                throw new ArgumentNullException(nameof(entity));

            if (DocumentsByEntity.TryGetValue(entity, out DocumentInfo value) == false)
            {
                throw new InvalidOperationException(entity + " is not associated with the session, cannot delete unknown entity instance");
            }
            DeletedEntities.Add(entity);
            IncludedDocumentsById.Remove(value.Id);
            KnownMissingIds.Add(value.Id);
        }

        /// <summary>
        /// Marks the specified entity for deletion. The entity will be deleted when <see cref="IDocumentSession.SaveChanges"/> is called.
        /// WARNING: This method will not call beforeDelete listener!
        /// </summary>
        /// <param name="id"></param>
        public void Delete(string id)
        {
            Delete(id, null);
        }

        public void Delete(string id, string expectedChangeVector)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            string changeVector = null;
            DocumentInfo documentInfo;
            if (DocumentsById.TryGetValue(id, out documentInfo))
            {
                var newObj = EntityToBlittable.ConvertEntityToBlittable(documentInfo.Entity, documentInfo);
                if (documentInfo.Entity != null && EntityChanged(newObj, documentInfo, null))
                {
                    throw new InvalidOperationException(
                        "Can't delete changed entity using identifier. Use Delete<T>(T entity) instead.");
                }
                if (documentInfo.Entity != null)
                {
                    DocumentsByEntity.Remove(documentInfo.Entity);
                }
                DocumentsById.Remove(id);
                changeVector = documentInfo.ChangeVector;
            }

            KnownMissingIds.Add(id);
            changeVector = UseOptimisticConcurrency ? changeVector : null;
            Defer(new DeleteCommandData(id, expectedChangeVector ?? changeVector));
        }

        //TODO
        /*internal void EnsureNotReadVetoed(RavenJObject metadata)
        {
            var readVeto = metadata["Raven-Read-Veto"] as RavenJObject;
            if (readVeto == null)
                return;

            var s = readVeto.Value<string>("Reason");
            throw new ReadVetoException(
                "Document could not be read because of a read veto." + Environment.NewLine +
                "The read was vetoed by: " + readVeto.Value<string>("Trigger") + Environment.NewLine +
                "Veto reason: " + s
            );
        }*/

        /// <summary>
        /// Stores the specified entity in the session. The entity will be saved when SaveChanges is called.
        /// </summary>
        public void Store(object entity)
        {
            var hasId = GenerateEntityIdOnTheClient.TryGetIdFromInstance(entity, out string _);
            StoreInternal(entity, null, null, hasId == false ? ConcurrencyCheckMode.Forced : ConcurrencyCheckMode.Auto);
        }

        /// <summary>
        /// Stores the specified entity in the session, explicitly specifying its Id. The entity will be saved when SaveChanges is called.
        /// </summary>
        public void Store(object entity, string id)
        {
            StoreInternal(entity, null, id, ConcurrencyCheckMode.Auto);
        }

        /// <summary>
        /// Stores the specified entity in the session, explicitly specifying its Id. The entity will be saved when SaveChanges is called.
        /// </summary>
        public void Store(object entity, string changeVector, string id)
        {
            StoreInternal(entity, changeVector, id, changeVector == null ? ConcurrencyCheckMode.Disabled : ConcurrencyCheckMode.Forced);
        }

        private void StoreInternal(object entity, string changeVector, string id, ConcurrencyCheckMode forceConcurrencyCheck)
        {
            if (null == entity)
                throw new ArgumentNullException(nameof(entity));

            DocumentInfo value;
            if (DocumentsByEntity.TryGetValue(entity, out value))
            {
                value.ConcurrencyCheckMode = forceConcurrencyCheck;
                return;
            }

            if (id == null)
            {
                if (GenerateDocumentIdsOnStore)
                {
                    id = GenerateEntityIdOnTheClient.GenerateDocumentIdForStorage(entity);
                }
                else
                {
                    RememberEntityForDocumentIdGeneration(entity);
                }
            }
            else
            {
                // Store it back into the Id field so the client has access to it                    
                GenerateEntityIdOnTheClient.TrySetIdentity(entity, id);
            }

            if (DeferredCommandsDictionary.ContainsKey((id, CommandType.ClientAnyCommand, null)))
                throw new InvalidOperationException("Can't store document, there is a deferred command registered for this document in the session. Document id: " + id);

            if (DeletedEntities.Contains(entity))
                throw new InvalidOperationException("Can't store object, it was already deleted in this session.  Document id: " + id);

            // we make the check here even if we just generated the ID
            // users can override the ID generation behavior, and we need
            // to detect if they generate duplicates.
            AssertNoNonUniqueInstance(entity, id);

            var tag = _requestExecutor.Conventions.GetCollectionName(entity);
            var metadata = new DynamicJsonValue();
            if (tag != null)
                metadata[Constants.Documents.Metadata.Collection] = tag;

            var clrType = _requestExecutor.Conventions.GetClrTypeName(entity.GetType());
            if (clrType != null)
                metadata[Constants.Documents.Metadata.RavenClrType] = clrType;

            if (id != null)
                KnownMissingIds.Remove(id);
            StoreEntityInUnitOfWork(id, entity, changeVector, metadata, forceConcurrencyCheck);
        }

        public Task StoreAsync(object entity, CancellationToken token = default(CancellationToken))
        {
            var hasId = GenerateEntityIdOnTheClient.TryGetIdFromInstance(entity, out string _);

            return StoreAsyncInternal(entity, null, null, hasId == false ? ConcurrencyCheckMode.Forced : ConcurrencyCheckMode.Auto, token: token);
        }

        public Task StoreAsync(object entity, string changeVector, string id, CancellationToken token = default(CancellationToken))
        {
            return StoreAsyncInternal(entity, changeVector, id, changeVector == null ? ConcurrencyCheckMode.Disabled : ConcurrencyCheckMode.Forced, token: token);
        }

        public Task StoreAsync(object entity, string id, CancellationToken token = default(CancellationToken))
        {
            return StoreAsyncInternal(entity, null, id, ConcurrencyCheckMode.Auto, token: token);
        }

        private async Task StoreAsyncInternal(object entity, string changeVector, string id, ConcurrencyCheckMode forceConcurrencyCheck, CancellationToken token = default(CancellationToken))
        {
            if (null == entity)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
            {
                id = await GenerateDocumentIdForStorageAsync(entity).WithCancellation(token).ConfigureAwait(false);
            }

            StoreInternal(entity, changeVector, id, forceConcurrencyCheck);
        }

        protected abstract string GenerateId(object entity);

        protected virtual void RememberEntityForDocumentIdGeneration(object entity)
        {
            throw new NotImplementedException("You cannot set GenerateDocumentIdsOnStore to false without implementing RememberEntityForDocumentIdGeneration");
        }

        protected internal async Task<string> GenerateDocumentIdForStorageAsync(object entity)
        {
            if (entity is IDynamicMetaObjectProvider)
            {
                if (GenerateEntityIdOnTheClient.TryGetIdFromDynamic(entity, out string id))
                    return id;

                id = await GenerateIdAsync(entity).ConfigureAwait(false);
                // If we generated a new id, store it back into the Id field so the client has access to it                    
                if (id != null)
                    GenerateEntityIdOnTheClient.TrySetIdOnDynamic(entity, id);
                return id;
            }

            var result = await GetOrGenerateDocumentIdAsync(entity).ConfigureAwait(false);
            GenerateEntityIdOnTheClient.TrySetIdentity(entity, result);
            return result;
        }

        protected abstract Task<string> GenerateIdAsync(object entity);

        protected virtual void StoreEntityInUnitOfWork(string id, object entity, string changeVector, DynamicJsonValue metadata, ConcurrencyCheckMode forceConcurrencyCheck)
        {
            DeletedEntities.Remove(entity);
            if (id != null)
                KnownMissingIds.Remove(id);

            var documentInfo = new DocumentInfo
            {
                Id = id,
                Metadata = Context.ReadObject(metadata, id),
                ChangeVector = changeVector,
                ConcurrencyCheckMode = forceConcurrencyCheck,
                Entity = entity,
                IsNewDocument = true,
                Document = null
            };

            DocumentsByEntity.Add(entity, documentInfo);
            if (id != null)
                DocumentsById.Add(documentInfo);
        }

        protected void AssertNoNonUniqueInstance(object entity, string id)
        {
            DocumentInfo info;
            if (string.IsNullOrEmpty(id) ||
                id[id.Length - 1] == '|' ||
                id[id.Length - 1] == '/' ||
                DocumentsById.TryGetValue(id, out info) == false ||
                ReferenceEquals(info.Entity, entity))
                return;

            throw new NonUniqueObjectException("Attempted to associate a different object with id '" + id + "'.");
        }

        protected async Task<string> GetOrGenerateDocumentIdAsync(object entity)
        {
            string id;
            GenerateEntityIdOnTheClient.TryGetIdFromInstance(entity, out id);

            Task<string> generator =
                id != null
                ? Task.FromResult(id)
                : GenerateIdAsync(entity);

            var result = await generator.ConfigureAwait(false);
            if (result != null && result.StartsWith("/"))
                throw new InvalidOperationException("Cannot use value '" + id + "' as a document id because it begins with a '/'");

            return result;
        }

        public SaveChangesData PrepareForSaveChanges()
        {
            var result = new SaveChangesData(this);
            DeferredCommands.Clear();
            DeferredCommandsDictionary.Clear();

            PrepareForEntitiesDeletion(result, null);
            PrepareForEntitiesPuts(result);

            return result;
        }

        private static void UpdateMetadataModifications(DocumentInfo documentInfo)
        {
            if (documentInfo.MetadataInstance == null || ((MetadataAsDictionary)documentInfo.MetadataInstance).Changed == false)
                return;

            if (documentInfo.Metadata.Modifications == null || documentInfo.Metadata.Modifications.Properties.Count == 0)
            {
                documentInfo.Metadata.Modifications = new DynamicJsonValue();
            }
            foreach (var prop in documentInfo.MetadataInstance.Keys)
            {
                documentInfo.Metadata.Modifications[prop] = documentInfo.MetadataInstance[prop];
            }
        }

        private void PrepareForEntitiesDeletion(SaveChangesData result, IDictionary<string, DocumentsChanges[]> changes)
        {
            foreach (var deletedEntity in DeletedEntities)
            {
                if (DocumentsByEntity.TryGetValue(deletedEntity, out DocumentInfo documentInfo) == false)
                    continue;

                if (changes != null)
                {
                    var docChanges = new List<DocumentsChanges>();
                    var change = new DocumentsChanges
                    {
                        FieldNewValue = string.Empty,
                        FieldOldValue = string.Empty,
                        Change = DocumentsChanges.ChangeType.DocumentDeleted
                    };

                    docChanges.Add(change);
                    changes[documentInfo.Id] = docChanges.ToArray();
                }
                else
                {
                    if (result.DeferredCommandsDictionary.TryGetValue((documentInfo.Id, CommandType.ClientAnyCommand, null), out ICommandData command))
                        ThrowInvalidDeletedDocumentWithDeferredCommand(command);

                    string changeVector = null;
                    if (DocumentsById.TryGetValue(documentInfo.Id, out documentInfo))
                    {
                        changeVector = documentInfo.ChangeVector;

                        if (documentInfo.Entity != null)
                        {
                            var afterStoreEventArgs = new AfterStoreEventArgs(this, documentInfo.Id, documentInfo.Entity);
                            OnAfterStore?.Invoke(this, afterStoreEventArgs);

                            DocumentsByEntity.Remove(documentInfo.Entity);
                            result.Entities.Add(documentInfo.Entity);
                        }

                        DocumentsById.Remove(documentInfo.Id);
                    }
                    changeVector = UseOptimisticConcurrency ? changeVector : null;
                    var beforeDeleteEventArgs = new BeforeDeleteEventArgs(this, documentInfo.Id, documentInfo.Entity);
                    OnBeforeDelete?.Invoke(this, beforeDeleteEventArgs);
                    result.SessionCommands.Add(new DeleteCommandData(documentInfo.Id, changeVector));
                }
            }
            DeletedEntities.Clear();
        }

        private void PrepareForEntitiesPuts(SaveChangesData result)
        {
            foreach (var entity in DocumentsByEntity)
            {
                UpdateMetadataModifications(entity.Value);
                var document = EntityToBlittable.ConvertEntityToBlittable(entity.Key, entity.Value);
                if (entity.Value.IgnoreChanges || EntityChanged(document, entity.Value, null) == false)
                    continue;

                if (result.DeferredCommandsDictionary.TryGetValue((entity.Value.Id, CommandType.ClientNotAttachmentPUT, null), out ICommandData command))
                    ThrowInvalidModifiedDocumentWithDeferredCommand(command);

                var onOnBeforeStore = OnBeforeStore;
                if (onOnBeforeStore != null)
                {
                    var beforeStoreEventArgs = new BeforeStoreEventArgs(this, entity.Value.Id, entity.Key);
                    onOnBeforeStore(this, beforeStoreEventArgs);
                    if (beforeStoreEventArgs.MetadataAccessed)
                        UpdateMetadataModifications(entity.Value);
                    if (beforeStoreEventArgs.MetadataAccessed ||
                        EntityChanged(document, entity.Value, null))
                        document = EntityToBlittable.ConvertEntityToBlittable(entity.Key, entity.Value);
                }

                entity.Value.IsNewDocument = false;
                result.Entities.Add(entity.Key);

                if (entity.Value.Id != null)
                    DocumentsById.Remove(entity.Value.Id);

                entity.Value.Document = document;

                var changeVector = UseOptimisticConcurrency &&
                                   entity.Value.ConcurrencyCheckMode != ConcurrencyCheckMode.Disabled ||
                           entity.Value.ConcurrencyCheckMode == ConcurrencyCheckMode.Forced
                    ? entity.Value.ChangeVector
                    : null;

                result.SessionCommands.Add(new PutCommandDataWithBlittableJson(entity.Value.Id, changeVector, document));
            }
        }

        private static void ThrowInvalidDeletedDocumentWithDeferredCommand(ICommandData resultCommand)
        {
            throw new InvalidOperationException(
                $"Cannot perform save because document {resultCommand.Id} has been deleted by the session and is also taking part in deferred {resultCommand.Type} command");
        }

        private static void ThrowInvalidModifiedDocumentWithDeferredCommand(ICommandData resultCommand)
        {
            throw new InvalidOperationException(
                $"Cannot perform save because document {resultCommand.Id} has been modified by the session and is also taking part in deferred {resultCommand.Type} command");
        }

        protected bool EntityChanged(BlittableJsonReaderObject newObj, DocumentInfo documentInfo, IDictionary<string, DocumentsChanges[]> changes)
        {
            return BlittableOperation.EntityChanged(newObj, documentInfo, changes);
        }

        public IDictionary<string, DocumentsChanges[]> WhatChanged()
        {
            var changes = new Dictionary<string, DocumentsChanges[]>();

            PrepareForEntitiesDeletion(null, changes);
            GetAllEntitiesChanges(changes);
            return changes;
        }

        /// <summary>
        /// Gets a value indicating whether any of the entities tracked by the session has changes.
        /// </summary>
        /// <value></value>
        public bool HasChanges
        {
            get
            {
                foreach (var entity in DocumentsByEntity)
                {
                    var document = EntityToBlittable.ConvertEntityToBlittable(entity.Key, entity.Value);
                    if (EntityChanged(document, entity.Value, null))
                    {
                        return true;
                    }
                }
                return DeletedEntities.Count > 0;
            }
        }

        /// <summary>
        /// Determines whether the specified entity has changed.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        /// 	<c>true</c> if the specified entity has changed; otherwise, <c>false</c>.
        /// </returns>
        public bool HasChanged(object entity)
        {
            DocumentInfo documentInfo;
            if (DocumentsByEntity.TryGetValue(entity, out documentInfo) == false)
                return false;
            var document = EntityToBlittable.ConvertEntityToBlittable(entity, documentInfo);
            return EntityChanged(document, documentInfo, null);
        }

        public void WaitForReplicationAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true,
            int replicas = 1, bool majority = false)
        {
            var realTimeout = timeout ?? TimeSpan.FromSeconds(15);
            if (_saveChangesOptions == null)
                _saveChangesOptions = new BatchOptions();
            _saveChangesOptions.WaitForReplicas = true;
            _saveChangesOptions.Majority = majority;
            _saveChangesOptions.NumberOfReplicasToWaitFor = replicas;
            _saveChangesOptions.WaitForReplicasTimeout = realTimeout;
            _saveChangesOptions.ThrowOnTimeoutInWaitForReplicas = throwOnTimeout;
        }

        public void WaitForIndexesAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = false,
            string[] indexes = null)
        {
            var realTimeout = timeout ?? TimeSpan.FromSeconds(15);
            if (_saveChangesOptions == null)
                _saveChangesOptions = new BatchOptions();
            _saveChangesOptions.WaitForIndexes = true;
            _saveChangesOptions.WaitForIndexesTimeout = realTimeout;
            _saveChangesOptions.ThrowOnTimeoutInWaitForIndexes = throwOnTimeout;
            _saveChangesOptions.WaitForSpecificIndexes = indexes;
        }

        private void GetAllEntitiesChanges(IDictionary<string, DocumentsChanges[]> changes)
        {
            foreach (var pair in DocumentsById)
            {
                UpdateMetadataModifications(pair.Value);
                var newObj = EntityToBlittable.ConvertEntityToBlittable(pair.Value.Entity, pair.Value);
                EntityChanged(newObj, pair.Value, changes);
            }
        }

        /// <summary>
        /// Mark the entity as one that should be ignore for change tracking purposes,
        /// it still takes part in the session, but is ignored for SaveChanges.
        /// </summary>
        public void IgnoreChangesFor(object entity)
        {
            GetDocumentInfo(entity).IgnoreChanges = true;
        }

        /// <summary>
        /// Evicts the specified entity from the session.
        /// Remove the entity from the delete queue and stops tracking changes for this entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">The entity.</param>
        public void Evict<T>(T entity)
        {
            DocumentInfo documentInfo;
            if (DocumentsByEntity.TryGetValue(entity, out documentInfo))
            {
                DocumentsByEntity.Remove(entity);
                DocumentsById.Remove(documentInfo.Id);
            }
            DeletedEntities.Remove(entity);
        }

        /// <summary>
        /// Clears this instance.
        /// Remove all entities from the delete queue and stops tracking changes for all entities.
        /// </summary>
        public void Clear()
        {
            DocumentsByEntity.Clear();
            DeletedEntities.Clear();
            DocumentsById.Clear();
            KnownMissingIds.Clear();
            IncludedDocumentsById.Clear();
        }

        /// <summary>
        ///     Defer commands to be executed on SaveChanges()
        /// </summary>
        /// <param name="command">Command to be executed</param>
        /// <param name="commands">Array of commands to be executed.</param>
        public void Defer(ICommandData command, params ICommandData[] commands)
        {
            // The signature here is like this in order to avoid calling 'Defer()' without any parameter.

            DeferredCommands.Add(command);
            DeferInternal(command);

            if (commands != null)
                Defer(commands);
        }

        /// <summary>
        /// Defer commands to be executed on SaveChanges()
        /// </summary>
        /// <param name="commands">The commands to be executed</param>
        public void Defer(ICommandData[] commands)
        {
            DeferredCommands.AddRange(commands);
            foreach (var command in commands)
            {
                DeferInternal(command);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeferInternal(ICommandData command)
        {
            DeferredCommandsDictionary[(command.Id, command.Type, command.Name)] = command;
            DeferredCommandsDictionary[(command.Id, CommandType.ClientAnyCommand, null)] = command;
            if (command.Type != CommandType.AttachmentPUT)
                DeferredCommandsDictionary[(command.Id, CommandType.ClientNotAttachmentPUT, null)] = command;
        }

        private void Dispose(bool isDisposing)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (isDisposing && RunningOn.FinalizerThread == false)
            {
                GC.SuppressFinalize(this);

                _releaseOperationContext.Dispose();
            }
            else
            {
                // when we are disposed from the finalizer then we have to dispose the context immediately instead of returning it to the pool because
                // the finalizer of ArenaMemoryAllocator could be already called so we cannot return such context to the pool (RavenDB-7571)

                Context.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
        }

        ~InMemoryDocumentSessionOperations()
        {
            try
            {
                Dispose(false);
            }
            catch (ObjectDisposedException)
            {
                // nothing can be done here
            }
#if DEBUG
            Debug.WriteLine("Disposing a session for finalizer! It should be disposed by calling session.Dispose()!");
#endif
        }

        public void RegisterMissing(string id)
        {
            KnownMissingIds.Add(id);
        }

        public void UnregisterMissing(string id)
        {
            KnownMissingIds.Remove(id);
        }

        internal void RegisterIncludes(BlittableJsonReaderObject includes)
        {
            if (includes == null)
                return;

            var propertyDetails = new BlittableJsonReaderObject.PropertyDetails();
            foreach (var propertyIndex in includes.GetPropertiesByInsertionOrder())
            {
                includes.GetPropertyByIndex(propertyIndex, ref propertyDetails);

                if (propertyDetails.Value == null)
                    continue;

                var json = (BlittableJsonReaderObject)propertyDetails.Value;

                var newDocumentInfo = DocumentInfo.GetNewDocumentInfo(json);
                if (newDocumentInfo.Metadata.TryGetConflict(out var conflict) && conflict)
                    continue;

                IncludedDocumentsById[newDocumentInfo.Id] = newDocumentInfo;
            }
        }

        public void RegisterMissingIncludes(BlittableJsonReaderArray results, BlittableJsonReaderObject includes, ICollection<string> includePaths)
        {
            if (includePaths == null || includePaths.Count == 0)
                return;

            foreach (BlittableJsonReaderObject result in results)
            {
                foreach (var include in includePaths)
                {
                    if (include == Constants.Documents.Indexing.Fields.DocumentIdFieldName)
                        continue;

                    IncludesUtil.Include(result, include, id =>
                    {
                        if (id == null)
                            return;

                        if (IsLoaded(id))
                            return;

                        if (includes.TryGet(id, out BlittableJsonReaderObject document))
                        {
                            var metadata = document.GetMetadata();
                            if (metadata.TryGetConflict(out var conflict) && conflict)
                                return;
                        }

                        RegisterMissing(id);
                    });
                }
            }
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(obj, this);
        }

        internal void HandleInternalMetadata(BlittableJsonReaderObject result)
        {
            // Implant a property with "id" value ... if it doesn't exist
            BlittableJsonReaderObject metadata;
            string id;
            if (result.TryGet(Constants.Documents.Metadata.Key, out metadata) == false ||
                metadata.TryGet(Constants.Documents.Metadata.Id, out id) == false)
            {
                // if the item doesn't have meta data, then nested items might have, so we need to check them
                var propDetail = new BlittableJsonReaderObject.PropertyDetails();
                for (int index = 0; index < result.Count; index++)
                {
                    result.GetPropertyByIndex(index, ref propDetail, addObjectToCache: true);
                    var jsonObj = propDetail.Value as BlittableJsonReaderObject;
                    if (jsonObj != null)
                    {
                        HandleInternalMetadata(jsonObj);
                        continue;
                    }

                    var jsonArray = propDetail.Value as BlittableJsonReaderArray;
                    if (jsonArray != null)
                    {
                        HandleInternalMetadata(jsonArray);
                    }
                }
                return;
            }

            string entityName;
            if (metadata.TryGet(Constants.Documents.Metadata.Collection, out entityName) == false)
                return;

            var idPropName = Conventions.FindIdentityPropertyNameFromEntityName(entityName);

            result.Modifications = new DynamicJsonValue
            {
                [idPropName] = id // this is being read by BlittableJsonReader for additional properties on the object
            };
        }

        internal void HandleInternalMetadata(BlittableJsonReaderArray values)
        {
            foreach (var nested in values)
            {
                var bObject = nested as BlittableJsonReaderObject;
                if (bObject != null)
                    HandleInternalMetadata(bObject);
                var bArray = nested as BlittableJsonReaderArray;
                if (bArray == null)
                    continue;
                HandleInternalMetadata(bArray);
            }
        }

        public object DeserializeFromTransformer(Type entityType, string id, BlittableJsonReaderObject document)
        {
            HandleInternalMetadata(document);
            return EntityToBlittable.ConvertToEntity(entityType, id, document);
        }

        public bool CheckIfIdAlreadyIncluded(string[] ids, KeyValuePair<string, Type>[] includes)
        {
            return CheckIfIdAlreadyIncluded(ids, includes.Select(x => x.Key));
        }

        public bool CheckIfIdAlreadyIncluded(string[] ids, IEnumerable<string> includes)
        {
            foreach (var id in ids)
            {
                if (KnownMissingIds.Contains(id))
                    continue;

                // Check if document was already loaded, the check if we've received it through include
                if (DocumentsById.TryGetValue(id, out DocumentInfo documentInfo) == false &&
                    IncludedDocumentsById.TryGetValue(id, out documentInfo) == false)
                    return false;

                if (documentInfo.Entity == null)
                    return false;

                if (includes == null)
                    continue;

                foreach (var include in includes)
                {
                    var hasAll = true;
                    IncludesUtil.Include(documentInfo.Document, include, s =>
                    {
                        hasAll &= IsLoaded(s);
                    });

                    if (hasAll == false)
                        return false;
                }
            }
            return true;
        }

        protected void RefreshInternal<T>(T entity, RavenCommand<GetDocumentResult> cmd, DocumentInfo documentInfo)
        {
            var document = (BlittableJsonReaderObject)cmd.Result.Results[0];
            if (document == null)
                throw new InvalidOperationException("Document '" + documentInfo.Id +
                                                    "' no longer exists and was probably deleted");

            document.TryGetMember(Constants.Documents.Metadata.Key, out object value);
            documentInfo.Metadata = value as BlittableJsonReaderObject;

            document.TryGetMember(Constants.Documents.Metadata.ChangeVector, out var changeVector);
            documentInfo.ChangeVector = changeVector as string;

            documentInfo.Document = document;

            documentInfo.Entity = ConvertToEntity(typeof(T), documentInfo.Id, document);

            var type = entity.GetType();
            foreach (var property in ReflectionUtil.GetPropertiesAndFieldsFor(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var prop = property;
                if (prop.DeclaringType != type && prop.DeclaringType != null)
                    prop = prop.DeclaringType.GetProperty(prop.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? property;

                if (!prop.CanWrite() || !prop.CanRead() || prop.GetIndexParameters().Length != 0)
                    continue;
                prop.SetValue(entity, prop.GetValue(documentInfo.Entity));
            }
        }

        protected static T GetOperationResult<T>(object result)
        {
            if (result == null)
                return default(T);

            if (result is T)
                return (T)result;

            var resultsArray = result as T[];
            if (resultsArray != null && resultsArray.Length > 0)
                return resultsArray[0];

            var resultsDictionary = result as Dictionary<string, T>;
            if (resultsDictionary != null)
            {
                if (resultsDictionary.Count == 0)
                    return default(T);

                if (resultsDictionary.Count == 1)
                    return resultsDictionary.Values.FirstOrDefault();
            }

            throw new InvalidCastException($"Unable to cast {result.GetType().Name} to {typeof(T).Name}");
        }

        public enum ConcurrencyCheckMode
        {
            /// <summary>
            /// Automatic optimistic concurrency check depending on UseOptimisticConcurrency setting or provided Change Vector
            /// </summary>
            Auto,

            /// <summary>
            /// Force optimistic concurrency check even if UseOptimisticConcurrency is not set
            /// </summary>
            Forced,

            /// <summary>
            /// Disable optimistic concurrency check even if UseOptimisticConcurrency is set
            /// </summary>
            Disabled
        }

        /// <summary>
        /// Data for a batch command to the server
        /// </summary>
        public class SaveChangesData
        {
            public readonly List<ICommandData> DeferredCommands;
            public readonly Dictionary<(string, CommandType, string), ICommandData> DeferredCommandsDictionary;
            public readonly List<ICommandData> SessionCommands = new List<ICommandData>();
            public readonly List<object> Entities = new List<object>();
            public readonly BatchOptions Options;

            public SaveChangesData(InMemoryDocumentSessionOperations session)
            {
                DeferredCommands = new List<ICommandData>(session.DeferredCommands);
                DeferredCommandsDictionary = new Dictionary<(string, CommandType, string), ICommandData>(session.DeferredCommandsDictionary);
                Options = session._saveChangesOptions;
            }
        }

        public void OnAfterStoreInvoke(AfterStoreEventArgs afterStoreEventArgs)
        {
            OnAfterStore?.Invoke(this, afterStoreEventArgs);
        }

        public void OnBeforeQueryExecutedInvoke(BeforeQueryExecutedEventArgs beforeQueryExecutedEventArgs)
        {
            OnBeforeQueryExecuted?.Invoke(this, beforeQueryExecutedEventArgs);
        }

        protected (string IndexName, string CollectionName) ProcessQueryParameters(Type type, string indexName, string collectionName, DocumentConventions conventions)
        {
            var isIndex = string.IsNullOrWhiteSpace(indexName) == false;
            var isCollection = string.IsNullOrWhiteSpace(collectionName) == false;

            if (isIndex && isCollection)
                throw new InvalidOperationException($"Parameters '{nameof(indexName)}' and '{nameof(collectionName)}' are mutually exclusive. Please specify only one of them.");

            if (isIndex == false && isCollection == false)
                collectionName = Conventions.GetCollectionName(type);

            return (indexName, collectionName);
        }
    }

    /// <summary>
    /// Information held about an entity by the session
    /// </summary>
    public class DocumentInfo
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the ChangeVector.
        /// </summary>
        /// <value>The ChangeVector.</value>
        public string ChangeVector { get; set; }

        /// <summary>
        /// A concurrency check will be forced on this entity 
        /// even if UseOptimisticConcurrency is set to false
        /// </summary>
        public InMemoryDocumentSessionOperations.ConcurrencyCheckMode ConcurrencyCheckMode { get; set; }

        /// <summary>
        /// If set to true, the session will ignore this document
        /// when SaveChanges() is called, and won't perform and change tracking
        /// </summary>
        public bool IgnoreChanges { get; set; }

        public BlittableJsonReaderObject Metadata { get; set; }

        public BlittableJsonReaderObject Document { get; set; }

        public IMetadataDictionary MetadataInstance { get; set; }

        public object Entity { get; set; }

        public bool IsNewDocument { get; set; }

        public string Collection { get; set; }

        public static DocumentInfo GetNewDocumentInfo(BlittableJsonReaderObject document)
        {
            if (document.TryGet(Constants.Documents.Metadata.Key, out BlittableJsonReaderObject metadata) == false)
                throw new InvalidOperationException("Document must have a metadata");
            if (metadata.TryGet(Constants.Documents.Metadata.Id, out string id) == false)
                throw new InvalidOperationException("Document must have an id");
            if (metadata.TryGet(Constants.Documents.Metadata.ChangeVector, out string changeVector) == false)
                throw new InvalidOperationException("Document " + id + " must have an Change Vector");

            var newDocumentInfo = new DocumentInfo
            {
                Id = id,
                Document = document,
                Metadata = metadata,
                Entity = null,
                ChangeVector = changeVector
            };
            return newDocumentInfo;
        }
    }
}
