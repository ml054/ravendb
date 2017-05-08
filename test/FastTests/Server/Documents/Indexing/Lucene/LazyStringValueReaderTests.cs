﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Raven.Server.Json;
using Sparrow.Json;
using Xunit;

namespace FastTests.Server.Documents.Indexing.Lucene
{
    public class LazyStringValueReaderTests : RavenLowLevelTestBase
    {
        private readonly LazyStringReader _sut = new LazyStringReader();
        private readonly JsonOperationContext _ctx;

        public LazyStringValueReaderTests()
        {
            _ctx = JsonOperationContext.ShortTermSingleUse();
        }

        [Theory]
        [InlineData("ąęłóżźćń")]
        [InlineData("לכובע שלי שלוש פינות")]
        public void Reads_unicode(string expected)
        {
            using (var lazyString = _ctx.GetLazyString(expected))
            {
                var stringResult = LazyStringReader.GetStringFor(lazyString);
                var readerResult = _sut.GetTextReaderFor(lazyString);

                Assert.Equal(expected, stringResult);
                Assert.Equal(expected, readerResult.ReadToEnd());
            }
        }

        [Theory]
        [InlineData(1024)]
        [InlineData(1500)]
        [InlineData(2048)]
        [InlineData(3000)]
        public void Reads_very_long_text(int length)
        {
            var expected = new string('a', length);
            using (var lazyString = _ctx.GetLazyString(expected))
            {
                var stringResult = LazyStringReader.GetStringFor(lazyString);
                var readerResult = _sut.GetTextReaderFor(lazyString);

                Assert.Equal(expected, stringResult);
                Assert.Equal(expected, readerResult.ReadToEnd());
            }
        }

        [Fact]
        public void Can_reuse_reader_multiple_times()
        {
            var r = new Random();
            
            for (int i = 0; i < 10; i++)
            {
                var bytes = new byte[r.Next(1, 2000)];
                r.NextBytes(bytes);

                var expected = Encoding.UTF8.GetString(bytes);

                var lazyString = _ctx.GetLazyString(expected);
             
                var stringResult = LazyStringReader.GetStringFor(lazyString);
                var readerResult = _sut.GetTextReaderFor(lazyString);

                Assert.Equal(expected, stringResult);
                Assert.Equal(expected, readerResult.ReadToEnd());
                _ctx.ReturnMemory(lazyString.AllocatedMemoryData);
            }
            
        }


        public override void Dispose()
        {
            _ctx.Dispose();

            base.Dispose();
        }
    }
}