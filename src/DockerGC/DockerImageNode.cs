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

        public static string GetImageShortId(string longId)
        {
            return longId.Substring("sha256:".Length, 12);
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

        public DateTime GetMostRecentTouchDateOfAllContainers()
        {
            DateTime lastTouchDate = DateTime.MinValue;

            foreach (var container in Containers) 
            {
                lastTouchDate = container.Created;

                if (!string.IsNullOrEmpty(container.State?.FinishedAt))
                {
                    var time = DateTime.Parse(container.State.FinishedAt);
                    if (time > lastTouchDate) lastTouchDate = time;
                }

                if (!string.IsNullOrEmpty(container.State?.StartedAt))
                {
                    var time = DateTime.Parse(container.State.StartedAt);
                    if (time > lastTouchDate) lastTouchDate = time;
                }
            }

            return lastTouchDate;
        }

        public DateTime GetImageLastTouchDate(IDictionary<string, DateTime> imageLastTouchDate) 
        {
            DateTime lastTouchDate = DateTime.MinValue;
            DateTime time;

            if (imageLastTouchDate.TryGetValue(this.InspectResponse.ID, out time))
            {
                lastTouchDate = time;
            }
            else if (GetContainerCount() > 0) // Query existing containers for most recent touch time
            {
                lastTouchDate = GetMostRecentTouchDateOfAllContainers();
                imageLastTouchDate[this.InspectResponse.ID] = lastTouchDate;
            }
            else if (this.Children.Count() > 0) // Query child images to get most recent touch date
            {
                foreach (var child in this.Children)
                {
                    var childLastTouchDate = child.GetImageLastTouchDate(imageLastTouchDate);
                    if (childLastTouchDate > lastTouchDate) lastTouchDate = childLastTouchDate;
                }
            } 
            else // No record found, no child containers, no containers using this image
            {
                lastTouchDate = DateTime.UtcNow;
                imageLastTouchDate[this.InspectResponse.ID] = lastTouchDate;
            }

            return lastTouchDate;
        }
    }
}