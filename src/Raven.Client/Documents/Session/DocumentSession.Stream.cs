using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session.Operations;
using Raven.Client.Documents.Transformers;
using Raven.Client.Extensions;
using Raven.Client.Json;
using Sparrow.Json;

namespace Raven.Client.Documents.Session
{
    public partial class DocumentSession
    {
        public IEnumerator<StreamResult<T>> Stream<T>(IQueryable<T> query)
        {
            var queryProvider = (IRavenQueryProvider)query.Provider;
            var docQuery = queryProvider.ToDocumentQuery<T>(query.Expression);
            return Stream(docQuery);
        }

        public IEnumerator<StreamResult<T>> Stream<T>(IQueryable<T> query, out StreamQueryStatistics streamQueryStats)
        {
            var queryProvider = (IRavenQueryProvider)query.Provider;
            var docQuery = queryProvider.ToDocumentQuery<T>(query.Expression);
            return Stream(docQuery, out streamQueryStats);
        }

        public IEnumerator<StreamResult<T>> Stream<T>(IDocumentQuery<T> query)
        {
            var streamOperation = new StreamOperation(this);
            var command = streamOperation.CreateRequest(query.IndexName, query.GetIndexQuery());

            RequestExecutor.Execute(command, Context);
            using (var result = streamOperation.SetResult(command.Result))
            {
                return YieldResults(query, result, command.UsedTransformer);
            }
        }

        public IEnumerator<StreamResult<T>> Stream<T>(IDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats)
        {
            var stats = new StreamQueryStatistics();
            var streamOperation = new StreamOperation(this, stats);
            var command = streamOperation.CreateRequest(query.IndexName, query.GetIndexQuery());

            RequestExecutor.Execute(command, Context);
            using (var result = streamOperation.SetResult(command.Result))
            {
                streamQueryStats = stats;

                return YieldResults(query, result, command.UsedTransformer);
            }
        }

        private IEnumerator<StreamResult<T>> YieldResults<T>(IDocumentQuery<T> query, IEnumerator<BlittableJsonReaderObject> enumerator, bool usedTransformer)
        {
            var projections = ((DocumentQuery<T>)query).ProjectionFields;

            while (enumerator.MoveNext())
            {
                var json = enumerator.Current;
                query.InvokeAfterStreamExecuted(json);

                if (usedTransformer)
                {
                    foreach (var streamResult in CreateMultipleStreamResults<T>(json))
                        yield return streamResult;

                    continue;
                }

                yield return CreateStreamResult<T>(json, projections);
            }
        }

        public void StreamInto<T>(IDocumentQuery<T> query, Stream output)
        {
            var streamOperation = new StreamOperation(this);
            var command = streamOperation.CreateRequest(query.IndexName, query.GetIndexQuery());

            RequestExecutor.Execute(command, Context);

            using (command.Result.Response)
            using (command.Result.Stream)
            {
                command.Result.Stream.CopyTo(output);
            }
        }

        private IEnumerable<StreamResult<T>> CreateMultipleStreamResults<T>(BlittableJsonReaderObject json)
        {
            BlittableJsonReaderArray values;
            if (json.TryGet(Constants.Json.Fields.Values, out values) == false)
                throw new InvalidOperationException("Transformed document must have a $values property");

            var metadata = json.GetMetadata();
            var etag = metadata.GetEtag();
            var id = metadata.GetId();

            foreach (var value in TransformerHelper.ParseResultsForStreamOperation<T>(this, values))
            {
                yield return new StreamResult<T>
                {
                    Key = id,
                    Etag = etag,
                    Document = value,
                    Metadata = new MetadataAsDictionary(metadata)
                };
            }
        }

        private StreamResult<T> CreateStreamResult<T>(BlittableJsonReaderObject json, string[] projectionFields)
        {
            var metadata = json.GetMetadata();
            var etag = metadata.GetEtag();
            var id = metadata.GetId();

            //TODO - Investigate why ConvertToEntity fails if we don't call ReadObject before
            json = Context.ReadObject(json, id);
            var entity = QueryOperation.Deserialize<T>(id, json, metadata, projectionFields, true, this);

            var streamResult = new StreamResult<T>
            {
                Etag = etag,
                Key = id,
                Document = entity,
                Metadata = new MetadataAsDictionary(metadata)
            };
            return streamResult;
        }

        public IEnumerator<StreamResult<T>> Stream<T>(long? fromEtag, int start = 0, int pageSize = Int32.MaxValue,
             string transformer = null,
            Dictionary<string, object> transformerParameters = null)
        {
            return Stream<T>(fromEtag: fromEtag, startsWith: null, matches: null, start: start, pageSize: pageSize,
                startAfter: null, transformer: transformer, transformerParameters: transformerParameters);
        }

        public IEnumerator<StreamResult<T>> Stream<T>(string startsWith, string matches = null, int start = 0, int pageSize = int.MaxValue,
             string startAfter = null, string transformer = null,
            Dictionary<string, object> transformerParameters = null)
        {
            return Stream<T>(fromEtag: null, startsWith: startsWith, matches: matches, start: start, pageSize: pageSize,
                startAfter: startAfter, transformer: transformer, transformerParameters: transformerParameters);
        }

        private IEnumerator<StreamResult<T>> Stream<T>(long? fromEtag, string startsWith, string matches, int start, int pageSize,
            string startAfter, string transformer, Dictionary<string, object> transformerParameters)
        {
            var streamOperation = new StreamOperation(this);
            var command = streamOperation.CreateRequest(fromEtag, startsWith, matches, start, pageSize, null, startAfter, transformer,
                transformerParameters);
            RequestExecutor.Execute(command, Context);
            using (var result = streamOperation.SetResult(command.Result))
            {
                while (result.MoveNext())
                {
                    var json = result.Current;

                    if (command.UsedTransformer)
                    {
                        foreach (var streamResult in CreateMultipleStreamResults<T>(json))
                            yield return streamResult;

                        continue;
                    }

                    yield return CreateStreamResult<T>(json, null);
                }
            }
        }
    }

}