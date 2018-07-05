namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class ByDiskSpaceRecyclingStrategy : RecyclingStrategy
    {   
        public double SizeLimitInGigabyte;

        public ImageDeletionOrderType ImageDeletionOrder;

        private IDictionary<string, DateTime> _imageLastTouchDate;

        public ByDiskSpaceRecyclingStrategy(double sizeLimitInGigabyte, IMatchlist imageWhitelist, IMatchlist stateBlacklist, IDictionary<string, DateTime> imageLastTouchDate, ImageDeletionOrderType deletionOrder) : base(imageWhitelist, stateBlacklist)
        {
            if (sizeLimitInGigabyte < 0) 
            {
                throw new ArgumentOutOfRangeException("sizeLimitInGigabyte");
            } 

            this.SizeLimitInGigabyte = sizeLimitInGigabyte;
            this.ImageDeletionOrder = deletionOrder;
            this._imageLastTouchDate = imageLastTouchDate;
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
            IList<DockerImageNode> leafs = null;

            if (ImageDeletionOrder is ImageDeletionOrderType.ByImageCreationDate)
            {
                leafs = leafNodes.OrderBy(o => o.InspectResponse.Created).ToList();
            }
            else if (ImageDeletionOrder is ImageDeletionOrderType.ByImageLastTouchDate)
            {
                leafs = leafNodes.OrderBy(o => o.GetImageLastTouchDate(this._imageLastTouchDate)).ToList();
            }

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

                // Put new leaf node (current node's parent) in the queue by creation date or last touch date order
                _insertNodeIntoLeafsQueue(leafs, node.Parent);
            }
            return imagesToBeRecycledInOrder;
        }

        private void _insertNodeIntoLeafsQueue(IList<DockerImageNode> leafs, DockerImageNode node)
        {
            var index = leafs.Count;
            for (int i = 0; i < leafs.Count; i++) 
            {
                if (ImageDeletionOrder is ImageDeletionOrderType.ByImageCreationDate)
                {
                    if (node.InspectResponse.Created < leafs[i].InspectResponse.Created) 
                    {
                        index = i;
                        break;
                    }
                }
                else if (ImageDeletionOrder is ImageDeletionOrderType.ByImageLastTouchDate)
                {
                    if (node.GetImageLastTouchDate(this._imageLastTouchDate) < leafs[i].GetImageLastTouchDate(this._imageLastTouchDate)) 
                    {
                        index = i;
                        break;
                    }
                }
            }
            leafs.Insert(index, node);
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
