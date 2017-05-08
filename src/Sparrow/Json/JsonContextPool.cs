﻿namespace Sparrow.Json
{
    public class JsonContextPool : JsonContextPoolBase<JsonOperationContext>
    {
        protected override JsonOperationContext CreateContext()
        {
            if (Platform.PlatformDetails.Is32Bits)
                return new JsonOperationContext(4096, 16 * 1024, LowMemoryFlag);
                
            return new JsonOperationContext(1024*1024, 16*1024, LowMemoryFlag);
        }
    }
}