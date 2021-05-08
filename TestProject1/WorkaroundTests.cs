using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace TestProject1
{
    public class WorkaroundTests : IClassFixture<TestFixture>
    {
        private TestFixture Fixture { get; }

        public WorkaroundTests(TestFixture fixture)
        {
            Fixture = fixture;
            Fixture.KeepUserContainerAfterTestIsDone = false;
        }

        /// <summary>
        /// WORKAROUND: When we put the RetryableStreamImpl into a MemoryStream we can workaround the issue
        /// </summary>
        [Fact]
        public async Task WORKAROUND__First_upload_of_blob_from_RetryableStreamImpl_with_tags_and_conditions_should_work()
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

            // WORKAROUND
            await using var memoryStream = new MemoryStream();
            await sourceStream.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0;
            // /WORKAROUND

            await blobClient.UploadAsync(memoryStream, options).ConfigureAwait(false);

            string text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }

        /// <summary>
        /// Testing to make sure that putting pumping into a MemoryStream doesn't break conditions
        /// </summary>
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

            // WORKAROUND
            await using var memoryStream = new MemoryStream();
            await sourceStream2.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0;
            // /WORKAROUND

            await blobClient.UploadAsync(memoryStream, options).ConfigureAwait(false);

            text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal(TestFixture.Samples.sample2.ToString(), text);
        }

        /// <summary>
        /// Testing to make sure that putting pumping into a MemoryStream doesn't break conditions
        /// </summary>
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

            // WORKAROUND
            await using var memoryStream = new MemoryStream();
            await sourceStream2.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0;
            // /WORKAROUND

            try
            {
                await blobClient.UploadAsync(memoryStream, options).ConfigureAwait(false);
            }
            catch (RequestFailedException exception) when (exception.ErrorCode == BlobErrorCode.ConditionNotMet)
            {
                // We are expecting this! The BlobRequestConditions aren't met
            }

            // We should still have original content here
            text = await TestFixture.DownloadAndReadBlobContents(blobClient).ConfigureAwait(false);
            Assert.Equal("sample", text);
        }
    }
}
