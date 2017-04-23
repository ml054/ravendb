﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lucene.Net.Search;
using Raven.Client;
using Raven.Server.Documents.Queries.Parse;
using Raven.Server.Documents.Queries.Results;
using Raven.Server.Documents.Queries.Sorting;
using Raven.Server.ServerWide.Context;
using Voron;

namespace Raven.Server.Documents.Queries
{
    public class CollectionQueryEnumerable : IEnumerable<Document>
    {
        private readonly DocumentsStorage _documents;
        private readonly FieldsToFetch _fieldsToFetch;
        private readonly DocumentsOperationContext _context;
        private readonly string _collection;
        private readonly IndexQueryServerSide _query;
        private readonly bool _isAllDocsCollection;

        public CollectionQueryEnumerable(DocumentsStorage documents, FieldsToFetch fieldsToFetch, string collection, IndexQueryServerSide query, DocumentsOperationContext context)
        {
            _documents = documents;
            _fieldsToFetch = fieldsToFetch;
            _collection = collection;
            _isAllDocsCollection = collection == Constants.Documents.Indexing.AllDocumentsCollection;
            _query = query;
            _context = context;
        }

        public IEnumerator<Document> GetEnumerator()
        {
            return new Enumerator(_documents, _fieldsToFetch, _collection, _isAllDocsCollection, _query, _context);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Enumerator : IEnumerator<Document>
        {
            private static readonly char[] InSeparator = { ',' };
            private static readonly string InPrefix = $"@in<{Constants.Documents.Indexing.Fields.DocumentIdFieldName}>:";
            private static readonly string EqualPrefix = $"{Constants.Documents.Indexing.Fields.DocumentIdFieldName}:";

            private readonly DocumentsStorage _documents;
            private readonly FieldsToFetch _fieldsToFetch;
            private readonly DocumentsOperationContext _context;
            private readonly string _collection;
            private readonly bool _isAllDocsCollection;
            private readonly IndexQueryServerSide _query;

            private bool _initialized;

            private int _returnedResults;

            private readonly HashSet<ulong> _alreadySeenProjections;
            private int _start;
            private IEnumerator<Document> _inner;
            private int _innerCount;
            private readonly List<Slice> _ids;
            private readonly Sort _sort;

            public Enumerator(DocumentsStorage documents, FieldsToFetch fieldsToFetch, string collection, bool isAllDocsCollection, IndexQueryServerSide query, DocumentsOperationContext context)
            {
                _documents = documents;
                _fieldsToFetch = fieldsToFetch;
                _collection = collection;
                _isAllDocsCollection = isAllDocsCollection;
                _query = query;
                _context = context;

                if (_fieldsToFetch.IsDistinct)
                    _alreadySeenProjections = new HashSet<ulong>();

                _ids = ExtractIdsFromQuery(query);

                if (_ids != null && _ids.Count > BooleanQuery.MaxClauseCount)
                    throw new BooleanQuery.TooManyClauses();

                _sort = ExtractSortFromQuery(query);
            }

            private static Sort ExtractSortFromQuery(IndexQueryServerSide query)
            {
                if (query.SortedFields == null || query.SortedFields.Length == 0)
                    return null;

                Debug.Assert(query.SortedFields.Length == 1);

                var randomField = query.SortedFields[0];

                Debug.Assert(randomField.Field.StartsWith(Constants.Documents.Indexing.Fields.RandomFieldName));

                var customFieldName = SortFieldHelper.ExtractName(randomField.Field);

                if (customFieldName.IsNullOrWhiteSpace())
                    return new Sort(null);

                return new Sort(customFieldName);
            }

            private List<Slice> ExtractIdsFromQuery(IndexQueryServerSide query)
            {
                if (string.IsNullOrWhiteSpace(query.Query))
                    return null;

                return SimpleQueryParser.GetTermValuesForField(query, Constants.Documents.Indexing.Fields.DocumentIdFieldName)
                    .Select(id =>
                    {
                        Slice key;
                        Slice.From(_context.Allocator, id, out key);
                        _context.Allocator.ToLowerCase(ref key.Content);

                        return key;
                    })
                    .OrderBy(x => x, SliceComparer.Instance)
                    .ToList();
            }

            public bool MoveNext()
            {
                if (_initialized == false)
                    _start = Initialize();

                while (true)
                {
                    if (_inner == null)
                    {
                        _inner = GetDocuments().GetEnumerator();
                        _innerCount = 0;
                    }

                    if (_inner.MoveNext() == false)
                    {
                        Current = null;

                        if (_returnedResults >= _query.PageSize)
                            return false;

                        if (_innerCount < _query.PageSize)
                            return false;

                        _start += _query.PageSize;
                        _inner = null;
                        continue;
                    }

                    _innerCount++;

                    var doc = _fieldsToFetch.IsProjection
                        ? MapQueryResultRetriever.GetProjectionFromDocument(_inner.Current, 0f, _fieldsToFetch, _context)
                        : _inner.Current;

                    if (_query.SkipDuplicateChecking || _fieldsToFetch.IsDistinct == false)
                    {
                        _returnedResults++;
                        Current = doc;
                        return true;
                    }

                    if (_alreadySeenProjections.Add(doc.DataHash))
                    {
                        _returnedResults++;
                        Current = doc;
                        return true;
                    }

                    if (_returnedResults >= _query.PageSize)
                        return false;
                }
            }

            private IEnumerable<Document> GetDocuments()
            {
                IEnumerable<Document> documents;
                if (_ids != null && _ids.Count > 0)
                    documents = _documents.GetDocuments(_context, _ids, _start, _query.PageSize);
                else if (_isAllDocsCollection)
                    documents = _documents.GetDocumentsFrom(_context, 0, _start, _query.PageSize);
                else
                    documents = _documents.GetDocumentsFrom(_context, _collection, 0, _start, _query.PageSize);

                return ApplySorting(documents);
            }

            private IEnumerable<Document> ApplySorting(IEnumerable<Document> documents)
            {
                if (_sort == null)
                    return documents;

                return documents
                    .OrderBy(x => _sort.Next());
            }

            private int Initialize()
            {
                _initialized = true;

                if (_query.Start == 0)
                    return 0;

                if (_query.SkipDuplicateChecking)
                    return _query.Start;

                if (_fieldsToFetch.IsDistinct == false)
                    return _query.Start;

                var start = 0;
                while (true)
                {
                    var count = 0;
                    foreach (var document in ApplySorting(_documents.GetDocumentsFrom(_context, _collection, 0, start, _query.PageSize)))
                    {
                        count++;

                        var doc = _fieldsToFetch.IsProjection
                            ? MapQueryResultRetriever.GetProjectionFromDocument(document, 0f, _fieldsToFetch, _context)
                            : _inner.Current;

                        if (doc.Data.Count <= 0)
                            continue;

                        if (_alreadySeenProjections.Add(doc.DataHash) && _alreadySeenProjections.Count == _query.Start)
                            break;
                    }

                    if (_alreadySeenProjections.Count == _query.Start)
                        break;

                    if (count < _query.PageSize)
                        break;

                    start += count;
                }

                return start;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public Document Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                if (_ids != null)
                {
                    foreach (var id in _ids)
                    {
                        id.Release(_context.Allocator);
                    }
                }
            }

            private class Sort
            {
                private readonly Random _random;

                public Sort(string field)
                {
                    if (string.IsNullOrWhiteSpace(field))
                        field = Guid.NewGuid().ToString();

                    _random = new Random(field.GetHashCode());
                }

                public int Next()
                {
                    return _random.Next();
                }
            }
        }
    }
}