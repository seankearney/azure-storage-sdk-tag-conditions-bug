using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestProject1
{
    public class BlobStorageUploadTests : IClassFixture<TestFixture>
    {
        private TestFixture Fixture { get; }

        public BlobStorageUploadTests(TestFixture fixture)
        {
            Fixture = fixture;
            Fixture.KeepUserContainerAfterTestIsDone = false;
        }

        /*
         Note:
            A `RetryableStreamImpl` is the underlying Stream provided when we use BlobClient.DownloadAsync()...Value.Content;
            See https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/src/Shared/RetriableStream.cs
         */

        [Fact]
        public async Task Upload_blob_from_RetryableStreamImpl_should_work()
        {
            BlobClient blobClient = Fixture.GetBlobClient(Guid.NewGuid().ToString());

            await using var sourceStream = await Fixture.GetSampleBlob();
            await blobClient.UploadAsync(sourceStream).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }

        [Fact]
        public async Task Upload_blob_from_RetryableStreamImpl_with_tags_should_work()
        {
            BlobClient blobClient = Fixture.GetBlobClient(Guid.NewGuid().ToString());

            var options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "1" } }
            };

            await using var sourceStream = await Fixture.GetSampleBlob();
            await blobClient.UploadAsync(sourceStream, options).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }

        /// <summary>
        /// ISSUE: Uploading a stream provided from a different DownloadBlob, when we have a BlobRequestCondition shouldn't fail
        /// </summary>
        [Fact]
        public async Task ISSUE__First_upload_of_blob_from_RetryableStreamImpl_with_tags_and_conditions_should_work()
        {
            BlobClient blobClient = Fixture.GetBlobClient(Guid.NewGuid().ToString());

            var options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "1" } },
                Conditions = new BlobRequestConditions
                {
                    TagConditions = $@"""MyTag"" < '1'"
                }
            };

            await using var sourceStream = await Fixture.GetSampleBlob();
            await blobClient.UploadAsync(sourceStream, options).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }



        [Fact]
        public async Task Second_upload_of_blob_from_RetryableStreamImpl_with_tags_and_valid_conditions_should_overwrite_blob()
        {
            BlobClient blobClient = Fixture.GetBlobClient(Guid.NewGuid().ToString());

            var options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "0" } }
            };

            await using var sourceStream = await Fixture.GetSampleBlob();
            await blobClient.UploadAsync(sourceStream, options).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);

            // Reupload with conditions
            options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "1" } },
                Conditions = new BlobRequestConditions
                {
                    TagConditions = $@"""MyTag"" < '1'"
                }
            };

            await using var sourceStream2 = await Fixture.GetSampleBlob(TestFixture.Samples.sample2);
            await blobClient.UploadAsync(sourceStream2, options).ConfigureAwait(false);

            text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal(TestFixture.Samples.sample2.ToString(), text);
        }

        [Fact]
        public async Task Second_upload_of_blob_from_RetryableStreamImpl_with_tags_and_INvalid_conditions_should_NOT_overwrite_blob()
        {
            BlobClient blobClient = Fixture.GetBlobClient(Guid.NewGuid().ToString());

            var options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "10" } }
            };

            await using var sourceStream = await Fixture.GetSampleBlob();
            await blobClient.UploadAsync(sourceStream, options).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);

            // Reupload with conditions
            options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "MyTag", "1" } },
                Conditions = new BlobRequestConditions
                {
                    TagConditions = $@"""MyTag"" < '1'"
                }
            };

            await using var sourceStream2 = await Fixture.GetSampleBlob(TestFixture.Samples.sample2);

            try
            {
                await blobClient.UploadAsync(sourceStream2, options).ConfigureAwait(false);
            }
            catch (RequestFailedException exception) when (exception.ErrorCode == BlobErrorCode.ConditionNotMet)
            {
                // We are expecting this! The BlobRequestConditions aren't met
            }

            // We should still have original content here
            text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        public async Task Blobs_larger_than_4mb_uploaded_with_BlobRequestConditions_fails_with_conditions_not_met(int numberOfMegs)
        {
          Fixture.KeepUserContainerAfterTestIsDone = true;
          string fileName = Guid.NewGuid().ToString("N");

            // Getnerate an _x_ MB stream
            await using var data = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 1_024 * 1_024 * numberOfMegs)));

            BlobClient blobClient = Fixture.GetBlobClient(fileName);
            var localId = 213700871031095297;

            var options = new BlobUploadOptions
            {
                Tags = new Dictionary<string, string> { { "LocalId", localId.ToString() } },
                Conditions = new BlobRequestConditions
                {
                    TagConditions = $@"""LocalId"" < '{localId}'"
                }
            };

            await blobClient.UploadAsync(data, options).ConfigureAwait(false);
        }
    }
}
