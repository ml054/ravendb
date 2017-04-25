﻿using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Queries;
using Raven.Client.Util;
using Raven.Server.Documents;
using Raven.Server.Documents.Indexes;
using Raven.Server.Documents.Indexes.Auto;
using Raven.Server.Documents.Indexes.MapReduce.Auto;
using Raven.Server.Documents.Queries;
using Raven.Server.Documents.Queries.Dynamic;
using Xunit;

namespace FastTests.Server.Documents.Queries.Dynamic.MapReduce
{
    public class MatchingAutoMapReduceIndexesForDynamicQueries : RavenLowLevelTestBase
    {
        private readonly DocumentDatabase _documentDatabase;
        protected readonly DynamicQueryToIndexMatcher _sut;

        public MatchingAutoMapReduceIndexesForDynamicQueries()
        {
            _documentDatabase = CreateDocumentDatabase();

            _sut = new DynamicQueryToIndexMatcher(_documentDatabase.IndexStore);
        }

        [Fact]
        public void Failure_match_if_there_is_no_index()
        {
            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "",
                DynamicMapReduceFields = new[]
            {
                new DynamicMapReduceField
                {
                    Name = "Location",
                    IsGroupBy = true
                },
                new DynamicMapReduceField
                {
                    Name = "Count",
                    OperationType = FieldMapReduceOperation.Count
                }
            }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Failure, result.MatchType);
        }


        [Fact]
        public void Complete_match_for_single_matching_index_with_sort_options()
        {
            var definition = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count,
                    Sort = SortOptions.Numeric
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                    Sort = SortOptions.String
                }
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
                {
                    new DynamicMapReduceField
                    {
                        Name = "Count",
                        OperationType = FieldMapReduceOperation.Count
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Location",
                        IsGroupBy = true
                    }
                },
                SortedFields = new[]
                {
                    new SortedField("Count_L_Range"),
                    new SortedField("Location"),
                }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Complete, result.MatchType);
            Assert.Equal(definition.Name, result.IndexName);
        }

        [Fact]
        public void Failure_match_for_map_index_containing_the_same_field_names()
        {
            var definition = new AutoMapIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                },
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                }
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
            {
                new DynamicMapReduceField
                {
                    Name = "Count",
                    OperationType = FieldMapReduceOperation.Count
                },
                new DynamicMapReduceField
                {
                    Name = "Location",
                    IsGroupBy = true
                }
            }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Failure, result.MatchType);
        }

        [Fact]
        public void Failure_match_for_map_reduce_index_having_different_aggregation_function()
        {
            var definition = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                }
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
                {
                    new DynamicMapReduceField
                    {
                        Name = "Count",
                        OperationType = FieldMapReduceOperation.Sum
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Location",
                        IsGroupBy = true
                    }
                }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Failure, result.MatchType);
        }

        [Fact]
        public void Partial_match_for_map_reduce_index_not_having_all_map_fields_defined_in_query()
        {
            var definition = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                }
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
                {
                    new DynamicMapReduceField
                    {
                        Name = "Count",
                        OperationType = FieldMapReduceOperation.Count
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Sum",
                        OperationType = FieldMapReduceOperation.Sum
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Location",
                        IsGroupBy = true
                    }
                }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Partial, result.MatchType);
            Assert.Equal(definition.Name, result.IndexName);
        }

        [Fact]
        public void Failure_match_for_map_reduce_index_not_matching_exactly_group_by_fields()
        {
            // missing nick name field to match
            var usersByCountReducedByAgeAndLocation = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                },
                new IndexField
                {
                    Name = "Age",
                    Storage = FieldStorage.Yes,
                }
            });

            // additional Age field to match
            var usersByCountReducedByLocationAndNickNameAndAge = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                },
                new IndexField
                {
                    Name = "NickName",
                    Storage = FieldStorage.Yes,
                },
                new IndexField
                {
                    Name = "Age",
                    Storage = FieldStorage.Yes,
                }
            });

            add_index(usersByCountReducedByAgeAndLocation);
            add_index(usersByCountReducedByLocationAndNickNameAndAge);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
                {
                    new DynamicMapReduceField
                    {
                        Name = "Count",
                        OperationType = FieldMapReduceOperation.Count
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Location",
                        IsGroupBy = true
                    },
                    new DynamicMapReduceField
                    {
                        Name = "NickName",
                        IsGroupBy = true
                    }
                }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Failure, result.MatchType);
        }

        [Fact]
        public void Complete_match_and_surpassed_map_reduce_index_is_choosen()
        {
            var usersByCountGroupedByLocation = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                }
            });

            var usersByCountAndTotalAgeGroupedByLocation = new AutoMapReduceIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Count",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Count
                },
                new IndexField
                {
                    Name = "TotalAge",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Sum
                },
            },
            new[]
            {
                new IndexField
                {
                    Name = "Location",
                    Storage = FieldStorage.Yes,
                }
            });

            add_index(usersByCountGroupedByLocation);
            add_index(usersByCountAndTotalAgeGroupedByLocation);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Location:Poland",
                DynamicMapReduceFields = new[]
                {
                    new DynamicMapReduceField
                    {
                        Name = "Count",
                        OperationType = FieldMapReduceOperation.Count
                    },
                    new DynamicMapReduceField
                    {
                        Name = "Location",
                        IsGroupBy = true
                    }
                }
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Complete, result.MatchType);
            Assert.Equal(usersByCountAndTotalAgeGroupedByLocation.Name, result.IndexName);
        }

        [Fact]
        public void Failure_when_sort_options_do_not_match()
        {
            var definition = new AutoMapReduceIndexDefinition("LineItems", new[]
            {
                new IndexField
                {
                    Name = "Price",
                    Storage = FieldStorage.Yes,
                    MapReduceOperation = FieldMapReduceOperation.Sum,
                    Sort = SortOptions.String
                },
            }, new[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.Yes
                }
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("LineItems", new IndexQueryServerSide
            {
                Query = "Price:70",
                SortedFields = new[] { new SortedField("Price_L_Range") },
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Failure, result.MatchType);
        }

        [Fact]
        public void Partial_match_when_sort_field_is_not_mapped()
        {
            var definition = new AutoMapIndexDefinition("Users", new[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.No
                },
            });

            add_index(definition);

            var dynamicQuery = DynamicQueryMapping.Create("Users", new IndexQueryServerSide
            {
                Query = "Name:Arek",
                SortedFields = new[] { new SortedField("Weight") },
            });

            var result = _sut.Match(dynamicQuery);

            Assert.Equal(DynamicQueryMatchType.Partial, result.MatchType);
            Assert.Equal(definition.Name, result.IndexName);
        }


        protected void add_index(IndexDefinitionBase definition)
        {
            AsyncHelpers.RunSync(() => _documentDatabase.IndexStore.CreateIndex(definition));
        }

        protected Index get_index(string name)
        {
            return _documentDatabase.IndexStore.GetIndex(name);
        }

        public override void Dispose()
        {
            try
            {
                _documentDatabase.Dispose();
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}