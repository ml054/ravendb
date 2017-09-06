﻿using System;
using System.Text;
using Sparrow.Json;

namespace Raven.Client.Exceptions
{
    public class InvalidQueryException : RavenException
    {
        private InvalidQueryException()
        {
        }

        private InvalidQueryException(string message)
            : base(message)
        {
        }

        private InvalidQueryException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public InvalidQueryException(string message, string queryText, BlittableJsonReaderObject parameters)
            : base(BuildMessage(message, queryText, parameters))
        {

        }

        private static string BuildMessage(string message, string queryText, BlittableJsonReaderObject parameters)
        {
            var result = new StringBuilder(message.Length + queryText.Length);

            result.Append(message)
                .Append(Environment.NewLine)
                .Append("Query: ")
                .Append(queryText);

            if (parameters != null)
            {
                result.Append(Environment.NewLine)
                    .Append("Parameters: ")
                    .Append(parameters);
            }

            return result.ToString();
        }
    }
}
