﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Raven.Client.Documents.Queries;
using Raven.Server.Documents.Queries.LuceneIntegration;
using Raven.Server.Documents.Queries.Parse;
using Raven.Server.Utils;
using Version = Lucene.Net.Util.Version;

namespace Raven.Server.Documents.Queries
{
    public static class QueryBuilder
    {
        private const string FieldRegexVal = @"([@\w<>,]+?):";
        private const string MethodRegexVal = @"(@\w+<[^>]+>):";
        private const string DateTimeVal = @"\s*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z?)";
        private static readonly Regex FieldQuery = new Regex(FieldRegexVal, RegexOptions.Compiled);
        private static readonly Regex UntokenizedQuery = new Regex(FieldRegexVal + @"[\s\(]*(\[\[.+?\]\])|(?<=,)\s*(\[\[.+?\]\])(?=\s*[,\)])", RegexOptions.Compiled);
        private static readonly Regex SearchQuery = new Regex(FieldRegexVal + @"\s*(\<\<.+?\>\>)(^[\d.]+)?", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex DateQuery = new Regex(FieldRegexVal + DateTimeVal, RegexOptions.Compiled);
        private static readonly Regex InDatesQuery = new Regex(MethodRegexVal + @"\s*(\([^)]*" + DateTimeVal + @"[^)]*\))", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex RightOpenRangeQuery = new Regex(FieldRegexVal + @"\[(\S+)\sTO\s(\S+)\}", RegexOptions.Compiled);
        private static readonly Regex LeftOpenRangeQuery = new Regex(FieldRegexVal + @"\{(\S+)\sTO\s(\S+)\]", RegexOptions.Compiled);
        private static readonly Regex CommentsRegex = new Regex(@"( //[^""]+?)$", RegexOptions.Compiled | RegexOptions.Multiline);

        public static bool UseLuceneASTParser { get; set; } = true;

        /* The reason that we use @emptyIn<PermittedUsers>:(no-results)
         * instead of using @in<PermittedUsers>:()
         * is that lucene does not access an empty () as a valid syntax.
         */
        private static readonly Dictionary<string, Func<string, List<string>, Query>> QueryMethods = new Dictionary<string, Func<string, List<string>, Query>>(StringComparer.OrdinalIgnoreCase)
        {
            {"in", (field, args) => new TermsMatchQuery(field, args)},
            {"emptyIn", (field, args) => new TermsMatchQuery(field, args)}
        };

        public static Query BuildQuery(string query, Analyzer analyzer)
        {
            return BuildQuery(query, QueryOperator.Or, null, analyzer);
        }

        public static Query BuildQuery(string query, QueryOperator defaultOperator, string defaultField, Analyzer analyzer)
        {
            using (CultureHelper.EnsureInvariantCulture())
            {
                if (UseLuceneASTParser)
                {
                    try
                    {
                        var parser = new LuceneQueryParser();
                        parser.IsDefaultOperatorAnd = defaultOperator == QueryOperator.And;
                        parser.Parse(query);
                        var res = parser.LuceneAST.ToQuery(
                            new LuceneASTQueryConfiguration
                            {
                                Analayzer = analyzer,
                                DefaultOperator = defaultOperator,
                                FieldName = new FieldName(defaultField ?? string.Empty)
                            });
                        // The parser already throws parse exception if there is a syntax error.
                        // We now return null in the case of a term query that has been fully analyzed, so we need to return a valid query.
                        return res??new BooleanQuery();
                    }
                    catch (ParseException pe)
                    {
                        throw new ParseException("Could not parse: '" + query + "'", pe);
                    }
                }
                var originalQuery = query;
                try
                {
                    var queryParser = new RangeQueryParser(Version.LUCENE_29, defaultField ?? string.Empty, analyzer)
                    {
                        DefaultOperator = defaultOperator == QueryOperator.Or
                            ? QueryParser.Operator.OR
                            : QueryParser.Operator.AND,
                        AllowLeadingWildcard = true
                    };
                    query = PreProcessComments(query);
                    query = PreProcessMixedInclusiveExclusiveRangeQueries(query);
                    query = PreProcessUntokenizedTerms(query, queryParser);
                    query = PreProcessSearchTerms(query);
                    query = PreProcessDateTerms(query, queryParser);
                    var generatedQuery = queryParser.Parse(query);
                    generatedQuery = HandleMethods(generatedQuery, analyzer);
                    return generatedQuery;
                }
                catch (ParseException pe)
                {
                    if (originalQuery == query)
                        throw new ParseException("Could not parse: '" + query + "'", pe);
                    throw new ParseException("Could not parse modified query: '" + query + "' original was: '" + originalQuery + "'", pe);
                }
            }
        }

        private static bool MightMatchComments(string query)
        {
            //return query.Contains("//");
            int len = query.Length - 1;
            for (int i = 0; i < len; i++)
            {
                if (query[i] == '/' && query[i + 1] == '/') return true;
            }
            return false;
        }

        internal static string PreProcessComments(string query)
        {
            // First we should check if this query might match the regex because regex are expenssive...
            if (!MightMatchComments(query)) return query;
            var matches = CommentsRegex.Matches(query);
            if (matches.Count < 1)
                return query;
            var q = new StringBuilder(query);
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                q.Remove(match.Index, match.Length);
            }
            return q.ToString();
        }

        internal static Query HandleMethods(Query query, Analyzer analyzer)
        {
            var termQuery = query as TermQuery;
            if (termQuery != null && termQuery.Term.Field.StartsWith("@"))
            {
                return HandleMethodsForQueryAndTerm(query, termQuery.Term);
            }
            var pharseQuery = query as PhraseQuery;
            if (pharseQuery != null)
            {
                var terms = pharseQuery.GetTerms();
                if (terms.All(x => x.Field.StartsWith("@")) == false ||
                    terms.Select(x => x.Field).Distinct().Count() != 1)
                    return query;
                return HandleMethodsForQueryAndTerm(query, terms);
            }
            var wildcardQuery = query as WildcardQuery;
            if (wildcardQuery != null)
            {
                return HandleMethodsForQueryAndTerm(query, wildcardQuery.Term);
            }
            var booleanQuery = query as BooleanQuery;
            if (booleanQuery != null)
            {
                foreach (var c in booleanQuery.Clauses)
                {
                    c.Query = HandleMethods(c.Query, analyzer);
                }
                if (booleanQuery.Clauses.Count == 0)
                    return booleanQuery;

                //merge only clauses that have "OR" operator between them
                var mergeGroups = booleanQuery.Clauses.Where(clause => clause.Occur == Occur.SHOULD)
                                                      .Select(x => x.Query)
                                                      .OfType<IRavenLuceneMethodQuery>()
                                                      .GroupBy(x => x.Field)
                                                      .ToArray();
                if (mergeGroups.Length == 0)
                    return booleanQuery;

                foreach (var mergeGroup in mergeGroups)
                {
                    var clauses = mergeGroup.ToArray();
                    var first = clauses[0];
                    foreach (var mergedClause in clauses.Skip(1))
                    {
                        booleanQuery.Clauses.RemoveAll(x => ReferenceEquals(x.Query, mergedClause));
                    }
                    var ravenLuceneMethodQuery = clauses.Skip(1).Aggregate(first, (methodQuery, clause) => methodQuery.Merge(clause));
                    booleanQuery.Clauses.First(x => ReferenceEquals(x.Query, first)).Query = (Query)ravenLuceneMethodQuery;
                }
                if (booleanQuery.Clauses.Count == 1)
                    return booleanQuery.Clauses[0].Query;
                return booleanQuery;
            }
            return query;
        }

        private static Regex _unescapedSplitter = new Regex("(?<!`),(?!`)", RegexOptions.Compiled);

        internal static Query HandleMethodsForQueryAndTerm(Query query, Term term)
        {
            Func<string, List<string>, Query> value;
            var field = term.Field;
            if (TryHandlingMethodForQueryAndTerm(ref field, out value) == false)
                return query;

            var parts = _unescapedSplitter.Split(term.Text);
            var list = new List<string>(
                    from part in parts
                    where string.IsNullOrWhiteSpace(part) == false
                    select part.Replace("`,`", ",")
            );
            return value(field, list);
        }

        internal static Query HandleMethodsForQueryAndTerm(Query query, Term[] terms)
        {
            Func<string, List<string>, Query> value;
            var field = terms[0].Field;
            if (TryHandlingMethodForQueryAndTerm(ref field, out value) == false)
                return query;

            return value(field, terms.Select(x => x.Text).ToList());
        }

        private static bool TryHandlingMethodForQueryAndTerm(ref string field,
            out Func<string, List<string>, Query> value)
        {
            value = null;
            var indexOfFieldStart = field.IndexOf('<');
            var indexOfFieldEnd = field.LastIndexOf('>');
            if (indexOfFieldStart == -1 || indexOfFieldEnd == -1)
            {
                return false;
            }
            var method = field.Substring(1, indexOfFieldStart - 1);
            field = field.Substring(indexOfFieldStart + 1, indexOfFieldEnd - indexOfFieldStart - 1);

            if (QueryMethods.TryGetValue(method, out value) == false)
            {
                throw new InvalidOperationException("Method call " + field + " is invalid.");
            }
            return true;
        }

        private static bool MightMatchDateTerms(string query)
        {
            //d{4}-\d{2}-\d{2}T
            //return query.Contains("//");
            int len = query.Length - 6;
            for (int i = 0; i < len; i++)
            {
                if (query[i] == '-' && query[i + 3] == '-' && query[i + 6] == 'T') return true;
            }
            return false;
        }

        internal static string PreProcessDateTerms(string query, RangeQueryParser queryParser)
        {
            // First we should check if this query might match the regex because regex are expenssive...
            if (!MightMatchDateTerms(query)) return query;
            var searchMatches = DateQuery.Matches(query);
            if (searchMatches.Count > 0)
            {
                query = TokenReplace(query, searchMatches, queryParser.ReplaceToken);
            }
            searchMatches = InDatesQuery.Matches(query);
            if (searchMatches.Count == 0)
                return query;
            return TokenReplace(query, searchMatches, queryParser.ReplaceDateTimeTokensInMethod);
        }

        private static string TokenReplace(string query, MatchCollection searchMatches, Func<string, string, string> replacFunc)
        {
            var queryStringBuilder = new StringBuilder(query);
            for (var i = searchMatches.Count - 1; i >= 0; i--) // reversing the scan so we won't affect positions of later items
            {
                var searchMatch = searchMatches[i];
                var field = searchMatch.Groups[1].Value;
                var termReplacement = searchMatch.Groups[2].Value;

                var replaceToken = replacFunc(field, termReplacement);
                queryStringBuilder.Remove(searchMatch.Index, searchMatch.Length);
                queryStringBuilder
                    .Insert(searchMatch.Index, field)
                    .Insert(searchMatch.Index + field.Length, ":")
                    .Insert(searchMatch.Index + field.Length + 1, replaceToken);
            }
            return queryStringBuilder.ToString();
        }

        private static bool MightMatchSearchTerms(string query)
        {
            //return query.Contains("<<");
            int len = query.Length - 1;
            for (int i = 0; i < len; i++)
            {
                if (query[i] == '<' && query[i + 1] == '<') return true;
            }
            return false;
        }

        internal static string PreProcessSearchTerms(string query)
        {
            // First we should check if this query might match the regex because regex are expenssive...
            if (!MightMatchSearchTerms(query)) return query;
            var searchMatches = SearchQuery.Matches(query);
            if (searchMatches.Count < 1)
                return query;

            var queryStringBuilder = new StringBuilder(query);
            for (var i = searchMatches.Count - 1; i >= 0; i--) // reversing the scan so we won't affect positions of later items
            {
                var searchMatch = searchMatches[i];
                var field = searchMatch.Groups[1].Value;
                var terms = searchMatch.Groups[2].Value.Substring(2, searchMatch.Groups[2].Length - 4);

                string boost = "";
                if (string.IsNullOrWhiteSpace(searchMatch.Groups[3].Value) == false)
                {
                    boost = searchMatch.Groups[3].Value;
                }

                queryStringBuilder.Remove(searchMatch.Index, searchMatch.Length);
                queryStringBuilder.Insert(searchMatch.Index, '(');
                var len = searchMatch.Index;
                foreach (var term in terms.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    switch (term) // ignore invalid options
                    {
                        case "OR":
                        case "AND":
                        case "||":
                        case "&&":
                            continue;
                    }

                    var termQuery = new StringBuilder().Append(field).Append(':').Append(term).Append(boost).Append(' ');
                    len += termQuery.Length;
                    queryStringBuilder.Insert(searchMatch.Index + 1, termQuery);
                }
                queryStringBuilder.Insert(len, ')');
            }
            return queryStringBuilder.ToString();
        }

        private static bool MightMatchUntokenizedTerms(string query)
        {
            //return query.Contains("[[");
            int len = query.Length - 1;
            for (int i = 0; i < len; i++)
            {
                if (query[i] == '[' && query[i + 1] == '[') return true;
            }
            return false;
        }
        /// <summary>
        /// Detects untokenized fields and sets as NotAnalyzed in analyzer
        /// </summary>
        private static string PreProcessUntokenizedTerms(string query, RangeQueryParser queryParser)
        {
            // First we should check if this query might match the regex because regex are expenssive...
            if (!MightMatchUntokenizedTerms(query)) return query;
            var untokenizedMatches = UntokenizedQuery.Matches(query);
            if (untokenizedMatches.Count < 1)
                return query;

            var sb = new StringBuilder(query);
            MatchCollection fieldMatches = null;

            // process in reverse order to leverage match string indexes
            for (var i = untokenizedMatches.Count; i > 0; i--)
            {
                var match = untokenizedMatches[i - 1];

                // specify that term for this field should not be tokenized
                var value = match.Groups[2].Value;
                var term = match.Groups[2];
                string name = match.Groups[1].Value;
                if (string.IsNullOrEmpty(value))
                {
                    value = match.Groups[3].Value;
                    term = match.Groups[3];
                    if (fieldMatches == null)
                        fieldMatches = FieldQuery.Matches(query);

                    var lastField = fieldMatches.Cast<Match>().LastOrDefault(x => x.Index <= term.Index);
                    if (lastField != null)
                    {
                        name = lastField.Groups[1].Value;
                    }
                }
                var rawTerm = value.Substring(2, value.Length - 4);
                queryParser.SetUntokenized(name, Unescape(rawTerm));


                // introduce " " around the term
                var startIndex = term.Index;
                var length = term.Length - 2;
                if (sb[startIndex + length - 1] != '"')
                {
                    sb.Insert(startIndex + length, '"');
                    length += 1;
                }
                if (sb[startIndex + 2] != '"')
                {
                    sb.Insert(startIndex + 2, '"');
                    length += 1;
                }
                // remove enclosing "[[" "]]" from term value (again in reverse order)
                sb.Remove(startIndex + length, 2);
                sb.Remove(startIndex, 2);
            }

            return sb.ToString();
        }

        public static string Unescape(string term)
        {
            // method doesn't allocate a StringBuilder unless the string requires unescaping
            // also this copies chunks of the original string into the StringBuilder which
            // is far more efficient than copying character by character because StringBuilder
            // can access the underlying string data directly

            if (string.IsNullOrEmpty(term))
            {
                return term;
            }

            bool isPhrase = term.StartsWith("\"") && term.EndsWith("\"");
            int start = 0;
            int length = term.Length;
            StringBuilder buffer = null;
            char prev = '\0';
            for (int i = start; i < length; i++)
            {
                char ch = term[i];
                if (prev != '\\')
                {
                    prev = ch;
                    continue;
                }
                prev = '\0';// reset
                switch (ch)
                {
                    case '*':
                    case '?':
                    case '+':
                    case '-':
                    case '&':
                    case '|':
                    case '!':
                    case '(':
                    case ')':
                    case '{':
                    case '}':
                    case '[':
                    case ']':
                    case '^':
                    case '"':
                    case '~':
                    case ':':
                    case '\\':
                        {
                            if (buffer == null)
                            {
                                // allocate builder with headroom
                                buffer = new StringBuilder(length * 2);
                            }
                            // append any leading substring
                            buffer.Append(term, start, i - start - 1);
                            buffer.Append(ch);
                            start = i + 1;
                            break;
                        }
                }
            }

            if (buffer == null)
            {
                if (isPhrase)
                    return term.Substring(1, term.Length - 2);
                // no changes required
                return term;
            }

            if (length > start)
            {
                // append any trailing substring
                buffer.Append(term, start, length - start);
            }

            return buffer.ToString();
        }

        private static bool MightMatchMixedInclusiveExclusiveRangeQueries(string query)
        {
            foreach (char c in query)
            {
                if (c == '{' || c == '}') return true;
            }
            return false;
        }
        public static string PreProcessMixedInclusiveExclusiveRangeQueries(string query)
        {
            // First we should check if this query might match the regex because regex are expenssive...
            if (!MightMatchMixedInclusiveExclusiveRangeQueries(query)) return query;

            // we need this method to support queries like [x, y} and {x, y]
            // Lucene 4 will have a built-in support for this

            StringBuilder queryStringBuilder = null;
            var tempSb = new StringBuilder();

            var rightOpenRanges = RightOpenRangeQuery.Matches(query);
            if (rightOpenRanges.Count > 0) // // field:[x, y} - right-open interval convert to (field: [x, y] AND NOT field:y)
            {
                queryStringBuilder = new StringBuilder(query);

                for (var i = rightOpenRanges.Count - 1; i >= 0; i--) // reversing the scan so we won't affect positions of later items
                {
                    var range = rightOpenRanges[i];
                    var field = range.Groups[1].Value;
                    var rangeStart = range.Groups[2].Value;
                    var rangeEnd = range.Groups[3].Value;
                    tempSb.Clear();
                    //string.Format("({0}:[{1} TO {2}] AND NOT {0}:{2})",field,rangeStart,rangeEnd)
                    tempSb.Append('(').Append(field).Append(":[").Append(rangeStart).Append(" TO ").Append(rangeEnd).Append("] AND NOT ").Append(field)
                        .Append(':').Append(rangeEnd).Append(')');
                    queryStringBuilder.Remove(range.Index, range.Length)
                        .Insert(range.Index, tempSb.ToString());
                }
            }

            var leftOpenRanges = LeftOpenRangeQuery.Matches(queryStringBuilder != null ? queryStringBuilder.ToString() : query);

            if (leftOpenRanges.Count > 0) // field:{x, y] - left-open interval convert to (field: [x, y] AND NOT field:x)
            {
                if (queryStringBuilder == null)
                    queryStringBuilder = new StringBuilder(query);

                for (var i = leftOpenRanges.Count - 1; i >= 0; i--) // reversing the scan so we won't affect positions of later items
                {
                    var range = leftOpenRanges[i];
                    var field = range.Groups[1].Value;
                    var rangeStart = range.Groups[2].Value;
                    var rangeEnd = range.Groups[3].Value;
                    tempSb.Clear();
                    //string.Format("({0}:[{1} TO {2}] AND NOT {0}:{2})",field,rangeStart,rangeStart)
                    tempSb.Append('(').Append(field).Append(":[").Append(rangeStart).Append(" TO ").Append(rangeEnd).Append("] AND NOT ").Append(field)
                        .Append(':').Append(rangeStart).Append(')');
                    queryStringBuilder.Remove(range.Index, range.Length)
                        .Insert(range.Index, tempSb.ToString());
                }
            }

            return queryStringBuilder != null ? queryStringBuilder.ToString() : query;
        }
    }
}