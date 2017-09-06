﻿namespace Raven.Server.Documents.Queries.Parser
{
    public enum OrderByFieldType
    {
        Implicit,
        String,
        Long,
        Double,
        AlphaNumeric,
        Random,
        Score,
        Distance
    }
}
