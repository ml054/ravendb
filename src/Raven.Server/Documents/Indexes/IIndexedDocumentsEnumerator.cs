﻿using System;
using System.Collections;

namespace Raven.Server.Documents.Indexes
{
    public interface IIndexedDocumentsEnumerator : IDisposable
    {
        bool MoveNext(out IEnumerable resultsOfCurrentDocument);

        void OnError();

        Document Current { get; }
    }
}