﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FastTests;
using FastTests.Server.Documents.Revisions;
using Raven.Client;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Smuggler;
using Raven.Server.Documents;
using Raven.Server.Utils;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Client.Attachments
{
    public class AttachmentsSmuggler : RavenTestBase
    {
        [Fact]
        public async Task ExportAndDeleteAttachmentThanCreateAnotherOneAndImport()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store1"
                }))
                {
                    using (var session = store.OpenSession())
                    {
                        session.Store(new User { Name = "Fitzchak" }, "users/1");
                        session.SaveChanges();
                    }

                    using (var stream = new MemoryStream(new byte[] { 1, 2, 3 }))
                        store.Operations.Send(new PutAttachmentOperation("users/1", "file1", stream, "image/png"));

                    await store.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    store.Operations.Send(new DeleteAttachmentOperation("users/1", "file1"));

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(0, stats.CountOfAttachments);
                    Assert.Equal(0, stats.CountOfUniqueAttachments);

                    using (var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
                        store.Operations.Send(new PutAttachmentOperation("users/1", "file2", stream, "image/jpeg"));

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    await store.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(2, stats.CountOfAttachments);
                    Assert.Equal(2, stats.CountOfUniqueAttachments);

                    using (var session = store.OpenSession())
                    {
                        var user = session.Load<User>("users/1");

                        var metadata = session.Advanced.GetMetadataFor(user);
                        Assert.Equal(DocumentFlags.HasAttachments.ToString(), metadata[Constants.Documents.Metadata.Flags]);
                        var attachments = metadata.GetObjects(Constants.Documents.Metadata.Attachments);
                        Assert.Equal(2, attachments.Length);
                        Assert.Equal("file1", attachments[0].GetString(nameof(AttachmentName.Name)));
                        Assert.Equal("EcDnm3HDl2zNDALRMQ4lFsCO3J2Lb1fM1oDWOk2Octo=", attachments[0].GetString(nameof(AttachmentName.Hash)));
                        Assert.Equal("image/png", attachments[0].GetString(nameof(AttachmentName.ContentType)));
                        Assert.Equal(3, attachments[0].GetLong(nameof(AttachmentName.Size)));

                        Assert.Equal("file2", attachments[1].GetString(nameof(Attachment.Name)));
                        Assert.Equal("Arg5SgIJzdjSTeY6LYtQHlyNiTPmvBLHbr/Cypggeco=", attachments[1].GetString(nameof(AttachmentName.Hash)));
                        Assert.Equal("image/jpeg", attachments[1].GetString(nameof(AttachmentName.ContentType)));
                        Assert.Equal(5, attachments[1].GetLong(nameof(AttachmentName.Size)));
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task ExportAndDeleteAttachmentAndImport()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store1"
                }))
                {
                    using (var session = store.OpenSession())
                    {
                        session.Store(new User { Name = "Fitzchak" }, "users/1");
                        session.SaveChanges();
                    }

                    using (var stream = new MemoryStream(new byte[] { 1, 2, 3 }))
                        store.Operations.Send(new PutAttachmentOperation("users/1", "file1", stream, "image/png"));

                    await store.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    store.Operations.Send(new DeleteAttachmentOperation("users/1", "file1"));

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(0, stats.CountOfAttachments);
                    Assert.Equal(0, stats.CountOfUniqueAttachments);

                    await store.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    using (var session = store.OpenSession())
                    {
                        var user = session.Load<User>("users/1");

                        var metadata = session.Advanced.GetMetadataFor(user);
                        Assert.Equal(DocumentFlags.HasAttachments.ToString(), metadata[Constants.Documents.Metadata.Flags]);
                        var attachments = metadata.GetObjects(Constants.Documents.Metadata.Attachments);
                        var attachment = attachments.Single();
                        Assert.Equal("file1", attachment.GetString(nameof(AttachmentName.Name)));
                        Assert.Equal("EcDnm3HDl2zNDALRMQ4lFsCO3J2Lb1fM1oDWOk2Octo=", attachment.GetString(nameof(AttachmentName.Hash)));
                        Assert.Equal("image/png", attachment.GetString(nameof(AttachmentName.ContentType)));
                        Assert.Equal(3, attachment.GetLong(nameof(AttachmentName.Size)));
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task ExportWithoutAttachmentAndCreateOneAndImport()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store1"
                }))
                {
                    using (var session = store.OpenSession())
                    {
                        session.Store(new User { Name = "Fitzchak" }, "users/1");
                        session.SaveChanges();
                    }

                    await store.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(0, stats.CountOfAttachments);
                    Assert.Equal(0, stats.CountOfUniqueAttachments);

                    using (var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
                        store.Operations.Send(new PutAttachmentOperation("users/1", "file2", stream, "image/jpeg"));

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    await store.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    stats = await store.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(1, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    using (var session = store.OpenSession())
                    {
                        var user = session.Load<User>("users/1");

                        var metadata = session.Advanced.GetMetadataFor(user);
                        Assert.Equal(DocumentFlags.HasAttachments.ToString(), metadata[Constants.Documents.Metadata.Flags]);
                        var attachments = metadata.GetObjects(Constants.Documents.Metadata.Attachments);
                        var attachment = attachments.Single();
                        Assert.Equal("file2", attachment.GetString(nameof(Attachment.Name)));
                        Assert.Equal("Arg5SgIJzdjSTeY6LYtQHlyNiTPmvBLHbr/Cypggeco=", attachment.GetString(nameof(AttachmentName.Hash)));
                        Assert.Equal("image/jpeg", attachment.GetString(nameof(AttachmentName.ContentType)));
                        Assert.Equal(5, attachment.GetLong(nameof(AttachmentName.Size)));
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task ExportEmptyStream()
        {
            var file = Path.GetTempFileName();
            try
            {
                var dbId2 = new Guid("99999999-48c4-421e-9466-999999999999");
                var dbId = new Guid("00000000-48c4-421e-9466-000000000000");

                using (var store1 = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store1"
                }))
                {
                    await SetDatabaseId(store1, dbId);

                    await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database, false, 4);
                    using (var session = store1.OpenSession())
                    {
                        session.Store(new User { Name = "Fitzchak" }, "users/1");
                        session.SaveChanges();
                    }
                    using (var emptyStream = new MemoryStream(new byte[0]))
                    {
                        var result = store1.Operations.Send(new PutAttachmentOperation("users/1", "empty-file", emptyStream, "image/png"));
                        Assert.Equal("A:3", result.ChangeVector.Substring(0, 3));
                        Assert.Equal("empty-file", result.Name);
                        Assert.Equal("users/1", result.DocumentId);
                        Assert.Equal("image/png", result.ContentType);
                        Assert.Equal("DldRwCblQ7Loqy6wYJnaodHl30d3j3eH+qtFzfEv46g=", result.Hash);
                        Assert.Equal(0, result.Size);
                    }

                    await store1.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store1.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(2, stats.CountOfRevisionDocuments);
                    Assert.Equal(2, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);
                }

                using (var store2 = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store2"
                }))
                {
                    await SetDatabaseId(store2, dbId2);

                    await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);

                    await store2.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store2.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(3, stats.CountOfRevisionDocuments);
                    Assert.Equal(2, stats.CountOfAttachments);
                    Assert.Equal(1, stats.CountOfUniqueAttachments);

                    using (var session = store2.OpenSession())
                    {
                        var readBuffer = new byte[1024 * 1024];
                        using (var attachmentStream = new MemoryStream(readBuffer))
                        using (var attachment = session.Advanced.GetAttachment("users/1", "empty-file"))
                        {
                            attachment.Stream.CopyTo(attachmentStream);
                            Assert.Contains("A:1", attachment.Details.ChangeVector);
                            Assert.Equal("empty-file", attachment.Details.Name);
                            Assert.Equal(0, attachment.Details.Size);
                            Assert.Equal("DldRwCblQ7Loqy6wYJnaodHl30d3j3eH+qtFzfEv46g=", attachment.Details.Hash);
                            Assert.Equal(0, attachmentStream.Position);
                            Assert.Equal(new byte[0], readBuffer.Take((int)attachmentStream.Position));
                        }
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task CanExportAndImportAttachmentsAndRevisionAttachments()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store1 = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store1"
                }))
                {
                    await SetDatabaseId(store1, new Guid("00000000-48c4-421e-9466-000000000000"));
                    await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database, false, 4);
                    AttachmentsRevisions.CreateDocumentWithAttachments(store1);
                    using (var bigStream = new MemoryStream(Enumerable.Range(1, 999 * 1024).Select(x => (byte)x).ToArray()))
                        store1.Operations.Send(new PutAttachmentOperation("users/1", "big-file", bigStream, "image/png"));

                    /*var result = */
                    await store1.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);
                    // TODO: RavenDB-6936 store.Smuggler.Export and Import method should return the SmugglerResult

                    var stats = await store1.Admin.SendAsync(new GetStatisticsOperation());
                    Assert.Equal(1, stats.CountOfDocuments);
                    Assert.Equal(4, stats.CountOfRevisionDocuments);
                    Assert.Equal(14, stats.CountOfAttachments);
                    Assert.Equal(4, stats.CountOfUniqueAttachments);
                }

                using (var store2 = GetDocumentStore(new Options
                {
                    ModifyDatabaseName = s => $"{s}_store2"
                }))
                {
                    var dbId = new Guid("00000000-48c4-421e-9466-000000000000");
                    await SetDatabaseId(store2, dbId);

                    await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);

                    for (var i = 0; i < 2; i++) // Make sure that we can import attachments twice and it will overwrite
                    {
                        await store2.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                        var stats = await store2.Admin.SendAsync(new GetStatisticsOperation());
                        Assert.Equal(1, stats.CountOfDocuments);
                        Assert.Equal(5, stats.CountOfRevisionDocuments);
                        Assert.Equal(14, stats.CountOfAttachments);
                        Assert.Equal(4, stats.CountOfUniqueAttachments);

                        using (var session = store2.OpenSession())
                        {
                            var readBuffer = new byte[1024 * 1024];
                            using (var attachmentStream = new MemoryStream(readBuffer))
                            using (var attachment = session.Advanced.GetAttachment("users/1", "big-file"))
                            {
                                attachment.Stream.CopyTo(attachmentStream);
                                Assert.Contains("A:" + (2 + 20 * i), attachment.Details.ChangeVector);
                                Assert.Equal("big-file", attachment.Details.Name);
                                Assert.Equal("zKHiLyLNRBZti9DYbzuqZ/EDWAFMgOXB+SwKvjPAINk=", attachment.Details.Hash);
                                Assert.Equal(999 * 1024, attachmentStream.Position);
                                Assert.Equal(Enumerable.Range(1, 999 * 1024).Select(x => (byte)x), readBuffer.Take((int)attachmentStream.Position));
                            }
                        }
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
