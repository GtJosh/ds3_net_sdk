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
using Ds3.Runtime;

using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
using System.Text;

namespace Ds3Examples
{
    class Ds3ExampleClient
    {
        protected IDs3Client _client;
        protected IDs3ClientHelpers _helpers;

        public Ds3ExampleClient(string endpoint, Credentials credentials, string proxy)
        {
            Ds3Builder builder = new Ds3Builder(endpoint, credentials);
            if (!string.IsNullOrEmpty(proxy))
            {
                builder.WithProxy(new Uri(proxy));
            }
            _client = builder.Build();

            // Set up the high-level abstractions.
            _helpers = new Ds3ClientHelpers(_client);
        }

        protected string runPing()
        {
            var verify = VerifySystemHealth();
            Console.WriteLine("VerifySystemHealth() -- {0}ms", verify.MillisToVerify);

            GetSystemInformationResponse sysinf = GetSystemInfo();

            return string.Format("Object 'MC5: {0}' | SN: {1} | BuildVer: {2} | BuildRev: {3} | BuildPath: {4} ", sysinf.ApiMC5, sysinf.SerialNumber, sysinf.BuildVersion, sysinf.BuildRev, sysinf.BuildBranch);
        }

        protected void runCreateBucket(string bucket)
        {
            // Creates a bucket if it does not already exist.
            _helpers.EnsureBucketExists(bucket);
        }

        protected void runPutFromStream(string bucket, string filename, long size)
        {
            _helpers.EnsureBucketExists(bucket);

            // create Ds3Object to store name and size
            var ds3Obj = new Ds3Object(filename, size);
            var ds3Objs = new List<Ds3Object>();
            ds3Objs.Add(ds3Obj);

            // start bulk put for objects list
            IJob job = _helpers.StartWriteJob(bucket, ds3Objs);
            if (clientSwitch.TraceInfo) { Trace.WriteLine(string.Format("runPutFromStream(): Job id {0}", job.JobId)); }
            Console.WriteLine("Started job runPutFromStream()");

            // Provide Func<string, stream> to be called on each object
            job.Transfer(key =>
            {
                var data = new byte[size];
                var stream = new MemoryStream(data);
                for (int i = 0; i < size; i++)
                {
                    stream.WriteByte((byte)(i & 0x7f));
                }

                stream.Seek(0, SeekOrigin.Begin);

                return stream;
            });
        }

        protected void runPut(string bucket, string srcDirectory, string filename)
        {
            // Creates a bucket if it does not already exist.
            _helpers.EnsureBucketExists(bucket);

            // get file size, instantiate Ds3Object, add to list
            FileInfo fileinf =  new FileInfo(Path.Combine(srcDirectory, filename));
            var ds3Obj = new Ds3Object(filename, fileinf.Length);
            var ds3Objs = new List<Ds3Object>();
            ds3Objs.Add(ds3Obj);

            // Creates a bulk job with the server based on the files in a directory (recursively).
            IJob job = _helpers.StartWriteJob(bucket, ds3Objs);
            if (clientSwitch.TraceInfo) { Trace.WriteLine(string.Format("runPut({1}): Job id {0}", job.JobId, filename)); }
            Console.WriteLine(string.Format("Started job runPut({0})", filename));

            // Provide Func<string, stream> to be called on each object
            job.Transfer(FileHelpers.BuildFilePutter(srcDirectory));
        }

        protected void runBulkPut(string bucket, string srcDirectory, string prefix = "")
        {
            // Creates a bucket if it does not already exist.
            _helpers.EnsureBucketExists(bucket);

            // Creates a bulk job with the server based on the files in a directory (recursively).
            IJob job = _helpers.StartWriteJob(bucket, FileHelpers.ListObjectsForDirectory(srcDirectory, prefix));
            if (clientSwitch.TraceInfo) { Trace.WriteLine(string.Format("runBulkPut(): Job id {0}", job.JobId)); }
            Console.WriteLine("Started job runBulkPut()");

            // Transfer all of the files.
            job.Transfer(FileHelpers.BuildFilePutter(srcDirectory, prefix));
        }

        protected bool runBulkGet(string bucket, string directory, string prefix)
        {
            // Creates a bulk job with all of the objects in the bucket.
            IJob job = _helpers.StartReadAllJob(bucket);
            // Same as: IJob job = helpers.StartReadJob(bucket, helpers.ListObjects(bucket));
            if (clientSwitch.TraceInfo) { Trace.WriteLine(string.Format("runBulkGet(): Job id {0}", job.JobId)); }
            Console.WriteLine("Started job runBulkGet()");

            // Transfer all of the files.
            job.Transfer(FileHelpers.BuildFileGetter(directory, prefix));

            return true;
        }

        protected bool runGet(string bucket, string directory, string filename)
        {
            // find the desired object 
            var objects = _helpers.ListObjects(bucket);
            var targetobj = (from o in objects
                         where o.Name == filename
                         select o);

            // get it
            IJob job = _helpers.StartReadJob(bucket, targetobj);
            if (clientSwitch.TraceInfo) { Trace.WriteLine(string.Format("runGet({1}): Job id {0}", job.JobId, filename)); }
            Console.WriteLine(string.Format("Started job runGet({0})", filename));

            // Transfer all of the files.
            job.Transfer(FileHelpers.BuildFileGetter(directory, string.Empty));

            return true;
        }

        public void runDeleteObject(string bucketname, string objectName)
        {
            var request = new Ds3.Calls.DeleteObjectRequest(bucketname, objectName);
            _client.DeleteObject(request);
        }

        public void runDeleteBucket(string bucketname)
        {
            var request = new Ds3.Calls.DeleteBucketRequest(bucketname);
            _client.DeleteBucket(request);
        }

        public void runDeleteFolder(string bucketname, string folderName)
        {
            var request = new Ds3.Calls.DeleteFolderRequest(bucketname, folderName);
            _client.DeleteFolder(request);
        }

        protected long runListObjects(string bucket)
        {
            var items = _helpers.ListObjects(bucket);

            Console.WriteLine("------ in {0}: ", bucket);
            // Loop through all of the objects in the bucket.
            foreach (var obj in items)
            {
                Console.WriteLine("Object '{0}' of size {1}.", obj.Name, obj.Size);
            }
            return items.Count<Ds3Object>();
        }
        
        protected bool runDeleteObjects(string bucket)
        {
            var items = _helpers.ListObjects(bucket);

            Console.WriteLine("----- In {0}: ", bucket);

            // Loop through all of the objects in the bucket.
            foreach (var obj in items)
            {
                Console.WriteLine("Deleting '{0}' of size {1}.", obj.Name, obj.Size);
                runDeleteObject(bucket, obj.Name);
            }
            return true;
        }

        protected long runListBuckets()
        {
            var buckets = _client.GetService(new GetServiceRequest()).Buckets;
            foreach (var bucket in buckets)
            {
                Console.WriteLine("Bucket '{0}'.", bucket.Name);
            }
            return buckets.Count;
        }

        protected bool runListAll()
        {
            var buckets = _client.GetService(new GetServiceRequest()).Buckets;
            foreach (var bucket in buckets)
            {
                runListObjects(bucket.Name);
            }
            return buckets.Count > 0;
        }

        protected bool runCleanAll(string match)
        {
            var buckets = _client.GetService(new GetServiceRequest()).Buckets;
            var matchbuckets = from b in buckets
                               where b.Name.StartsWith(match)
                               select b;
            foreach (var bucket in matchbuckets)
            {
                runDeleteObjects(bucket.Name);
                runDeleteBucket(bucket.Name);
            }
            return buckets.Count > 0;
        }

        protected bool runGetObjects(string bucket, string name, string objid, long length, long offset, DS3ObjectTypes type, long version)
        {
            var items = GetObjects(bucket, name, objid, length, offset, type, version);

            // Loop through all of the objects in the bucket.
            foreach (var obj in items)
            {
                Console.WriteLine("Object '{0}' | {1} | {2} | {3} ", obj.Name, obj.Version, obj.Type, obj.CreationDate);
            }
            return true;
        }

        protected bool runListDir(string sourcedir, string prefix)
        {
            var items =  FileHelpers.ListObjectsForDirectory(sourcedir, prefix);

            // Loop through all of the objects in the bucket.
            foreach (var obj in items)
            {
                Console.WriteLine("Object '{0}' ", obj.Name);
            }
            return true;
        }


        public IEnumerable<DS3GetObjectsInfo> GetObjects(string bucketName, string objectName, string ds3ObjectId, long length, long offset, DS3ObjectTypes type, long version)
        {
            var request = new Ds3.Calls.GetObjectsRequest()
            {
                BucketId = bucketName,
                ObjectName = objectName,
                ObjectId = ds3ObjectId,
                Length = length,
                Offset = offset,
                ObjectType = type,
                Version = version
            };
            var response = _client.GetObjects(request);
            foreach (var ds3Object in response.Objects)
            {
                yield return ds3Object;
            }
        }
  
        public GetSystemInformationResponse GetSystemInfo()
        {
            var request = new Ds3.Calls.GetSystemInformationRequest();
            return _client.GetSystemInformation(request);
        }

        public VerifySystemHealthResponse VerifySystemHealth()
        {
            var request = new Ds3.Calls.VerifySystemHealthRequest();
            return _client.VerifySystemHealth(request);
        }


        #region main()

        private static TraceSwitch clientSwitch = new TraceSwitch("clientSwitch","test switch");

        static void Main(string[] args)
        {
            if (clientSwitch.TraceInfo) { Trace.WriteLine("Starting Ds3ExampleClient main()"); }
            if (clientSwitch.TraceWarning) { Trace.WriteLine("Almost out of coffee"); }
            if (clientSwitch.TraceVerbose) { Trace.WriteLine("I think I should share some of my feelings..."); }

            // set the following environment variables or pass in from App.config
            string endpoint = Environment.GetEnvironmentVariable("DS3_ENDPOINT");
            string accesskey = Environment.GetEnvironmentVariable("DS3_ACCESS_KEY");
            string secretkey = Environment.GetEnvironmentVariable("DS3_SECRET_KEY");
            string proxy = Environment.GetEnvironmentVariable("http_proxy");

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accesskey) || string.IsNullOrEmpty(secretkey))
            {
                string fatalerr = "Must set values for DS3_ENDPOINT, DS3_ACCESS_KEY, and DS3_SECRET_KEY to continue";
                Console.WriteLine(fatalerr);
                if (clientSwitch.TraceWarning) { Trace.WriteLine(fatalerr); }
                return;
            } // if connection paramaters defined

            // out-of-box examples in project
            string bucket = "sdkexamples";
            string testRestoreDirectory = "DataFromBucket";
            string testSourceDirectory = "TestData";
            string testSourceSubDirectory = "FScottKey";
            string testSourceFile = "LoremIpsum.txt";
            string binaryfile = "binarytest";
            long binaryfilesize = 4096L;

            // set these values to valid locations on local filesystem
            // directory to be copied (should exist and be poulated)
            string sourcedir = "C:\\TestObjectData";
            // destination for restore if not empty (will be created if needed)
            string destdir = "";

            // optional prefix to prepend to filename
            string prefix = "mytest123_";

            // instantiate client
            Ds3ExampleClient exampleclient = new Ds3ExampleClient(endpoint, 
                                    new Credentials(accesskey, secretkey), proxy);

            try
            {
                // set up test files
                Ds3ExampleClient.SetupFiles(testSourceDirectory, testSourceSubDirectory);

                // connect to machine
                string systeminfo = exampleclient.runPing();
                if (clientSwitch.TraceVerbose) { Trace.WriteLine(systeminfo); }

                // List all contents before operations
                Console.WriteLine("\nSTARTING STATE:");
                exampleclient.runListAll();

                // force removal of test bucket from previous executions.
                exampleclient.runCleanAll(bucket);

                #region put objects
                /*************************************************
                 *** PUT FILES FROM LOCAL FILESYSTEM TO DEVICE ***
                 *************************************************/
                // create a bucket on the device
                exampleclient.runCreateBucket(bucket);

                // put a single file into the bucket from stream
                exampleclient.runPut(bucket, testSourceDirectory, testSourceFile);

                // put a file into the bucket from stream
                exampleclient.runPutFromStream(bucket, binaryfile, binaryfilesize);

                // copy the whole directory with a file prefix
                exampleclient.runBulkPut(bucket, testSourceDirectory, prefix);

                // copy a local directory, recursively into the bucket
                if (Directory.Exists(sourcedir))
                {
                    exampleclient.runBulkPut(bucket, sourcedir);
                }
                else
                {
                    if (clientSwitch.TraceInfo) { Trace.WriteLine("set srcDirectory variable to put local data"); }
                }
                // List all contents
                Console.WriteLine("\nAFTER PUT:");
                exampleclient.runListAll();
                
                #endregion put objects

                #region list objects
                /*************************************************
                 ***  LIST OBJECT NAMES FROM DEVICE            ***
                 *************************************************/
                Console.WriteLine("\nLIST:");
                
                // get bucket list
                Console.WriteLine("Buckets:");
                long bucketcount = exampleclient.runListBuckets();

                // get object list
                Console.WriteLine("Objects in {0}:", bucket);
                long objectcount = exampleclient.runListObjects(bucket);
                
                // get object list in pages
                Console.WriteLine("Objects in {0}:", bucket);
                string objid = null;
                string objectname = null;
                DS3ObjectTypes type = DS3ObjectTypes.ALL;
                long version = 1L;
                long pagesize = objectcount / 3L;
                for (long offset = 0L; offset < objectcount; offset += pagesize)
                {
                    Console.WriteLine(string.Format("Get {0} (offset = {1})", bucket, offset));
                    exampleclient.runGetObjects(bucket, objectname, objid, pagesize, offset, type, version);
                }

                #endregion listobjects

                #region get objects
                /*************************************************
                 *** RESTORE OBJECTS FROM DEVICE TO FILESYSTEM ***
                 *************************************************/

                // get single file from out-of-box example
                exampleclient.runGet(bucket, testRestoreDirectory, testSourceFile);

                // restore whole bucket into local directory
                if (!string.IsNullOrEmpty(destdir))
                {
                    exampleclient.runBulkGet(bucket, destdir, string.Empty);
                }
                
                #endregion get objects



                #region delete objects

                /*************************************************
                 ***         DELETE FILES FROM DEVICE          ***
                 *************************************************/
                // delete a single object
                exampleclient.runDeleteObject(bucket, testSourceFile);

                // delete all objects in a folder
                exampleclient.runDeleteFolder(bucket, testSourceSubDirectory);

                // delete all objects in a bucket but not the bucket
                exampleclient.runDeleteObjects(bucket);

                // delete an empty bucket
                exampleclient.runDeleteBucket(bucket);

                // List all contents 
                Console.WriteLine("\nAFTER DELETE:");
                exampleclient.runListAll();

                #endregion delete objects

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Wait for user input.
            Console.WriteLine("All jobs complete. Hit any key to exit.");
            Console.ReadLine();
        }

        #endregion main()

        #region setup
        
        public static void SetupFiles(string testSourceDirectory, string testSourceSubDirectory)
        {
            string[] files = { "one.txt", "two.txt", "three.txt", "four.txt" };
            string testdata = "On the shore dimly seen, through the mists of the deep. ";
            testdata += "Where our foe's haughty host in dread silence reposes. ";
            testdata += "What is that which the breeze, o'er the towering steep, ";
            testdata += "As it fitfully blows, half conceals half discloses? ";
            testdata += "Now it catches the gleam of the morning’s first beam, ";
            testdata += "In full glory reflected now shines in the stream: ";
            testdata += "Tis the star-spangled banner! Oh long may it wave, ";
            testdata += "O’er the land of the free and the home of the brave!";
 
            // create and populate a new test dir
            if (Directory.Exists(testSourceDirectory))
            {
                string subdir = Path.Combine(testSourceDirectory, testSourceSubDirectory);
                Directory.CreateDirectory(subdir);
                foreach (var file in files)
                {
                    TextWriter writer = new StreamWriter(Path.Combine(subdir, file));
                    writer.WriteLine(testdata);
                    writer.Close();
                }
            }
        }

        #endregion setup
    }
}
