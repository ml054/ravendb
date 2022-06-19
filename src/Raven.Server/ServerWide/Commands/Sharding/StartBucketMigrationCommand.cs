﻿using System;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Sharding;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands.Sharding
{
    public class StartBucketMigrationCommand : UpdateDatabaseCommand
    {
        public int SourceShard;
        public int DestinationShard;
        public int Bucket;

        public StartBucketMigrationCommand()
        {
        }

        public StartBucketMigrationCommand(int bucket, int sourceShard, int destShard, string database, string raftId) : base(database, raftId)
        {
            Bucket = bucket;
            SourceShard = sourceShard;
            DestinationShard = destShard;
        }

        public override void UpdateDatabaseRecord(DatabaseRecord record, long etag)
        {
            if (record.Sharding.ShardBucketMigrations.Count > 0)
            {
                foreach (var migration in record.Sharding.ShardBucketMigrations)
                {
                    if (migration.Value.Status < MigrationStatus.OwnershipTransferred)
                        throw new InvalidOperationException(
                            $"Only one bucket can be transferred at a time, currently bucket {migration.Key} is {migration.Value.Status}");

                    if (migration.Key == Bucket)
                        throw new InvalidOperationException($"Can't migrate bucket {Bucket}, since it is still migrating.");
                }
            }

            var bucketMigration = new ShardBucketMigration
            {
                Bucket = Bucket,
                DestinationShard = DestinationShard,
                SourceShard = SourceShard,
                MigrationIndex = etag,
                Status = MigrationStatus.Moving
            };

            record.ShardBucketMigrations.Add(Bucket, bucketMigration);
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(SourceShard)] = SourceShard;
            json[nameof(DestinationShard)] = DestinationShard;
            json[nameof(Bucket)] = Bucket;
        }
    }
}
