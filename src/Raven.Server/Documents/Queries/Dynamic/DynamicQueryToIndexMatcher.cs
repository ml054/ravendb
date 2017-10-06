﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Raven.Client.Documents.Indexes;
using Raven.Server.Documents.Indexes;
using Raven.Server.Documents.Indexes.Auto;
using Raven.Server.Documents.Indexes.MapReduce.Auto;

namespace Raven.Server.Documents.Queries.Dynamic
{
    public class DynamicQueryMatchResult
    {
        public string IndexName { get; set; }
        public DynamicQueryMatchType MatchType { get; set; }

        public DynamicQueryMatchResult(string match, DynamicQueryMatchType matchType)
        {
            IndexName = match;
            MatchType = matchType;
        }

        public long LastMappedEtag { get; set; }

        public long NumberOfMappedFields { get; set; }
    }

    public enum DynamicQueryMatchType
    {
        Complete,
        Partial,
        Failure
    }

    public class DynamicQueryToIndexMatcher
    {
        private readonly IndexStore _indexStore;

        public DynamicQueryToIndexMatcher(IndexStore indexStore)
        {
            _indexStore = indexStore;
        }

        public class Explanation
        {
            public Explanation(string index, string reason)
            {
                Index = index;
                Reason = reason;
            }

            public string Index { get; }
            public string Reason { get; }
        }

        public DynamicQueryMatchResult Match(DynamicQueryMapping query, List<Explanation> explanations = null)
        {
            var definitions = _indexStore.GetIndexesForCollection(query.ForCollection)
                .Where(x => x.Type.IsAuto() && (query.IsGroupBy ? x.Type.IsMapReduce() : x.Type.IsMap()))
                .Select(x => x.Definition as AutoIndexDefinitionBase)
                .ToList();

            if (definitions.Count == 0)
                return new DynamicQueryMatchResult(string.Empty, DynamicQueryMatchType.Failure);

            var results = definitions.Select(definition => ConsiderUsageOfIndex(query, definition, explanations))
                    .Where(result => result.MatchType != DynamicQueryMatchType.Failure)
                    .GroupBy(x => x.MatchType)
                    .ToDictionary(x => x.Key, x => x.ToArray());

            if (results.TryGetValue(DynamicQueryMatchType.Complete, out DynamicQueryMatchResult[] matchResults) && matchResults.Length > 0)
            {
                var prioritizedResults = matchResults
                    .OrderByDescending(x => x.LastMappedEtag)
                    .ThenByDescending(x => x.NumberOfMappedFields)
                    .ToArray();

                if (explanations != null)
                {
                    for (var i = 1; i < prioritizedResults.Length; i++)
                    {
                        explanations.Add(new Explanation(prioritizedResults[i].IndexName, "Wasn't the widest / most unstable index matching this query"));
                    }
                }

                return prioritizedResults[0];
            }

            if (results.TryGetValue(DynamicQueryMatchType.Partial, out matchResults) && matchResults.Length > 0)
            {
                return matchResults.OrderByDescending(x => x.NumberOfMappedFields).First();
            }

            return new DynamicQueryMatchResult(string.Empty, DynamicQueryMatchType.Failure);
        }

        private DynamicQueryMatchResult ConsiderUsageOfIndex(DynamicQueryMapping query, AutoIndexDefinitionBase definition, List<Explanation> explanations = null)
        {
            var collection = query.ForCollection;
            var indexName = definition.Name;

            if (definition.Collections.Contains(collection, StringComparer.OrdinalIgnoreCase) == false)
            {
                if (definition.Collections.Count == 0)
                    explanations?.Add(new Explanation(indexName, "Query is specific for collection, but the index searches across all of them, may result in a different type being returned."));
                else
                    explanations?.Add(new Explanation(indexName, $"Index does not apply to collection '{collection}'"));

                return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Failure);
            }

            if (definition.Collections.Count > 1) // we only allow indexes with a single entity name
            {
                explanations?.Add(new Explanation(indexName, "Index contains more than a single entity name, may result in a different type being returned."));
                return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Failure);
            }

            var index = _indexStore.GetIndex(definition.Name);
            if (index == null)
                return new DynamicQueryMatchResult(definition.Name, DynamicQueryMatchType.Failure);

            var state = index.State;
            var stats = index.GetStats();

            if (state == IndexState.Error || state == IndexState.Disabled || stats.IsInvalidIndex)
            {
                explanations?.Add(new Explanation(indexName, $"Cannot do dynamic queries on disabled index or index with errors (index name = {indexName})"));
                return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Failure);
            }

            var currentBestState = DynamicQueryMatchType.Complete;

            foreach (var field in query.MapFields.Values)
            {
                if (definition.TryGetField(field.Name, out var indexField))
                {
                    if (field.IsFullTextSearch && indexField.Indexing.HasFlag(AutoFieldIndexing.Search) == false)
                    {
                        explanations?.Add(new Explanation(indexName, $"The following field is not searchable {indexField.Name}, while the query needs to search() on it"));
                        return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Partial);
                    }

                    if (field.IsExactSearch && indexField.Indexing.HasFlag(AutoFieldIndexing.Exact) == false)
                    {
                        explanations?.Add(new Explanation(indexName, $"The following field is not exactable {indexField.Name}, while the query needs to perform exact() on it"));
                        return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Partial);
                    }

                    if (field.Spatial != null)
                    {
                        Debug.Assert(indexField.Spatial != null);

                        if (field.Spatial.Equals(indexField.Spatial) == false)
                        {
                            explanations?.Add(new Explanation(indexName, $"The following field is not a spatial field {indexField.Name}, while the query needs to perform spatial() on it"));
                            return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Failure);
                        }
                    }
                }
                else
                {
                    explanations?.Add(new Explanation(indexName, $"The following field is missing: {field.Name}"));
                    currentBestState = DynamicQueryMatchType.Partial;
                }
            }

            if (currentBestState == DynamicQueryMatchType.Complete && state == IndexState.Idle)
            {
                currentBestState = DynamicQueryMatchType.Partial;
                explanations?.Add(new Explanation(indexName, $"The index (name = {indexName}) is disabled or abandoned. The preference is for active indexes - making a partial match"));
            }

            if (currentBestState != DynamicQueryMatchType.Failure && query.IsGroupBy)
            {
                var bestMapReduceMatch = AssertMapReduceFields(query, (AutoMapReduceIndexDefinition)definition, currentBestState, explanations);

                if (bestMapReduceMatch != DynamicQueryMatchType.Complete)
                    return new DynamicQueryMatchResult(indexName, bestMapReduceMatch);
            }

            if (currentBestState == DynamicQueryMatchType.Partial && index.Type.IsStatic()) // we cannot support this because we might extend fields from static index into auto index
                return new DynamicQueryMatchResult(indexName, DynamicQueryMatchType.Failure);

            return new DynamicQueryMatchResult(indexName, currentBestState)
            {
                LastMappedEtag = index.GetLastMappedEtagFor(collection),
                NumberOfMappedFields = definition.MapFields.Count
            };
        }

        private static DynamicQueryMatchType AssertMapReduceFields(DynamicQueryMapping query, AutoMapReduceIndexDefinition definition, DynamicQueryMatchType currentBestState,
            List<Explanation> explanations)
        {
            var indexName = definition.Name;

            foreach (var mapField in query.MapFields.Values)
            {
                if (definition.ContainsField(mapField.Name) == false)
                {
                    Debug.Assert(currentBestState == DynamicQueryMatchType.Partial);
                    continue;
                }

                var field = definition.GetField(mapField.Name);

                if (field.Aggregation != mapField.AggregationOperation)
                {
                    explanations?.Add(new Explanation(indexName,
                        $"The following field {field.Name} has {field.Aggregation} operation defined, while query required {mapField.AggregationOperation}"));

                    return DynamicQueryMatchType.Failure;
                }
            }

            foreach (var groupByField in query.GroupByFields.Values)
            {
                if (definition.GroupByFields.TryGetValue(groupByField.Name, out var indexField))
                {
                    if (groupByField.IsSpecifiedInWhere == false)
                        continue;

                    if (groupByField.IsFullTextSearch && indexField.Indexing.HasFlag(AutoFieldIndexing.Search) == false)
                    {
                        explanations?.Add(new Explanation(indexName,
                            $"The following group by field is not searchable {indexField.Name}, while the query needs to perform search() on it"));

                        return DynamicQueryMatchType.Partial;
                    }

                    if (groupByField.IsExactSearch && indexField.Indexing.HasFlag(AutoFieldIndexing.Exact) == false)
                    {
                        explanations?.Add(new Explanation(indexName,
                            $"The following group by field is not exactable {indexField.Name}, while the query needs to perform exact() on it"));

                        return DynamicQueryMatchType.Partial;
                    }
                }
                else
                {
                    if (explanations != null)
                    {
                        var missingFields = query.GroupByFields.Where(x => definition.GroupByFields.ContainsKey(x.Value.Name) == false);
                        explanations.Add(new Explanation(indexName, $"The following group by fields are missing: {string.Join(", ", missingFields)}"));
                    }

                    return DynamicQueryMatchType.Failure;
                }
            }


            if (query.GroupByFields.Count != definition.GroupByFields.Count)
            {
                if (explanations != null)
                {
                    var extraFields = definition.GroupByFields.Where(x => query.GroupByFields.Select(y => y.Value.Name).Contains(x.Key) == false);
                    explanations.Add(new Explanation(indexName, $"Index {indexName} has additional group by fields: {string.Join(", ", extraFields)}"));
                }

                return DynamicQueryMatchType.Failure;
            }

            return DynamicQueryMatchType.Complete;
        }
    }
}
