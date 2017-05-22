// -----------------------------------------------------------------------
//  <copyright file="IAsyncReliableSubscriptions.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Raven.Client.Documents.Subscriptions
{
    public interface IAsyncReliableSubscriptions : IDisposable
    {
        /// <summary>
        /// It creates a data subscription in a database. The subscription will expose all documents that match the specified subscription criteria for a given type.
        /// </summary>
        /// <returns>Created subscription identifier.</returns>
        Task<string> CreateAsync<T>(SubscriptionCreationOptions<T> subscriptionCreationOptions, string database = null);

        /// <summary>
        /// It creates a data subscription in a database. The subscription will expose all documents that match the specified subscription criteria.
        /// </summary>
        /// <returns>Created subscription identifier.</returns>
        Task<string> CreateAsync(SubscriptionCreationOptions subscriptionCreationOptions, string database = null);

        /// <summary>
        /// It opens a subscription and starts pulling documents since a last processed document for that subscription (in document's long? order).
        /// The connection options determine client and server cooperation rules like document batch sizes or a timeout in a matter of which a client
        /// needs to acknowledge that batch has been processed. The acknowledgment is sent after all documents are processed by subscription's handlers.  
        /// There can be only a single client that is connected to a subscription.
        /// </summary>
        /// <returns>Subscription object that allows to add/remove subscription handlers.</returns>
        Subscription<dynamic> Open(SubscriptionConnectionOptions options, string database = null);

        /// <summary>
        /// It opens a subscription and starts pulling documents since a last processed document for that subscription (in document's long? order).
        /// The connection options determine client and server cooperation rules like document batch sizes or a timeout in a matter of which a client
        /// needs to acknowledge that batch has been processed. The acknowledgment is sent after all documents are processed by subscription's handlers.  
        /// There can be only a single client that is connected to a subscription.
        /// </summary>
        /// <returns>Subscription object that allows to add/remove subscription handlers.</returns>
        Subscription<T> Open<T>(SubscriptionConnectionOptions options, string database = null) where T : class;

        /// <summary>
        /// It downloads a list of all existing subscriptions in a database.
        /// </summary>
        /// <returns>Existing subscriptions' configurations.</returns>
        Task<List<SubscriptionState>> GetSubscriptionsAsync(int start, int take, string database = null);

        /// <summary>
        /// It deletes a subscription.
        /// </summary>
        Task DeleteAsync(string id, string database = null);
    }
}