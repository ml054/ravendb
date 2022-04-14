/// <reference path="../../../typings/tsd.d.ts" />

type IndexLockMode = Raven.Client.Documents.Indexes.IndexLockMode;
type IndexPriority = Raven.Client.Documents.Indexes.IndexPriority;
import saveIndexPriorityCommand from "commands/database/index/saveIndexPriorityCommand";
import database from "models/resources/database";
import saveIndexLockModeCommand from "commands/database/index/saveIndexLockModeCommand";
import { IndexSharedInfo } from "../models/indexes";
import getIndexesStatsCommand from "commands/database/index/getIndexesStatsCommand";
import IndexUtils from "../utils/IndexUtils";
import resetIndexCommand from "commands/database/index/resetIndexCommand";
import enableIndexCommand from "commands/database/index/enableIndexCommand";
import disableIndexCommand from "commands/database/index/disableIndexCommand";
type IndexStats = Raven.Client.Documents.Indexes.IndexStats;
import togglePauseIndexingCommand from "commands/database/index/togglePauseIndexingCommand";
import getIndexesProgressCommand from "commands/database/index/getIndexesProgressCommand";
import openFaultyIndexCommand from "commands/database/index/openFaultyIndexCommand";
import forceIndexReplace from "commands/database/index/forceIndexReplace";

export default class IndexesService {
    
    async getProgress(db: database, location: databaseLocationSpecifier): Promise<Raven.Client.Documents.Indexes.IndexProgress[]> {
        return Promise.resolve([]);
    }
    
    async setLockMode(indexes: IndexSharedInfo[], lockMode: IndexLockMode, db: database) {
        await new saveIndexLockModeCommand(indexes, lockMode, db)
            .execute();
    }
    
    async setPriority(index: IndexSharedInfo, priority: IndexPriority, db: database) {
        await new saveIndexPriorityCommand(index.name, priority, db)
            .execute();
    }
    
    async getStats(db: database, location: databaseLocationSpecifier): Promise<IndexStats[]> {
        const a: any = {"Results":[{"Name":"Orders/ByCompany","MapAttempts":830,"MapSuccesses":830,"MapErrors":0,"MapReferenceAttempts":null,"MapReferenceSuccesses":null,"MapReferenceErrors":null,"ReduceAttempts":830,"ReduceSuccesses":830,"ReduceErrors":0,"ReduceOutputCollection":null,"ReduceOutputReferencePattern":null,"PatternReferencesCollectionName":null,"MappedPerSecondRate":0.0,"ReducedPerSecondRate":0.0,"MaxNumberOfOutputsPerDocument":1,"Collections":{"Orders":{"LastProcessedDocumentEtag":1740,"LastProcessedTombstoneEtag":0,"DocumentLag":0,"TombstoneLag":0}},"LastQueryingTime":"2022-04-14T10:07:03.6475081Z","State":"Normal","Priority":"Normal","CreatedTimestamp":"2022-04-14T08:40:53.8772125Z","LastIndexingTime":"2022-04-14T10:07:03.6476957Z","IsStale":false,"LockMode":"Unlock","Type":"MapReduce","Status":"Running","EntriesCount":89,"ErrorsCount":0,"SourceType":"Documents","IsInvalidIndex":false,"Memory":{"DiskSize":{"SizeInBytes":8388608,"HumaneSize":"8 MBytes"},"ThreadAllocations":{"SizeInBytes":0,"HumaneSize":"0 Bytes"},"MemoryBudget":{"SizeInBytes":33554432,"HumaneSize":"32 MBytes"}},"LastBatchStats":{"InputCount":0,"FailedCount":0,"OutputCount":0,"SuccessCount":0,"Started":"2022-04-14T10:07:03.6476957Z","DurationInMs":5.09,"AllocatedBytes":{"SizeInBytes":19616,"HumaneSize":"19.16 KBytes"},"DocumentsSize":{"SizeInBytes":0,"HumaneSize":"0 Bytes"}}}]};
        
        return a.Results;
    }
    
    async resetIndex(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new resetIndexCommand(index.name, db, location)
            .execute();
    } 
    
    async enable(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new enableIndexCommand(index.name, db, location)
            .execute();
    }

    async disable(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new disableIndexCommand(index.name, db, location)
            .execute();
    }

    async pause(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new togglePauseIndexingCommand(false, db, { name: index.name }, location)
            .execute();
    }

    async resume(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new togglePauseIndexingCommand(true, db, { name: index.name }, location)
            .execute();
    }
    
    async openFaulty(index: IndexSharedInfo, db: database, location: databaseLocationSpecifier) {
        await new openFaultyIndexCommand(index.name, db, location)
            .execute();
    }

    async forceReplace(name: string, database: database) {
        await new forceIndexReplace(name, database)
            .execute();
    }
}
