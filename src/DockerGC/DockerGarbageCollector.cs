namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class DockerGarbageCollector
    {
        private IDockerClient _dockerClient;

        private DockerImageDependencyTreeBuilder _dependencyGraphBuilder;

        private DockerRepositoryDescriptor _repoDescriptor;

        private IMatchlist _containerStateBlacklist;

        private ILogger _logger;

        private int _executionIntervalInMinutes;

        private RecyclingStrategy _recyclingStrategy;

        public DockerGarbageCollector(IDockerClient dockerClient, 
            DockerImageDependencyTreeBuilder dependencyGraphBuilder,
            DockerRepositoryDescriptor repoDescriptor,
            RecyclingStrategy recyclingStrategy,
            IMatchlist containerStateBlacklist,
            int executionIntervalInMinutes,
            ILogger logger)
        {

            this._dockerClient = dockerClient;
            this._dependencyGraphBuilder = dependencyGraphBuilder;
            this._repoDescriptor = repoDescriptor;
            this._containerStateBlacklist = containerStateBlacklist;
            this._logger = logger;
            this._executionIntervalInMinutes = executionIntervalInMinutes;
            this._recyclingStrategy = recyclingStrategy;
        }

        private void StopAndRemoveContainers(DockerImageNode image)
        {
            var imageShortId = DockerImageNode.GetImageShortId(image.InspectResponse.ID);

            // Remove containers in black list states
            foreach (var container in image.Containers) 
            {
                var status = _dockerClient.Containers.InspectContainerAsync(container.ID).Result.State.Status;
                // If container state changed and not in black list states, we should skip deleting
                if (!_containerStateBlacklist.Match(status))
                {
                    throw new Exception($"Postponed deletion for image: {imageShortId} due to container status change");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Stop and remove container: {container.ID} using image: {imageShortId}");
                    _dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters()).Wait();
                    _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters() { Force = false, RemoveLinks = false, RemoveVolumes = false }).Wait();
                }
            }
        }

        private void DeleteImage(DockerImageNode image, string repoTag)
        {
            var imageShortId = DockerImageNode.GetImageShortId(image.InspectResponse.ID);
            var imageRepoTag = repoTag ?? "<none>";

            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Preparing to delete image: {imageShortId} ({imageRepoTag})");

            // Delete image
            if (!string.IsNullOrEmpty(repoTag))
            {
                _dockerClient.Images.DeleteImageAsync($"{imageRepoTag}", new ImageDeleteParameters() { Force = false, PruneChildren = true }).Wait();
            }
            else
            { 
                _dockerClient.Images.DeleteImageAsync(image.InspectResponse.ID, new ImageDeleteParameters() { Force = false, PruneChildren = true }).Wait();
            }

            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Image deleted: {imageShortId} ({imageRepoTag})");
        }

        private void DeleteImages(IList<DockerImageNode> images) 
        {
            var recycledDiskSize = 0L;
            var deletedImages = 0;

            var dic = new Dictionary<string, int>();

            foreach (var image in images)
            {
                var imageShortId = DockerImageNode.GetImageShortId(image.InspectResponse.ID);
                var imageRepoTags = image.InspectResponse.RepoTags;

                try
                {
                    // Remove containers in black list states
                    StopAndRemoveContainers(image);

                    // Delete tagged image references
                    foreach (var repoTag in imageRepoTags) 
                    {
                        DeleteImage(image, repoTag);
                    }

                    // Delete untagged image
                    if (imageRepoTags.Count() == 0) 
                    {
                        DeleteImage(image, null);
                    }
                
                    recycledDiskSize += image.DiskSize;
                    deletedImages++;
                }
                // If container created before we delete the image, docker will throw exception
                // In this case, we catch the exception here but keep on deleting
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {e}");
                }
            }

            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {deletedImages} images deleted (total of {(int)(recycledDiskSize / (1024 * 1024))} MB)");
            _logger.LogCounter("images-recycled-count", deletedImages);
            _logger.LogCounter("disk-space-recycled-mb", (int)(recycledDiskSize / (1024 * 1024)));
        }

        public void Run() 
        {
            do
            {
                try 
                {
                    var imageListResponses = _dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true }).Result;
                    var images = imageListResponses.Select(i => _dockerClient.Images.InspectImageAsync(i.ID).Result).ToList();
                    var containerListResponses = _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true }).Result;
                    var containers = containerListResponses.Select(i => _dockerClient.Containers.InspectContainerAsync(i.ID).Result).ToList();
                    var baseImageNodes = _dependencyGraphBuilder.GetBaseImageNodes(images, containerListResponses, containers);
                    var imagesToBeRecycled = _recyclingStrategy.GetImagesToBeRecycledInOrder(baseImageNodes);

                    var currentDiskUsage = _repoDescriptor.GetDiskspaceUsage(baseImageNodes);
                    Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Current disk usage by docker images: {currentDiskUsage/(1024*1024*1024)} GB");

                    if (imagesToBeRecycled.Count > 0) 
                    {
                        DeleteImages(imagesToBeRecycled);
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] No matching images were found");
                    }

                    if (_executionIntervalInMinutes > 0) 
                    {   
                        Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc will re-evaluate in {_executionIntervalInMinutes} minutes");
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {e}");
                }

                if (_executionIntervalInMinutes > 0) 
                {
                    System.Threading.Thread.Sleep(1000 * 60 * _executionIntervalInMinutes);
                }
                
            } while (_executionIntervalInMinutes != -1);

            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc finished");
        }
    }
}