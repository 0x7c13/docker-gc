namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class ByDiskSpaceRecyclingStrategy : RecyclingStrategy
    {   
        public static string Name = "ByDiskSpace";

        public double SizeLimitInGigabyte;

        public ByDiskSpaceRecyclingStrategy(double sizeLimitInGigabyte, IMatchlist imageWhitelist, IMatchlist stateBlacklist, int waitToleranceOfBlacklistStateContainersInDays) : base(imageWhitelist, stateBlacklist, waitToleranceOfBlacklistStateContainersInDays)
        {
            if (sizeLimitInGigabyte < 0) 
            {
                throw new ArgumentOutOfRangeException("sizeLimitInGigabyte");
            } 

            this.SizeLimitInGigabyte = sizeLimitInGigabyte;
        }

        private IList<DockerImageNode> _getLeafImageNodes(DockerImageNode imageNode) 
        {
            if (imageNode.Children.Count == 0) 
            {
                return new List<DockerImageNode> { imageNode };
            }

            var leafs = new List<DockerImageNode>();

            foreach(var child in imageNode.Children) 
            {
                leafs.AddRange(_getLeafImageNodes(child));
            }

            return leafs;
        }

        private IList<DockerImageNode> _getParents(DockerImageNode imageNode)
        {
            var node = imageNode;
            var parents = new List<DockerImageNode>(); 
            
            while (node.Parent != null) 
            {
                node = node.Parent;
                parents.Add(node);
            }

            return parents;
        }   

        private long _getTotalDiskSpaceUsage(DockerImageNode imageNode) 
        {
            var diskUsage = imageNode.DiskSize;

            foreach (var child in imageNode.Children) 
            {
                diskUsage += _getTotalDiskSpaceUsage(child);
            }

            return diskUsage;
        }

        private IList<DockerImageNode> _getImagesToBeRecycledInOrder(IList<DockerImageNode> leafNodes, long totalDiskUsage)
        {
            var leafs = leafNodes.OrderBy(o => o.InspectResponse.Created).ToList();
            var imagesCanNotBeDeleted = new List<DockerImageNode>();
            var imagesToBeRecycledInOrder = new List<DockerImageNode>();
            var analyzedImages = new List<DockerImageNode>();

            while (leafs.Count() > 0) 
            {
                if (totalDiskUsage < SizeLimitInGigabyte * 1024 * 1024 * 1024) break;

                var node = leafs.First();
                analyzedImages.Add(node);
                leafs.Remove(node);

                if (imagesCanNotBeDeleted.Contains(node)) continue;
                
                if (!base.CanDelete(node)) 
                {
                    var parents = _getParents(node);
                    imagesCanNotBeDeleted.AddRange(parents);
                    analyzedImages.AddRange(parents);
                    continue;
                }

                imagesToBeRecycledInOrder.Add(node);
                totalDiskUsage -= node.DiskSize;
                
                if (node.Parent == null) continue;

                var allChildImagesHaveBeenAnalyzed = true;
                
                foreach (var child in node.Parent.Children) 
                {
                    if (!analyzedImages.Contains(child)) 
                    {
                        allChildImagesHaveBeenAnalyzed = false;
                        break;
                    }
                }

                if (!allChildImagesHaveBeenAnalyzed) continue;

                var index = 0;
                for (int i = 0; i < leafs.Count; i++) 
                {
                    if (node.Parent.InspectResponse.Created < leafs[i].InspectResponse.Created) 
                    {
                        index = i;
                        break;
                    }
                }
                leafs.Insert(index, node.Parent);
            }
            return imagesToBeRecycledInOrder;
        }

        public override IList<DockerImageNode> GetImagesToBeRecycledInOrder(IList<DockerImageNode> baseImageNodes) 
        {
            var totalDiskUsage = 0L;
            var imagesToBeRecycledInOrder = new List<DockerImageNode>();

            var leafs = new List<DockerImageNode>();

            foreach (var image in baseImageNodes) 
            {
                leafs.AddRange(_getLeafImageNodes(image));
                totalDiskUsage += _getTotalDiskSpaceUsage(image);
            }

            imagesToBeRecycledInOrder = _getImagesToBeRecycledInOrder(leafs, totalDiskUsage).ToList();

            return imagesToBeRecycledInOrder;
        }
    }
}
