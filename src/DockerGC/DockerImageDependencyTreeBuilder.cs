namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class DockerImageDependencyTreeBuilder
    {
        // Build image dependency tree
        public DockerImageNode BuildImageTree(IList<ImageInspectResponse> images, 
            IList<ContainerListResponse> containerListResponses,
            IList<ContainerInspectResponse> containerInspectResponses,
            ImageInspectResponse image) 
        {
            var containerList = containerListResponses.Where(r => string.Equals(r.ImageID, image.ID)).ToList();
            IList<ContainerInspectResponse> containers = new List<ContainerInspectResponse>();

            foreach  (var container in containerList) 
            {
                containers.Add(containerInspectResponses.Where(c => string.Equals(c.ID, container.ID)).FirstOrDefault());
            }

            var dockerImageNode = new DockerImageNode()
            {
                InspectResponse = image,
                Children = new List<DockerImageNode>(),
                Containers = containers,
            };

            var children = images.Where(i => string.Equals(i.Parent, image.ID)).ToList();

            foreach (var child in children) 
            {
                var childNode = BuildImageTree(images, containerListResponses, containerInspectResponses, child);
                childNode.Parent = dockerImageNode;
                dockerImageNode.Children.Add(childNode);
            }
            
            return dockerImageNode;
        }

        // Build dependency tree for each root (base image), then return list of base images
        public IList<DockerImageNode> GetBaseImageNodes(IList<ImageInspectResponse> images, IList<ContainerListResponse> containerListResponses, IList<ContainerInspectResponse> containerInspectResponses) 
        {
            return images.Where(i => string.IsNullOrEmpty(i.Parent)).Select(i => BuildImageTree(images, containerListResponses, containerInspectResponses, i)).ToList();
        }
    }
}