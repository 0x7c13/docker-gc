namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class DockerImageDependencyTreePrinter
    {
        private int _indent;

        private string _highlightMarker;

        public DockerImageDependencyTreePrinter(int indent = 2, string highlightMarker = "<---") 
        {
            _indent = indent;
            _highlightMarker = highlightMarker;
        }

        public static string Indent(int count)
        {
            return "".PadLeft(count);
        }

        public static string GetImageShortId(string longId)
        {
            return longId.Substring("sha256:".Length, 12);
        }

        private void _print(DockerImageNode node, IList<DockerImageNode> highlights, IMatchlist containerStateBlacklist, int indent = 0) 
        {
            Console.Write(Indent(indent) + $"Image: {GetImageShortId(node.InspectResponse.ID)} ({node.InspectResponse.RepoTags.FirstOrDefault()}) {(int)((DateTime.UtcNow - node.InspectResponse.Created).TotalDays)} days {node.DiskSize/(1024*1024)} MB "); 
            
            if (node.Containers.Count > 0) 
            {
                Console.Write($"({node.GetContainerCount() - node.GetContainerCount(containerStateBlacklist)}/{node.Containers.Count}) ");
            }

            if (highlights.Any(i => string.Equals(i.InspectResponse.ID, node.InspectResponse.ID, StringComparison.InvariantCultureIgnoreCase))) 
            {
                Console.Write($"{_highlightMarker} {highlights.IndexOf(node) + 1}");
            }
            
            Console.WriteLine();

            foreach (var child in node.Children) 
            {
                _print(child, highlights, containerStateBlacklist, indent + _indent);
            }
        }

        public void Print(IList<DockerImageNode> baseImageNodes, IList<DockerImageNode> highlights, IMatchlist containerStateBlacklist) 
        {
            Console.WriteLine("Print image dependency graph:");
            foreach (var node in baseImageNodes) 
            {
                _print(node, highlights, containerStateBlacklist);
            }
        }
    }
}