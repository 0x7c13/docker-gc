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

        private DockerImageDependencyTreePrinter _dependencyGraphPrinter;

        private IMatchlist _containerStateBlacklist;

        private ILogger _logger;

        private int _executionIntervalInMinutes;

        private RecyclingStrategy _recyclingStrategy;

        public DockerGarbageCollector(IDockerClient dockerClient, 
            DockerImageDependencyTreeBuilder dependencyGraphBuilder,
            DockerImageDependencyTreePrinter dependencyGraphPrinter,
            RecyclingStrategy recyclingStrategy,
            IMatchlist containerStateBlacklist,
            int executionIntervalInMinutes,
            ILogger logger)
        {

            _dockerClient = dockerClient;
            _dependencyGraphBuilder = dependencyGraphBuilder;
            _dependencyGraphPrinter = dependencyGraphPrinter;
            _containerStateBlacklist = containerStateBlacklist;
            _logger = logger;
            _executionIntervalInMinutes = executionIntervalInMinutes;
            _recyclingStrategy = recyclingStrategy;
        }

        private void DeleteImages(IList<DockerImageNode> images) 
        {
            var recycledDiskSize = 0L;
            var deletedImages = 0;

            foreach (var image in images)
            {
                Console.WriteLine($"Deleting image: {image.InspectResponse.ID} {image.InspectResponse.RepoTags.FirstOrDefault()}");
               
                try 
                {
                    // Remove exited/dead containers
                    foreach (var container in image.Containers) 
                    {
                        _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters() { Force = true }).Wait();
                    }
                
                    // Delete image
                    _dockerClient.Images.DeleteImageAsync(image.InspectResponse.ID, new ImageDeleteParameters() { Force = true, PruneChildren = true }).Wait();
                    recycledDiskSize += image.DiskSize;
                    deletedImages++;
                }
                catch (Exception e) // Low chance to hit race condition, catch the exception here but keep on deleting
                {
                    Console.Error.WriteLine(e);
                }               
            }
            
            Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {deletedImages} images deleted (total of {(int)(recycledDiskSize / (1024 * 1024))} MB). DockerGC will re-evaluate in {_executionIntervalInMinutes} minutes");
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

                    if (imagesToBeRecycled.Count > 0) 
                    {
#if DEBUG
                    // print dependency graph with images to be deleted as highlights
                        _dependencyGraphPrinter.Print(baseImageNodes, imagesToBeRecycled, _containerStateBlacklist);
#else
                        DeleteImages(imagesToBeRecycled);    
#endif 
                    }
                    else
                    {
                        if (_executionIntervalInMinutes > 0) 
                        {
                            Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] No matching images were found. DockerGC will re-evaluate in {_executionIntervalInMinutes} minutes");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }

                if (_executionIntervalInMinutes > 0) 
                {
                    System.Threading.Thread.Sleep(1000 * 60 * _executionIntervalInMinutes);
                }
                
            } while (_executionIntervalInMinutes != -1);
        }
    }
}
