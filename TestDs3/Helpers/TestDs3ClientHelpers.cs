﻿/*
 * ******************************************************************************
 *   Copyright 2014 Spectra Logic Corporation. All Rights Reserved.
 *   Licensed under the Apache License, Version 2.0 (the "License"). You may not use
 *   this file except in compliance with the License. A copy of the License is located at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 *   or in the "license" file accompanying this file.
 *   This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *   CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *   specific language governing permissions and limitations under the License.
 * ****************************************************************************
 */

using Ds3;
using Ds3.Calls;
using Ds3.Helpers;
using Ds3.Models;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Range = Ds3.Models.Range;

namespace TestDs3.Helpers
{
    using Stubs = JobResponseStubs;

    [TestFixture]
    public class TestDs3ClientHelpers
    {
        [Test, Timeout(1000)]
        public void BasicReadTransfer()
        {
            var initialJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(null, false, false),
                Stubs.Chunk2(null, false, false),
                Stubs.Chunk3(null, false, false)
            );
            var availableJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(Stubs.NodeId2, true, true),
                Stubs.Chunk2(Stubs.NodeId2, true, true),
                Stubs.Chunk3(Stubs.NodeId1, true, true)
            );

            var node1Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupGetObject(node1Client, "hello", 0L, "ABCDefGHIJ");
            MockHelpers.SetupGetObject(node1Client, "bar", 35L, "zABCDEFGHIJ");

            var node2Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupGetObject(node2Client, "bar", 0L, "0123456789abcde");
            MockHelpers.SetupGetObject(node2Client, "foo", 10L, "klmnopqrst");
            MockHelpers.SetupGetObject(node2Client, "foo", 0L, "abcdefghij");
            MockHelpers.SetupGetObject(node2Client, "bar", 15L, "fghijklmnopqrstuvwxy");

            var clientFactory = new Mock<IDs3ClientFactory>(MockBehavior.Strict);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId1))
                .Returns(node1Client.Object);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId2))
                .Returns(node2Client.Object);

            var client = new Mock<IDs3Client>(MockBehavior.Strict);
            client
                .Setup(c => c.BuildFactory(Stubs.Nodes))
                .Returns(clientFactory.Object);
            client
                .Setup(c => c.BulkGet(MockHelpers.ItIsBulkGetRequest(
                    Stubs.BucketName,
                    ChunkOrdering.None,
                    Stubs.ObjectNames,
                    Enumerable.Empty<Ds3PartialObject>()
                )))
                .Returns(initialJobResponse);
            client
                .Setup(c => c.GetAvailableJobChunks(MockHelpers.ItIsGetAvailableJobChunksRequest(Stubs.JobId)))
                .Returns(GetAvailableJobChunksResponse.Success(TimeSpan.FromMinutes(1), availableJobResponse));

            var job = new Ds3ClientHelpers(client.Object).StartReadJob(
                Stubs.BucketName,
                Stubs.ObjectNames.Select(name => new Ds3Object(name, null))
            );

            var dataTransfers = new ConcurrentQueue<long>();
            var itemsCompleted = new ConcurrentQueue<string>();
            job.DataTransferred += dataTransfers.Enqueue;
            job.ItemCompleted += itemsCompleted.Enqueue;

            var streams = new ConcurrentDictionary<string, MockStream>();
            job.Transfer(key => streams.GetOrAdd(key, k => new MockStream()));

            node1Client.VerifyAll();
            node2Client.VerifyAll();
            clientFactory.VerifyAll();
            client.VerifyAll();

            CollectionAssert.AreEqual(
                new[]
                {
                    new { Key = "bar", Value = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJ" },
                    new { Key = "foo", Value = "abcdefghijklmnopqrst" },
                    new { Key = "hello", Value = "ABCDefGHIJ" },
                },
                from item in streams
                orderby item.Key
                select new { item.Key, Value = MockHelpers.Encoding.GetString(item.Value.Result) }
            );
            CollectionAssert.AreEquivalent(new[] { 15L, 20L, 11L, 10L, 10L, 10L }, dataTransfers);
            CollectionAssert.AreEquivalent(Stubs.ObjectNames, itemsCompleted);
        }

        [Test, Timeout(1000)]
        public void PartialReadTransfer()
        {
            var partialObjects = new[]
            {
                new Ds3PartialObject(Range.ByLength(0L, 4L), "foo"),
                new Ds3PartialObject(Range.ByLength(6L, 10L), "foo"),
                new Ds3PartialObject(Range.ByLength(18L, 1L), "foo"),
                new Ds3PartialObject(Range.ByLength(10L, 26L), "bar"),
            };
            var fullObjects = new[] { "hello" };

            var initialJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(null, false, false),
                Stubs.Chunk2(null, false, false),
                Stubs.Chunk3(null, false, false)
            );
            var availableJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(Stubs.NodeId2, true, true),
                Stubs.Chunk2(Stubs.NodeId2, true, true),
                Stubs.Chunk3(Stubs.NodeId1, true, true)
            );

            var node1Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupGetObject(node1Client, "hello", 0L, "ABCDefGHIJ", Range.ByLength(0L, 10L));
            MockHelpers.SetupGetObject(node1Client, "bar", 35L, "z", Range.ByLength(35L, 1L));

            var node2Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupGetObject(node2Client, "bar", 0L, "abcde", Range.ByLength(10L, 5L));
            MockHelpers.SetupGetObject(node2Client, "foo", 10L, "klmnop!", Range.ByLength(10L, 6L), Range.ByLength(18L, 1L));
            MockHelpers.SetupGetObject(node2Client, "foo", 0L, "abcdghij", Range.ByLength(0L, 4L), Range.ByLength(6L, 4L));
            MockHelpers.SetupGetObject(node2Client, "bar", 15L, "fghijklmnopqrstuvwxy", Range.ByLength(15L, 20L));

            var clientFactory = new Mock<IDs3ClientFactory>(MockBehavior.Strict);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId1))
                .Returns(node1Client.Object);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId2))
                .Returns(node2Client.Object);

            var client = new Mock<IDs3Client>(MockBehavior.Strict);
            client
                .Setup(c => c.BuildFactory(Stubs.Nodes))
                .Returns(clientFactory.Object);
            client
                .Setup(c => c.BulkGet(MockHelpers.ItIsBulkGetRequest(
                    Stubs.BucketName,
                    ChunkOrdering.None,
                    fullObjects,
                    partialObjects
                )))
                .Returns(initialJobResponse);
            client
                .Setup(c => c.GetAvailableJobChunks(MockHelpers.ItIsGetAvailableJobChunksRequest(Stubs.JobId)))
                .Returns(GetAvailableJobChunksResponse.Success(TimeSpan.FromMinutes(1), availableJobResponse));

            var job = new Ds3ClientHelpers(client.Object)
                .StartPartialReadJob(Stubs.BucketName, fullObjects, partialObjects);
            CollectionAssert.AreEquivalent(
                partialObjects.Concat(new[] { new Ds3PartialObject(Range.ByLength(0L, 10L), "hello") }),
                job.AllItems
            );

            var dataTransfers = new ConcurrentQueue<long>();
            var itemsCompleted = new ConcurrentQueue<Ds3PartialObject>();
            job.DataTransferred += dataTransfers.Enqueue;
            job.ItemCompleted += itemsCompleted.Enqueue;

            var streams = new ConcurrentDictionary<Ds3PartialObject, MockStream>();
            job.Transfer(key => streams.GetOrAdd(key, k => new MockStream()));

            node1Client.VerifyAll();
            node2Client.VerifyAll();
            clientFactory.VerifyAll();
            client.VerifyAll();

            var fullObjectPart = new Ds3PartialObject(Range.ByLength(0L, 10L), fullObjects[0]);
            CollectionAssert.AreEqual(
                new[]
                {
                    new { Key = partialObjects[0], Value = "abcd" },
                    new { Key = partialObjects[1], Value = "ghijklmnop" },
                    new { Key = partialObjects[2], Value = "!" },
                    new { Key = partialObjects[3], Value = "abcdefghijklmnopqrstuvwxyz" },
                    new { Key = fullObjectPart, Value = "ABCDefGHIJ" },
                }.OrderBy(it => it.Key).ToArray(),
                (
                    from item in streams
                    orderby item.Key
                    select new { item.Key, Value = MockHelpers.Encoding.GetString(item.Value.Result) }
                ).ToArray()
            );
            CollectionAssert.AreEquivalent(
                new[] { 1L, 1L, 4L, 4L, 5L, 6L, 10L, 20L },
                dataTransfers.Sorted().ToArray()
            );
            CollectionAssert.AreEquivalent(partialObjects.Concat(new[] { fullObjectPart }), itemsCompleted);
        }

        [Test, Timeout(1000), TestCase(1048576L), TestCase(null)]
        public void BasicWriteTransfer(long? maxBlobSize)
        {
            var initialJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(null, false, false),
                Stubs.Chunk2(null, false, false),
                Stubs.Chunk3(null, false, false)
            );

            var node1Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupPutObject(node1Client, "hello", 0L, "ABCDefGHIJ");
            MockHelpers.SetupPutObject(node1Client, "bar", 35L, "zABCDEFGHIJ");

            var node2Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupPutObject(node2Client, "bar", 0L, "0123456789abcde");
            MockHelpers.SetupPutObject(node2Client, "foo", 10L, "klmnopqrst");
            MockHelpers.SetupPutObject(node2Client, "foo", 0L, "abcdefghij");
            MockHelpers.SetupPutObject(node2Client, "bar", 15L, "fghijklmnopqrstuvwxy");

            var clientFactory = new Mock<IDs3ClientFactory>(MockBehavior.Strict);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId1))
                .Returns(node1Client.Object);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId2))
                .Returns(node2Client.Object);

            var streams = new Dictionary<string, MockStream>
            {
                { "bar", new MockStream("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJ") },
                { "foo", new MockStream("abcdefghijklmnopqrst") },
                { "hello", new MockStream("ABCDefGHIJ") },
            };
            var ds3Objects = Stubs
                .ObjectNames
                .Select(name => new Ds3Object(name, streams[name].Length));

            var client = new Mock<IDs3Client>(MockBehavior.Strict);
            client
                .Setup(c => c.BuildFactory(Stubs.Nodes))
                .Returns(clientFactory.Object);
            client
                .Setup(c => c.BulkPut(MockHelpers.ItIsBulkPutRequest(Stubs.BucketName, ds3Objects, maxBlobSize)))
                .Returns(initialJobResponse);
            client
                .Setup(c => c.AllocateJobChunk(MockHelpers.ItIsAllocateRequest(Stubs.ChunkId1)))
                .Returns(AllocateJobChunkResponse.Success(Stubs.Chunk1(Stubs.NodeId2, false, false)));
            client
                .Setup(c => c.AllocateJobChunk(MockHelpers.ItIsAllocateRequest(Stubs.ChunkId2)))
                .Returns(AllocateJobChunkResponse.Success(Stubs.Chunk2(Stubs.NodeId2, false, false)));
            client
                .Setup(c => c.AllocateJobChunk(MockHelpers.ItIsAllocateRequest(Stubs.ChunkId3)))
                .Returns(AllocateJobChunkResponse.Success(Stubs.Chunk3(Stubs.NodeId1, false, false)));

            var job = new Ds3ClientHelpers(client.Object).StartWriteJob(Stubs.BucketName, ds3Objects, maxBlobSize);

            var dataTransfers = new ConcurrentQueue<long>();
            var itemsCompleted = new ConcurrentQueue<string>();
            job.DataTransferred += dataTransfers.Enqueue;
            job.ItemCompleted += itemsCompleted.Enqueue;

            job.Transfer(key => streams[key]);

            node1Client.VerifyAll();
            node2Client.VerifyAll();
            clientFactory.VerifyAll();
            client.VerifyAll();

            CollectionAssert.AreEquivalent(new[] { 15L, 20L, 11L, 10L, 10L, 10L }, dataTransfers);
            CollectionAssert.AreEquivalent(Stubs.ObjectNames, itemsCompleted);
        }

        [Test]
        public void ReadTransferFailsUponTransferrerException()
        {
            var initialJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk1(null, false, false),
                Stubs.Chunk2(null, false, false),
                Stubs.Chunk3(null, false, false)
            );
            var availableJobResponse = Stubs.BuildJobResponse(
                Stubs.Chunk2(Stubs.NodeId2, true, true)
            );

            var node2Client = new Mock<IDs3Client>(MockBehavior.Strict);
            MockHelpers.SetupGetObject(node2Client, "foo", 0L, "abcdefghij");
            node2Client
                .Setup(c => c.GetObject(MockHelpers.ItIsGetObjectRequest(
                    Stubs.BucketName,
                    "bar",
                    Stubs.JobId,
                    15L,
                    Enumerable.Empty<Range>()
                )))
                .Throws<NullReferenceException>();

            var clientFactory = new Mock<IDs3ClientFactory>(MockBehavior.Strict);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId2))
                .Returns(node2Client.Object);

            var client = new Mock<IDs3Client>(MockBehavior.Strict);
            client
                .Setup(c => c.BuildFactory(Stubs.Nodes))
                .Returns(clientFactory.Object);
            client
                .Setup(c => c.BulkGet(MockHelpers.ItIsBulkGetRequest(
                    Stubs.BucketName,
                    ChunkOrdering.None,
                    Stubs.ObjectNames,
                    Enumerable.Empty<Ds3PartialObject>()
                )))
                .Returns(initialJobResponse);
            client
                .Setup(c => c.GetAvailableJobChunks(MockHelpers.ItIsGetAvailableJobChunksRequest(Stubs.JobId)))
                .Returns(GetAvailableJobChunksResponse.Success(TimeSpan.FromMinutes(1), availableJobResponse));

            var job = new Ds3ClientHelpers(client.Object).StartReadJob(
                Stubs.BucketName,
                Stubs.ObjectNames.Select(name => new Ds3Object(name, null))
            );
            try
            {
                job.Transfer(key => new MockStream());
                Assert.Fail("Should have thrown an exception.");
            }
            catch (AggregateException e)
            {
                Assert.IsInstanceOf<NullReferenceException>(e.InnerException);
            }
        }

        [Test]
        public void TestListObjects()
        {
            var ds3ClientMock = new Mock<IDs3Client>(MockBehavior.Strict);
            ds3ClientMock
                .Setup(client => client.GetBucket(It.IsAny<GetBucketRequest>()))
                .Returns(new Queue<GetBucketResponse>(new[] {
                    MockHelpers.CreateGetBucketResponse(
                        marker: "",
                        nextMarker: "baz",
                        isTruncated: true,
                        ds3objectInfos: new List<Ds3ObjectInfo> {
                            MockHelpers.BuildDs3Object("foo", "2cde576e5f5a613e6cee466a681f4929", "2009-10-12T17:50:30.000Z", 12), MockHelpers.BuildDs3Object("bar", "f3f98ff00be128139332bcf4b772be43", "2009-10-14T17:50:31.000Z", 12)
                        }
                    ),
                    MockHelpers.CreateGetBucketResponse(
                        marker: "baz",
                        nextMarker: "",
                        isTruncated: false,
                        ds3objectInfos: new List<Ds3ObjectInfo> {
                            MockHelpers.BuildDs3Object("baz", "802d45fcb9a3f7d00f1481362edc0ec9", "2009-10-18T17:50:35.000Z", 12)
                        }
                    )
                }).Dequeue);

            var objects = new Ds3ClientHelpers(ds3ClientMock.Object).ListObjects("mybucket").ToList();

            Assert.AreEqual(3, objects.Count);
            MockHelpers.CheckContents(objects[0], "foo", 12);
            MockHelpers.CheckContents(objects[1], "bar", 12);
            MockHelpers.CheckContents(objects[2], "baz", 12);
        }

        [Test, Timeout(1000)]
        public void PartialObjectReturn()
        {
            var initialJobResponse = Stubs.BuildJobResponse(
                Stubs.ReadFailureChunk(null, false)
            );
            var availableJobResponse = Stubs.BuildJobResponse(
            
                Stubs.ReadFailureChunk(Stubs.NodeId1, true)
            );

            var node1Client = new Mock<IDs3Client>(MockBehavior.Strict);            
            MockHelpers.SetupGetObjectWithContentLengthMismatchException(node1Client, "bar", 0L, "ABCDEFGHIJ", 20L, 10L); // The initial request is for all 20 bytes, but only the first 10 will be sent
            MockHelpers.SetupGetObject(node1Client, "bar", 0L, "JLMNOPQRSTU", Range.ByPosition(9L, 19L));  // The client will request the full last byte based off of when the client fails

            var clientFactory = new Mock<IDs3ClientFactory>(MockBehavior.Strict);
            clientFactory
                .Setup(cf => cf.GetClientForNodeId(Stubs.NodeId1))
                .Returns(node1Client.Object);
            
            var client = new Mock<IDs3Client>(MockBehavior.Strict);
            client
                .Setup(c => c.BuildFactory(Stubs.Nodes))
                .Returns(clientFactory.Object);
            client
                .Setup(c => c.BulkGet(MockHelpers.ItIsBulkGetRequest(
                    Stubs.BucketName,
                    ChunkOrdering.None,
                    Stubs.ObjectNames,
                    Enumerable.Empty<Ds3PartialObject>()
                )))
                .Returns(initialJobResponse);
            client
                .Setup(c => c.GetAvailableJobChunks(MockHelpers.ItIsGetAvailableJobChunksRequest(Stubs.JobId)))
                .Returns(GetAvailableJobChunksResponse.Success(TimeSpan.FromMinutes(1), availableJobResponse));

            var job = new Ds3ClientHelpers(client.Object).StartReadJob(
                Stubs.BucketName,
                Stubs.ObjectNames.Select(name => new Ds3Object(name, null))
            );

            var dataTransfers = new ConcurrentQueue<long>();
            var itemsCompleted = new ConcurrentQueue<string>();
            job.DataTransferred += dataTransfers.Enqueue;
            job.ItemCompleted += itemsCompleted.Enqueue;

            var streams = new ConcurrentDictionary<string, MockStream>();
            job.Transfer(key => streams.GetOrAdd(key, k => new MockStream()));

            node1Client.VerifyAll();            
            clientFactory.VerifyAll();
            client.VerifyAll();

            // Since we are using a mock for the underlying client, the first request does not write any content to the stream
            CollectionAssert.AreEqual(
                new[]
                {
                    new { Key = "bar", Value = "ABCDEFGHIJLMNOPQRSTU" },
                },
                from item in streams
                orderby item.Key
                select new { item.Key, Value = MockHelpers.Encoding.GetString(item.Value.Result) }
            );
            CollectionAssert.AreEquivalent(new[] { 20L }, dataTransfers);
            CollectionAssert.AreEquivalent(Stubs.PartialFailureObjectNames, itemsCompleted);
        }
    }
}
