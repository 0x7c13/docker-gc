namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public abstract class RecyclingStrategy
    {
        private IMatchlist _imageWhitelist;
        private IMatchlist _stateBlacklist;

        public RecyclingStrategy(IMatchlist imageWhitelist, IMatchlist stateBlacklist) 
        {
            this._imageWhitelist = imageWhitelist;
            this._stateBlacklist = stateBlacklist;
        }

        public abstract IList<DockerImageNode> GetImagesToBeRecycledInOrder(IList<DockerImageNode> baseImageNodes);

        public bool CanDelete(DockerImageNode imageNode) 
        {
            // We DO NOT delete image if it is in the whitelist
            if (this._imageWhitelist.MatchAny(imageNode.InspectResponse.RepoTags))
            {
                return false;
            }

            // We DO NOT delete image if there is a non blacklist state container exists using that image
            if (imageNode.GetContainerCount() - imageNode.GetContainerCount(_stateBlacklist) > 0) 
            {
                return false;
            }

            return true;
        }
    }
}
