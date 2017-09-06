﻿using Raven.Client.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Voron;
using Voron.Data.Tables;

namespace Raven.Server.ServerWide.Commands
{
    public abstract class UpdateValueForDatabaseCommand : CommandBase
    {
        public string DatabaseName { get; set; }

        public abstract string GetItemId();

        public abstract void FillJson(DynamicJsonValue json);

        protected abstract BlittableJsonReaderObject GetUpdatedValue(long index, DatabaseRecord record, JsonOperationContext context,
            BlittableJsonReaderObject existingValue, bool isPassive);

        public virtual unsafe void Execute(TransactionOperationContext context, Table items, long index, DatabaseRecord record, bool isPassive, out object result)
        {
            BlittableJsonReaderObject itemBlittable = null;
            var itemKey = GetItemId();

            using (Slice.From(context.Allocator, itemKey.ToLowerInvariant(), out Slice valueNameLowered))
            {
                if (items.ReadByKey(valueNameLowered, out TableValueReader reader))
                {
                    var ptr = reader.Read(2, out int size);
                    itemBlittable = new BlittableJsonReaderObject(ptr, size, context);
                }

                itemBlittable = GetUpdatedValue(index, record, context, itemBlittable, isPassive);

                // if returned null, means, there is nothing to update and we just wanted to delete the value
                if (itemBlittable == null)
                {
                    items.DeleteByKey(valueNameLowered);
                    result = GetResult();
                    return;
                }

                // here we get the item key again, in case it was changed (a new entity, etc)
                itemKey = GetItemId();
            }

            using (Slice.From(context.Allocator, itemKey, out Slice valueName))
            using (Slice.From(context.Allocator, itemKey.ToLowerInvariant(), out Slice valueNameLowered))
            {
                ClusterStateMachine.UpdateValue(index, items, valueNameLowered, valueName, itemBlittable);
                result = GetResult();
            }
        }

        public virtual object GetResult()
        {
            return null;
        }

        protected UpdateValueForDatabaseCommand(string databaseName)
        {
            DatabaseName = databaseName;
        }

        public override DynamicJsonValue ToJson(JsonOperationContext context)
        {
            var djv = base.ToJson(context);
            djv[nameof(DatabaseName)] = DatabaseName;

            FillJson(djv);

            return djv;
        }
    }
}
