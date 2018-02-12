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
        private int _waitToleranceOfBlacklistStateContainersInDays;

        public RecyclingStrategy(IMatchlist imageWhitelist, IMatchlist stateBlacklist, int waitToleranceOfBlacklistStateContainersInDays) 
        {
            if (waitToleranceOfBlacklistStateContainersInDays < 0) 
            {
                throw new ArgumentOutOfRangeException("waitToleranceOfBlacklistStateContainersInDays");
            } 
            _imageWhitelist = imageWhitelist;
            _stateBlacklist = stateBlacklist;
            _waitToleranceOfBlacklistStateContainersInDays = waitToleranceOfBlacklistStateContainersInDays;
        }

        public abstract IList<DockerImageNode> GetImagesToBeRecycledInOrder(IList<DockerImageNode> baseImageNodes);

        public bool CanDelete(DockerImageNode imageNode) 
        {
            // We DO NOT delete image if it is in the whitelist
            if (_imageWhitelist.MatchAny(imageNode.InspectResponse.RepoTags))
            {
                return false;
            }

            // We DO NOT delete image if there is a non blacklist state container exists using that image
            if (imageNode.GetContainerCount() - imageNode.GetContainerCount(_stateBlacklist) > 0) 
            {
                return false;
            }

            // We choose to wait for few days before removing the image when all containers are in blacklist state
            if (imageNode.GetContainerCount(_stateBlacklist) > 0)
            {
                var days = (DateTime.UtcNow - imageNode.GetMostRecentFinshedAtTimestampOfAllContainers(_stateBlacklist)).Days;
                if (days < _waitToleranceOfBlacklistStateContainersInDays) { return false; }
            }

            return true;
        }
    }
}
