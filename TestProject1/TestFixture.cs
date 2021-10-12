using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestProject1
{
    public class TestFixture : IAsyncLifetime
    {
        public enum Samples { sample, sample2 }

        public readonly string CONTAINER_NAME = "repro";

        public IConfiguration Configuration { get; private set; }

        public bool KeepUserContainerAfterTestIsDone { get; set; } = false;

        public string BlobStorageConnectionString { get; private set; }

        public async Task InitializeAsync()
        {
            // load configuration
            var config = new ConfigurationBuilder();
            config.AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.local.json", optional: true);

            Configuration = config.Build();

            BlobStorageConnectionString = Configuration["BlobStorageConnectionString"];

            await CreateTestRunContainer();
            await CreateSampleBlobs();
        }

        public Task DisposeAsync()
        {
            if (KeepUserContainerAfterTestIsDone)
            {
                return Task.CompletedTask;
            }

            BlobServiceClient blobServiceClient = CreateServiceClient(BlobStorageConnectionString);
            return blobServiceClient.DeleteBlobContainerAsync(CONTAINER_NAME);
        }

        /// <summary>
        /// Gets a blob's Stream to be used a source for a new blob.
        /// This Stream is a <see cref="Azure.Core.Pipeline.RetriableStream.RetriableStreamImpl"/>
        /// See: https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/src/Shared/RetriableStream.cs
        /// </summary>
        public async Task<Stream> GetSampleBlob(Samples sample = Samples.sample)
        {
            BlobServiceClient blobServiceClient = CreateServiceClient(BlobStorageConnectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("sources");
            BlobClient blobClient = blobContainerClient.GetBlobClient($"{sample.ToString()}.dat");
            var blob = await blobClient.DownloadAsync().ConfigureAwait(false);
            return blob.Value.Content;
        }

        public BlobClient GetBlobClient(string blobName)
        {
            BlobServiceClient blobServiceClient = CreateServiceClient(BlobStorageConnectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            return blobClient;
        }

        /// <summary>
        /// Downloads the blob and reads the text contents of the blob
        /// </summary>
        public static async Task<string> DownloadAndReadBlobContents(BlobClient blobClient)
        {
            var downloadedBlob = await blobClient.DownloadAsync().ConfigureAwait(false);
            var stream = downloadedBlob.Value.Content;

            // RetriableStreamImpl.Length isn't supported... so off to memory stream we go again
            await using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0;

            var buffer = new byte[memoryStream.Length];
            await memoryStream.ReadAsync(buffer);
            var text = Encoding.UTF8.GetString(buffer);
            await stream.DisposeAsync();
            return text;
        }

        private async Task CreateSampleBlobs()
        {
            BlobServiceClient blobServiceClient = CreateServiceClient(BlobStorageConnectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("sources");

            if (!await blobContainerClient.ExistsAsync())
            {
                blobContainerClient = await blobServiceClient.CreateBlobContainerAsync("sources");
            }
            // Create two sample blobs we will pull down later
            BlobClient blobClient = blobContainerClient.GetBlobClient("sample.dat");
            if (!await blobClient.ExistsAsync())
            {
                await using var sampleData = new MemoryStream(Encoding.UTF8.GetBytes("sample"));
                await blobClient.UploadAsync(sampleData, false);
            }
            blobClient = blobContainerClient.GetBlobClient("sample2.dat");
            if (!await blobClient.ExistsAsync())
            {
                await using var sampleData = new MemoryStream(Encoding.UTF8.GetBytes("sample2"));
                await blobClient.UploadAsync(sampleData, false);
            }
        }

        private async Task CreateTestRunContainer()
        {
            BlobServiceClient blobServiceClient = CreateServiceClient(BlobStorageConnectionString);
            await blobServiceClient.CreateBlobContainerAsync(CONTAINER_NAME);
        }

        private static BlobServiceClient CreateServiceClient(string connectionString) => new BlobServiceClient(connectionString);
    }
}
