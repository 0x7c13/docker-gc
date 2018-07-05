namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class ByDateRecyclingStrategy : RecyclingStrategy
    {
        public int DaysBeforeDeletion;

        public ImageDeletionOrderType ImageDeletionOrder;

        private IDictionary<string, DateTime> _imageLastTouchDate;

        public ByDateRecyclingStrategy(int daysBeforeDeletion, IMatchlist imageWhitelist, IMatchlist stateBlacklist,  IDictionary<string, DateTime> imageLastTouchDate, ImageDeletionOrderType deletionOrder) : base(imageWhitelist, stateBlacklist)
        {
            if (daysBeforeDeletion < 0) 
            {
                throw new ArgumentOutOfRangeException("daysBeforeDeletion");
            } 
            this.DaysBeforeDeletion = daysBeforeDeletion;
            this.ImageDeletionOrder = deletionOrder;
            this._imageLastTouchDate = imageLastTouchDate;
        }

        // Returns true when all child images can be deleted, meaning all child images have no running containers (all dead or exited)
        // and they are old enough to be safely deleted.
        private bool _canDelete(DockerImageNode image, List<DockerImageNode> imagesToBeRecycledInOrder)
        {
            var canDelete = true;

            foreach (var child in image.Children) 
            {
                if (!_canDelete(child, imagesToBeRecycledInOrder)) canDelete = false;
                else imagesToBeRecycledInOrder.Add(child);
            }

            if (ImageDeletionOrder is ImageDeletionOrderType.ByImageCreationDate)
            {
                if (image.InspectResponse.Created >= DateTime.UtcNow.AddDays(-this.DaysBeforeDeletion))
                {
                    return false;
                }
            }
            else if (ImageDeletionOrder is ImageDeletionOrderType.ByImageLastTouchDate)
            {
                var imageLastTouchDate = image.GetImageLastTouchDate(this._imageLastTouchDate);

                if (imageLastTouchDate >= DateTime.UtcNow.AddDays(-this.DaysBeforeDeletion))
                {
                    return false;
                }
            }

            if (!base.CanDelete(image)) canDelete = false;

            return canDelete;
        }

        public override IList<DockerImageNode> GetImagesToBeRecycledInOrder(IList<DockerImageNode> baseImageNodes)
        {
            var imagesToBeRecycledInOrder = new List<DockerImageNode>();

            foreach (var image in baseImageNodes) 
            {
                if (_canDelete(image, imagesToBeRecycledInOrder)) 
                {
                    imagesToBeRecycledInOrder.Add(image);
                }
            }

            return imagesToBeRecycledInOrder;
        }
    }
}
