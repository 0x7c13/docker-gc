namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class DockerRepositoryDescriptor
    {
        public DockerRepositoryDescriptor() {}

        private static string _indent(int count)
        {
            return "".PadLeft(count);
        }

        private void _printDependencyGraph(DockerImageNode node, IList<DockerImageNode> highlights, IMatchlist containerStateBlacklist, int indentOffset = 0, int indent = 2, string highlightMarker = "<---") 
        {
            Console.Write(_indent(indentOffset) + $"Image: {DockerImageNode.GetImageShortId(node.InspectResponse.ID)} ({node.InspectResponse.RepoTags.FirstOrDefault() ?? "<none>"}) {(int)((DateTime.UtcNow - node.InspectResponse.Created).TotalDays)} days {node.DiskSize/(1024*1024)} MB "); 
            
            if (node.Containers.Count > 0) 
            {
                Console.Write($"({node.GetContainerCount() - node.GetContainerCount(containerStateBlacklist)}/{node.Containers.Count}) ");
            }

            if (highlights.Any(i => string.Equals(i.InspectResponse.ID, node.InspectResponse.ID, StringComparison.InvariantCultureIgnoreCase))) 
            {
                Console.Write($"{highlightMarker} {highlights.IndexOf(node) + 1}");
            }
            
            Console.WriteLine();

            foreach (var child in node.Children) 
            {
                _printDependencyGraph(child, highlights, containerStateBlacklist, indentOffset + indent);
            }
        }

        public void PrintDependencyGraph(IList<DockerImageNode> baseImageNodes, IList<DockerImageNode> highlights, IMatchlist containerStateBlacklist, int indent = 2, string highlightMarker = "<---") 
        {
            Console.WriteLine("Image dependency graph:");
            foreach (var node in baseImageNodes) 
            {
                _printDependencyGraph(node, highlights, containerStateBlacklist);
            }
        }

        public long GetDiskspaceUsage(DockerImageNode image)
        {
            return image.DiskSize + image.Children.Sum(i => GetDiskspaceUsage(i));
        }

        public long GetDiskspaceUsage(IList<DockerImageNode> images)
        {
            return images.Sum(i => GetDiskspaceUsage(i));
        }
    }
}