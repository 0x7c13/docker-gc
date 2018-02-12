namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class DockerImageNode
    {
        public ImageInspectResponse InspectResponse { get; set; }

        public IList<ContainerInspectResponse> Containers { get; set; }
        
        public DockerImageNode Parent { get; set; }

        public IList<DockerImageNode> Children { get; set; }
        
        public long DiskSize { 

            get 
            {
                if (Parent == null) 
                {
                    return InspectResponse.Size;
                } 
                else 
                {
                    return InspectResponse.Size - Parent.InspectResponse.Size;
                }
            }
        }

        public DockerImageNode()
        {
            Containers = new List<ContainerInspectResponse>();
            Children = new List<DockerImageNode>();
        }

        public int GetContainerCount() 
        {
            return Containers.Count;
        }

        public int GetContainerCount(IMatchlist states) 
        {
            var count = 0;
                
            foreach (var container in Containers) 
            {
                // created      A container that has been created (e.g. with docker create) but not started
                // restarting   A container that is in the process of being restarted
                // running      A currently running container
                // paused       A container whose processes have been paused
                // exited       A container that ran and completed ("stopped" in other contexts, although a created container is technically also "stopped")
                // dead         A container that the daemon tried and failed to stop (usually due to a busy device or resource used by the container)
                // removing

                if (states.Match(container.State.Status))
                {
                    count++;
                }
            }
            return count;
        }

        public DateTime GetMostRecentFinshedAtTimestampOfAllContainers(IMatchlist states) 
        {
            DateTime timestamp = DateTime.MinValue;

            foreach (var container in Containers.Where(c => states.Match(c.State.Status))) 
            {
                var time = DateTime.Parse(container.State.FinishedAt);
                if (time > timestamp) timestamp = time;
            }

            return timestamp;
        }
    }
}