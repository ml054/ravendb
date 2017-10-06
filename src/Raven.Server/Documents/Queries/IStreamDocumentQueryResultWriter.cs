﻿using System;

namespace Raven.Server.Documents.Queries
{
    public interface IStreamDocumentQueryResultWriter : IStreamQueryResultWriter<Document>
    {
    }

    public interface IStreamQueryResultWriter<T> : IDisposable
    {
        void StartResponse();
        void StartResults();
        void EndResults();
        void AddResult(T res);
        void EndResponse();
        void WriteError(Exception e);
        void WriteError(string error);
        void WriteQueryStatistics(long resultEtag, bool isStale, string indexName, long totalResults, DateTime timestamp);
        bool SupportError { get; }
        bool SupportStatistics { get; }
    }
}
