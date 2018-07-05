namespace DockerGC.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;
    using Moq;
    using DockerGC;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class TestStrategy : RecyclingStrategy
    {
        public TestStrategy(IMatchlist imageWhitelist, IMatchlist stateBlacklist) : base(imageWhitelist, stateBlacklist)
        {
        }

        public override IList<DockerImageNode> GetImagesToBeRecycledInOrder(IList<DockerImageNode> baseImageNodes)
        {
            throw new NotImplementedException();
        }
    }

    public class RecyclingStrategyUnitTest
    {
        [Fact]
        public void ShouldNotDeleteImageIfInWhitelist()
        {
            var strategyA = new TestStrategy(new Matchlist(), new Matchlist("dead,exited"));
            var strategyB = new TestStrategy(new Matchlist("test"), new Matchlist("dead,exited"));

            var image = new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "A",
                                RepoTags = new List<string> {"test"},
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                            },
                            Containers = new List<ContainerInspectResponse>() {
                                new ContainerInspectResponse(){
                                    State = new ContainerState() {
                                        Status = "dead",
                                        FinishedAt = DateTime.UtcNow.ToString(),
                                    },
                                }
                            },
                        };

            Assert.True(strategyA.CanDelete(image));
            Assert.False(strategyB.CanDelete(image));
        }      

        [Fact]
        public void ShouldNotDeleteImageIfContainerExistsButNotInBlacklist()
        {
            var strategyA = new TestStrategy(new Matchlist(), new Matchlist("dead,exited,pause"));
            var strategyB = new TestStrategy(new Matchlist(), new Matchlist("dead,exited"));

            var image = new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "A",
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                            },
                            Containers = new List<ContainerInspectResponse>() {
                                new ContainerInspectResponse(){
                                    State = new ContainerState() {
                                        Status = "pause",
                                        FinishedAt = DateTime.UtcNow.ToString(),
                                    },
                                }
                            },
                        };

            Assert.True(strategyA.CanDelete(image));
            Assert.False(strategyB.CanDelete(image));
        }

        [Fact]
        public void ShouldDeleteImageIfNoContainerUsage()
        {
            var strategy = new TestStrategy(new Matchlist(), new Matchlist("dead,exited"));

            var image = new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "A",
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                            }
                        };

            Assert.True(strategy.CanDelete(image));
        }     
    }
}
