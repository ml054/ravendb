﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Util;
using Raven.Server.Routing;
using Raven.Server.Web;
using Sparrow.Collections.LockFree;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Utils;

namespace Raven.Server.Documents.Handlers.Debugging
{
    public class MemoryStatsHandler : RequestHandler
    {
        [RavenAction("/admin/debug/memory/stats", "GET", AuthorizationStatus.Operator, IsDebugInformationEndpoint = true)]
        public Task MemoryStats()
        {
            using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            {
                //TODO: When https://github.com/dotnet/corefx/issues/10157 is done, add managed 
                //TODO: allocations per thread to the stats as well

                var djv = MemoryStatsInternal();

                using (var write = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    context.Write(write, djv);
                }
                return Task.CompletedTask;
            }
        }

        public static DynamicJsonValue MemoryStatsInternal()
        {
            var currentProcess = Process.GetCurrentProcess();
            long workingSet;
            if (Sparrow.Platform.PlatformDetails.RunningOnPosix == false)
                workingSet = currentProcess.WorkingSet64;
            else
                workingSet = Sparrow.LowMemory.MemoryInformation.GetRssMemoryUsage(currentProcess.Id);
            long totalUnmanagedAllocations = 0;
            long totalMapping = 0;
            var fileMappingByDir = new Dictionary<string, Dictionary<string, ConcurrentDictionary<IntPtr, long>>>();
            var fileMappingSizesByDir = new Dictionary<string, long>();
            foreach (var mapping in NativeMemory.FileMapping)
            {
                var dir = Path.GetDirectoryName(mapping.Key);

                if (fileMappingByDir.TryGetValue(dir, out Dictionary<string, ConcurrentDictionary<IntPtr, long>> value) == false)
                {
                    value = new Dictionary<string, ConcurrentDictionary<IntPtr, long>>();
                    fileMappingByDir[dir] = value;
                }
                value[mapping.Key] = mapping.Value;
                foreach (var singleMapping in mapping.Value)
                {
                    fileMappingSizesByDir.TryGetValue(dir, out long prevSize);
                    fileMappingSizesByDir[dir] = prevSize + singleMapping.Value;
                    totalMapping += singleMapping.Value;

                }
            }

            var prefixLength = LongestCommonPrefixLength(new List<string>(fileMappingSizesByDir.Keys));

            var fileMappings = new DynamicJsonArray();
            foreach (var sizes in fileMappingSizesByDir.OrderByDescending(x => x.Value))
            {
                if (fileMappingByDir.TryGetValue(sizes.Key, out Dictionary<string, ConcurrentDictionary<IntPtr, long>> value))
                {
                    var dir = new DynamicJsonValue
                    {
                        ["Directory"] = sizes.Key.Substring(prefixLength),
                        ["TotalDirectorySize"] = new DynamicJsonValue
                        {
                            ["Mapped"] = sizes.Value,
                            ["HumaneMapped"] = Size.Humane(sizes.Value)
                        }
                    };
                    foreach (var file in value.OrderBy(x => x.Key))
                    {
                        long totalMapped = 0;
                        var dja = new DynamicJsonArray();
                        var dic = new Dictionary<long, long>();
                        foreach (var mapping in file.Value)
                        {
                            totalMapped += mapping.Value;
                            dic.TryGetValue(mapping.Value, out long prev);
                            dic[mapping.Value] = prev + 1;
                        }
                        foreach (var maps in dic)
                        {
                            dja.Add(new DynamicJsonValue
                            {
                                ["Size"] = maps.Key,
                                ["Count"] = maps.Value
                            });
                        }
                        dir[Path.GetFileName(file.Key)] = new DynamicJsonValue
                        {
                            ["FileSize"] = GetFileSize(file.Key),
                            ["TotalMapped"] = totalMapped,
                            ["HumaneTotalMapped"] = Size.Humane(totalMapped),
                            ["Mappings"] = dja
                        };
                    }
                    fileMappings.Add(dir);
                }
            }

            var threads = new DynamicJsonArray();
            foreach (var stats in NativeMemory.ThreadAllocations.Values
                .Where(x => x.ThreadInstance.IsAlive)
                .GroupBy(x => x.Name)
                .OrderByDescending(x => x.Sum(y => y.TotalAllocated)))
            {
                var unmanagedAllocations = stats.Sum(x => x.TotalAllocated);
                totalUnmanagedAllocations += unmanagedAllocations;
                var ids = new DynamicJsonArray(stats.OrderByDescending(x => x.TotalAllocated).Select(x => new DynamicJsonValue
                {
                    ["Id"] = x.Id,
                    ["Allocations"] = x.TotalAllocated,
                    ["HumaneAllocations"] = Size.Humane(x.TotalAllocated)
                }));
                var groupStats = new DynamicJsonValue
                {
                    ["Name"] = stats.Key,
                    ["Allocations"] = unmanagedAllocations,
                    ["HumaneAllocations"] = Size.Humane(unmanagedAllocations)
                };
                if (ids.Count == 1)
                {
                    groupStats["Id"] = stats.First().Id;
                }
                else
                {
                    groupStats["Ids"] = ids;
                }
                threads.Add(groupStats);
            }
            var managedMemory = GC.GetTotalMemory(false);
            var djv = new DynamicJsonValue
            {
                ["WorkingSet"] = workingSet,
                ["TotalUnmanagedAllocations"] = totalUnmanagedAllocations,
                ["ManagedAllocations"] = managedMemory,
                ["TotalMemoryMapped"] = totalMapping,

                ["Humane"] = new DynamicJsonValue
                {
                    ["WorkingSet"] = Size.Humane(workingSet),
                    ["TotalUnmanagedAllocations"] = Size.Humane(totalUnmanagedAllocations),
                    ["ManagedAllocations"] = Size.Humane(managedMemory),
                    ["TotalMemoryMapped"] = Size.Humane(totalMapping)
                },

                ["Threads"] = threads,

                ["Mappings"] = fileMappings
            };
            return djv;
        }

        private static long GetFileSize(string file)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.Exists == false)
                return -1;
            try
            {
                return fileInfo.Length;
            }
            catch (FileNotFoundException)
            {
                return -1;
            }
        }

        public static int LongestCommonPrefixLength(List<string> strings)
        {
            if (strings.Count == 0)
                return 0;

            strings = strings
                .OrderBy(x => x.Length)
                .ToList();

            var maxLength = strings.Last().Length;
            var shortestString = strings.First();

            var prefixLength = 0;
            foreach (var s in strings)
            {
                if (s == shortestString)
                    continue;

                if (shortestString[prefixLength] != s[prefixLength])
                    prefixLength = 0;

                for (var i = prefixLength; i < shortestString.Length; i++)
                {
                    var shortChar = shortestString[i];
                    var c = s[i];

                    if (shortChar != c)
                    {
                        if (prefixLength == maxLength)
                            return 0;

                        return prefixLength;
                    }

                    prefixLength = i;
                }
            }

            if (prefixLength == maxLength)
                return 0;

            return prefixLength;
        }
    }
}
