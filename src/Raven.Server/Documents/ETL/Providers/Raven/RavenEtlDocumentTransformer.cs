﻿using System;
using System.Collections.Generic;
using Jint.Native;
using Raven.Client;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide.ETL;
using Raven.Server.Documents.Patch;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

// ReSharper disable ForCanBeConvertedToForeach

namespace Raven.Server.Documents.ETL.Providers.Raven
{
    public class RavenEtlDocumentTransformer : EtlTransformer<RavenEtlItem, ICommandData>
    {
        private readonly ScriptInput _script;
        private readonly List<ICommandData> _commands = new List<ICommandData>();

        public RavenEtlDocumentTransformer(DocumentDatabase database, DocumentsOperationContext context, ScriptInput script) 
            : base(database, context, script.Transformation)
        {
            _script = script;

            LoadToDestinations = _script.Transformation == null ? new string[0] : _script.LoadToCollections;
        }

        protected override string[] LoadToDestinations { get; }

        protected override void LoadToFunction(string collectionName, ScriptRunnerResult document)
        {
            if (collectionName == null)
                ThrowLoadParameterIsMandatory(nameof(collectionName));
            string id;

            if (_script.IsLoadedToDefaultCollection(Current, collectionName))
            {
                id = Current.DocumentId;
            }
            else
            {
                id = GetPrefixedId(Current.DocumentId, collectionName, OperationType.Put);

                var metadata = document.GetOrCreate(Constants.Documents.Metadata.Key);
                metadata.Put(Constants.Documents.Metadata.Collection, collectionName, false);
                metadata.Put(Constants.Documents.Metadata.Id, id, false);
            }

            var transformed = document.TranslateToObject(Context);

            var transformResult = Context.ReadObject(transformed, id);

            _commands.Add(new PutCommandDataWithBlittableJson(id, null, transformResult));
        }

        private string GetPrefixedId(LazyStringValue documentId, string loadCollectionName, OperationType type)
        {
            var prefixEnding = type == OperationType.Put ? "|" : (type == OperationType.Delete ? "/" : ThrowUnknownOperationType(type));

            return $"{documentId}/{_script.IdPrefixForCollection[loadCollectionName]}{prefixEnding}";
        }

        public override IEnumerable<ICommandData> GetTransformedResults()
        {
            return _commands;
        }

        public override void Transform(RavenEtlItem item)
        {
            Current = item;

            if (item.IsDelete == false)
            {
                if (_script.Transformation != null)
                {
                    if (_script.LoadToCollections.Length > 1 || _script.IsLoadedToDefaultCollection(item, _script.LoadToCollections[0]) == false)
                    {
                        // first, we need to delete docs prefixed by modified document ID to properly handle updates of 
                        // documents loaded to non default collections

                        ApplyDeleteCommands(item, OperationType.Put);
                    }

                    SingleRun.Run(Context, "execute", new object[] {Current.Document}).Dispose();
                }
                else
                    _commands.Add(new PutCommandDataWithBlittableJson(item.DocumentId, null, item.Document.Data));
            }
            else
            {
                if (_script.Transformation != null)
                    ApplyDeleteCommands(item, OperationType.Delete);
                else
                    _commands.Add(new DeleteCommandData(item.DocumentId, null));
            }
        }

        private void ApplyDeleteCommands(RavenEtlItem item, OperationType operation)
        {
            for (var i = 0; i < _script.LoadToCollections.Length; i++)
            {
                var collection = _script.LoadToCollections[i];

                if (_script.IsLoadedToDefaultCollection(item, collection))
                {
                    if (operation == OperationType.Delete)
                        _commands.Add(new DeleteCommandData(item.DocumentId, null));
                }
                else
                    _commands.Add(new DeletePrefixedCommandData(GetPrefixedId(item.DocumentId, collection, OperationType.Delete)));
            }
        }

        private static string ThrowUnknownOperationType(OperationType type)
        {
            throw new ArgumentException($"Unknown opearation: {type}");
        }

        public class ScriptInput
        {
            private readonly Dictionary<string, Dictionary<string, bool>> _collectionNameComparisons;

            public readonly string[] LoadToCollections = new string[0];

            public readonly PatchRequest Transformation;

            public readonly HashSet<string> DefaultCollections;

            public readonly Dictionary<string, string> IdPrefixForCollection = new Dictionary<string, string>();

            public ScriptInput(Transformation transformation)
            {
                DefaultCollections = new HashSet<string>(transformation.Collections, StringComparer.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(transformation.Script))
                    return;

                Transformation = new PatchRequest(transformation.Script, PatchRequestType.RavenEtl);

                LoadToCollections = transformation.GetCollectionsFromScript();

                foreach (var collection in LoadToCollections)
                {
                    IdPrefixForCollection[collection] = DocumentConventions.DefaultTransformCollectionNameToDocumentIdPrefix(collection);
                }

                if (transformation.Collections == null)
                    return;

                _collectionNameComparisons = new Dictionary<string, Dictionary<string, bool>>(transformation.Collections.Count);

                foreach (var sourceCollection in transformation.Collections)
                {
                    _collectionNameComparisons[sourceCollection] = new Dictionary<string, bool>(transformation.Collections.Count);

                    foreach (var loadToCollection in LoadToCollections)
                    {
                        _collectionNameComparisons[sourceCollection][loadToCollection] = string.Compare(sourceCollection, loadToCollection, StringComparison.OrdinalIgnoreCase) == 0;
                    }
                }
            }

            public bool IsLoadedToDefaultCollection(RavenEtlItem item, string loadToCollection)
            {
                if (item.Collection != null)
                    return _collectionNameComparisons[item.Collection][loadToCollection];

                var collection = item.CollectionFromMetadata;

                return collection?.CompareTo(loadToCollection) == 0;
            }
        }

        private enum OperationType
        {
            Put,
            Delete
        }
    }
}
