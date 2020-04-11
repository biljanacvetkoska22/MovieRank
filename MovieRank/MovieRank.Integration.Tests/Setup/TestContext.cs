using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace MovieRank.Integration.Tests.Setup
{
    public class TestContext : IAsyncLifetime
    {
        private TestServer _server;
        private readonly DockerClient _dockerClient;
        public HttpClient Client { get; set; }

        private const string ContainerImageUri = "amazon/dynamodb-local";
        private string _containerId;

        public TestContext()
        {            
            SetupClient();

            _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
        }

        public async Task InitializeAsync()
        {
            await PullImage();

            await StartContainer();

            await new TestDataSetup().CreateTable();
        }

        private async Task PullImage()
        {
            await _dockerClient.Images
                .CreateImageAsync(new ImagesCreateParameters
                    {
                        FromImage = ContainerImageUri,
                        Tag = "latest"
                    },
                    new AuthConfig(),
                    new Progress<JSONMessage>());
        }

        private async Task StartContainer()
        {
            var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = ContainerImageUri,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    {
                        "8000", default(EmptyStruct)
                    }
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {"8000", new List<PortBinding> {new PortBinding {HostPort = "8000" } }}
                    },
                    PublishAllPorts = true
                }
            });

            _containerId = response.ID;

            await _dockerClient.Containers.StartContainerAsync(_containerId, null);
        }
        private void SetupClient()
        {
            _server = new TestServer(new WebHostBuilder()
                .UseEnvironment("Development")
                .UseStartup<Startup>());

            _server.BaseAddress = new Uri("http://localhost:8000");

            Client = _server.CreateClient();
        }

        public async Task DisposeAsync()
        {
            if (_containerId != null)
            {
                await _dockerClient.Containers.KillContainerAsync(_containerId, new ContainerKillParameters());
            }
        }
    }
}