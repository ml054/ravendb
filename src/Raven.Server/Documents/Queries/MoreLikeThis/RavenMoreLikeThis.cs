﻿using System.Collections.Generic;
using System.IO;

using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Raven.Server.Documents.Queries.MoreLikeThis
{
    internal class RavenMoreLikeThis : Lucene.Net.Search.Similar.MoreLikeThis
    {
        private readonly IndexReader _ir;
        private readonly IState _state;

        public RavenMoreLikeThis(IndexReader ir, MoreLikeThisQueryServerSide query, IState state)
            : base(ir, state)
        {
            _ir = ir;
            _state = state;

            if (query.Boost != null)
                Boost = query.Boost.Value;

            if (query.BoostFactor != null)
                BoostFactor = query.BoostFactor.Value;

            if (query.MaximumNumberOfTokensParsed != null)
                MaxNumTokensParsed = query.MaximumNumberOfTokensParsed.Value;

            if (query.MaximumQueryTerms != null)
                MaxQueryTerms = query.MaximumQueryTerms.Value;

            if (query.MinimumWordLength != null)
                MinWordLen = query.MinimumWordLength.Value;

            if (query.MaximumWordLength != null)
                MaxWordLen = query.MaximumWordLength.Value;

            if (query.MinimumTermFrequency != null)
                MinTermFreq = query.MinimumTermFrequency.Value;

            if (query.MinimumDocumentFrequency != null)
                MinDocFreq = query.MinimumDocumentFrequency.Value;

            if (query.MaximumDocumentFrequency != null)
                MaxDocFreq = query.MaximumDocumentFrequency.Value;

            if (query.MaximumDocumentFrequencyPercentage != null)
                SetMaxDocFreqPct(query.MaximumDocumentFrequencyPercentage.Value);
        }

        protected override PriorityQueue<object[]> RetrieveTerms(int docNum)
        {
            var fieldNames = GetFieldNames();

            IDictionary<string, Int> termFreqMap = new Lucene.Net.Support.HashMap<string, Int>();

            foreach (var fieldName in fieldNames)
            {
                var vector = _ir.GetTermFreqVector(docNum, fieldName, _state);

                // field does not store term vector info
                if (vector == null)
                {
                    var d = _ir.Document(docNum, _state);
                    var text = d.GetValues(fieldName, _state);
                    if (text != null)
                    {
                        foreach (var t in text)
                        {
                            AddTermFrequencies(new StringReader(t), termFreqMap, fieldName);
                        }
                    }
                }
                else
                {
                    AddTermFrequencies(termFreqMap, vector);
                }
            }

            return CreateQueue(termFreqMap);
        }
    }
}
