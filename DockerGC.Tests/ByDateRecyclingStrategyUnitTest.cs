namespace DockerGC.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Moq;
    using DockerGC;
    using Docker.DotNet;
    using Docker.DotNet.Models;

    public class ByDateRecyclingStrategyUnitTest
    {
        [Fact]
        public void ShouldNotRemoveAnyImage_WhenNoImagesWereFound()
        {
            var strategy = new ByDateRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(new List<DockerImageNode>());

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (0/0)
        // Image: B () 3 days (0/0)
        [Fact]
        public void ShouldNotRemoveAnyImage_WhenNoImagesWereOldEnough()
        {
            var strategy = new ByDateRecyclingStrategy(5, new Matchlist(), new Matchlist("dead,exited"), 0); // 5 days

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },                  
                },
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (0/0)
        // Image: B () 3 days (0/0)
        // Image: C () 5 days (0/0)
        [Fact]
        public void ShouldRemoveOldImages_WhenNoContainersWereFound()
        {
            var strategy = new ByDateRecyclingStrategy(2, new Matchlist(), new Matchlist("dead,exited"), 0); // 2 days

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },                 
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                    },                
                },            
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 2);
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "B"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "C"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (0/1)
        // Image: B () 3 days (0/1)
        // Image: C () 5 days (0/2)
        [Fact]
        public void ShouldRemoveOldImages_WhenContainersWereFoundButNotInRunningOrRestartingState()
        {
            var strategy = new ByDateRecyclingStrategy(2, new Matchlist(), new Matchlist("dead,exited"), 0); // 2 days

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
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
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "exited",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        }
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "exited",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        },
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "dead",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        }
                    },                  
                },
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 2);
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "B"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "C"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (1/1)
        // Image: B () 3 days (1/3)
        [Fact]
        public void ShouldNotRemoveAnyImage_WhenAllImagesHaveRunningOrRestartingContainer()
        {
            var strategy = new ByDateRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "running",
                            },
                        }
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "restarting",
                            },
                        },
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "dead",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        },
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "paused",
                            },
                        }
                    },
                },
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (0/1)
        //  - Image: C () 1 days (1/1)
        //   - Image: D () 1 days (0/0)
        // Image: B () 3 days (1/3)
        [Fact]
        public void ShouldNotRemoveImage_WhenChildImagesHaveRunningOrRestartingContainer()
        {
            var strategy = new ByDateRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "exited",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        }
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "C",
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                            },
                            Containers = new List<ContainerInspectResponse>() {
                                new ContainerInspectResponse(){
                                    State = new ContainerState() {
                                        Status = "running",
                                    },
                                }
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "D",
                                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                                    },
                                },
                            }
                        },
                    }
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },
                    Containers = new List<ContainerInspectResponse>() {
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "restarting",
                            },
                        },
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "dead",
                                FinishedAt = DateTime.UtcNow.ToString(),
                            },
                        },
                        new ContainerInspectResponse(){
                            State = new ContainerState() {
                                Status = "paused",
                            },
                        }
                    },             
                },
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 1);
            Assert.Equal(imagesToBeRecycled[0].InspectResponse.ID, "D");
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days (0/0)
        //  - Image: X () 1 days (0/0)
        //   - Image: Y () 1 days (0/0)
        // Image: B () 3 days (0/0)
        //  - Image: M () 3 days (0/0)
        //  - Image: N () 3 days (0/0)
        // Image: C () 7 days (0/0)
        //  - Image: J () 7 days (0/0)
        //   - Image: L () 3 days (0/0)
        //  - Image: K () 3 days (0/0)
        //
        // When date set to be 0 day, expected deleting order should be: Y,X,A,M,N,B,L,J,K,C
        [Fact]
        public void ShouldRemoveOldImages_InDependencyOrder_HaveNoRunningOrRestartingContainers_All()
        {
            var strategy = new ByDateRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "X",
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "Y",
                                        Created = DateTime.UtcNow.AddDays(-1), // 1 days
                                    },                 
                                }
                            },
                        },
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "M",
                                Created = DateTime.UtcNow.AddDays(-3), // 3 days
                            },                
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "N",
                                Created = DateTime.UtcNow.AddDays(-3), // 3 days
                            },                
                        }, 
                    },                
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-7), // 7 days
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "J",
                                Created = DateTime.UtcNow.AddDays(-7), // 7 day
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "L",
                                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                                    },               
                                }
                            },
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "K",
                                Created = DateTime.UtcNow.AddDays(-3), // 3 days
                            },                  
                        }, 
                    },
                },       
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 10);
            // Expected order: Y,X,A,M,N,B,L,J,K,C
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "Y"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "X"));
            Assert.True(string.Equals(imagesToBeRecycled[2].InspectResponse.ID, "A"));
            Assert.True(string.Equals(imagesToBeRecycled[3].InspectResponse.ID, "M"));
            Assert.True(string.Equals(imagesToBeRecycled[4].InspectResponse.ID, "N"));
            Assert.True(string.Equals(imagesToBeRecycled[5].InspectResponse.ID, "B"));
            Assert.True(string.Equals(imagesToBeRecycled[6].InspectResponse.ID, "L"));
            Assert.True(string.Equals(imagesToBeRecycled[7].InspectResponse.ID, "J"));
            Assert.True(string.Equals(imagesToBeRecycled[8].InspectResponse.ID, "K"));
            Assert.True(string.Equals(imagesToBeRecycled[9].InspectResponse.ID, "C"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 10 days (0/0)
        //  - Image: X () 8 days (0/0)
        //   - Image: Y () 5 days (0/0)
        // Image: B () 14 days (0/0)
        //  - Image: M () 5 days (0/0)
        //  - Image: N () 10 days (0/0)
        // Image: C () 30 days (0/0)
        //  - Image: J () 20 days (0/0)
        //   - Image: L () 10 days (0/0)
        //  - Image: K () 3 days (0/0)
        //
        // When date set to be 7 days, expected deleting order should be: N,L,J
        [Fact]
        public void ShouldRemoveOldImages_InDependencyOrder_HaveNoRunningOrRestartingContainers_Partial()
        {
            var strategy = new ByDateRecyclingStrategy(7, new Matchlist(), new Matchlist("dead,exited"), 0); //7 days

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-10), // 10 days
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "X",
                                Created = DateTime.UtcNow.AddDays(-8), // 8 days
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "Y",
                                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                                    },                 
                                }
                            },
                        },
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-14), // 14 days
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "M",
                                Created = DateTime.UtcNow.AddDays(-5), // 5 days
                            },                
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "N",
                                Created = DateTime.UtcNow.AddDays(-10), // 10 days
                            },                
                        }, 
                    },                
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-30), // 30 days
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "J",
                                Created = DateTime.UtcNow.AddDays(-20), // 20 day
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "L",
                                        Created = DateTime.UtcNow.AddDays(-10), // 10 days
                                    },               
                                }
                            },
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "K",
                                Created = DateTime.UtcNow.AddDays(-3), // 3 days
                            },                  
                        }, 
                    },
                },       
            };

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 3);
            // Expected order: N,L,J
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "N"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "L"));
            Assert.True(string.Equals(imagesToBeRecycled[2].InspectResponse.ID, "J"));
        }    
    }
}
