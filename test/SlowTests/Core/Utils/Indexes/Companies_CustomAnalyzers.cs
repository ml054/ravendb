// -----------------------------------------------------------------------
//  <copyright file="QueryResultsStreaming.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// ----------------------------------------------------------------------
using System.Linq;
using Raven.Client.Documents.Indexes;
using Company = SlowTests.Core.Utils.Entities.Company;

namespace SlowTests.Core.Utils.Indexes
{
    public class Companies_CustomAnalyzers : AbstractIndexCreationTask<Company>
    {
        public Companies_CustomAnalyzers()
        {
            Map = companies => from company in companies
                               select new
                               {
                                   company.Name,
                                   company.Desc,
                                   company.Email,
                                   company.Address1,
                                   company.Address2,
                                   company.Address3,
                                   company.Phone
                               };

            Analyzers.Add(c => c.Name, typeof(Lucene.Net.Analysis.Standard.StandardAnalyzer).ToString());
            Analyzers.Add(c => c.Desc, typeof(Lucene.Net.Analysis.StopAnalyzer).ToString());
            Analyzers.Add(c => c.Email, typeof(Lucene.Net.Analysis.StopAnalyzer).ToString());
            Analyzers.Add(c => c.Address1, typeof(Lucene.Net.Analysis.SimpleAnalyzer).ToString());
            Analyzers.Add(c => c.Address2, typeof(Lucene.Net.Analysis.WhitespaceAnalyzer).ToString());
            Analyzers.Add(c => c.Address3, typeof(Lucene.Net.Analysis.KeywordAnalyzer).ToString());
            Analyzers.Add(c => c.Phone, typeof(Lucene.Net.Analysis.Standard.StandardAnalyzer).ToString());

            Stores.Add(c => c.Name, FieldStorage.Yes);
            Stores.Add(c => c.Desc, FieldStorage.Yes);

            Indexes.Add(c => c.Email, FieldIndexing.Exact);

            TermVectors.Add(c => c.Name, FieldTermVector.WithPositionsAndOffsets);
        }
    }
}
