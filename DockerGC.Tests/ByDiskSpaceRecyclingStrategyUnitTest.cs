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

    public class ByDiskSpaceRecyclingStrategyUnitTest
    {
        private void _addParentRef(DockerImageNode node)
        {
            foreach (var child in node.Children) 
            {
                child.Parent = node;
                _addParentRef(child);
            }
        }
        private void _fixParentRef(List<DockerImageNode> images)
        {
            foreach (var image in images) 
            {
                _addParentRef(image);
            }
        }

        [Fact]
        public void ShouldNotRemoveAnyImage_WhenNoImagesWereFound()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(new List<DockerImageNode>());

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 200MB (0/0)
        // Image: B () 3 days 700MB (0/0)
        [Fact]
        public void ShouldNotRemoveAnyImage_WhenSpaceIsEnough()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(1, new Matchlist(), new Matchlist("dead,exited"), 0); // 1GB

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day,
                        Size = 200 * 1024 * 1024 // 200 MB
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                        Size = 700 * 1024 * 1024 // 700 MB
                    },                  
                },
            };

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 100MB (0/0)
        // Image: B () 3 days 200MB (0/0)
        // Image: C () 5 days 300MB (0/0)
        [Fact]
        public void ShouldRemoveOldImages_WhenNoContainersWereFound()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0.2, new Matchlist(), new Matchlist("dead,exited"), 0); // 200MB

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-3), // 3 days
                        Size = 200 * 1024 * 1024 // 200 MB
                    },                 
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                        Size = 300 * 1024 * 1024 // 300 MB
                    },                
                },            
            };

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 2);
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "C"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "B"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 100MB (0/1)
        // Image: B () 3 days 200MB (0/1)
        // Image: C () 5 days 300MB (0/2)
        [Fact]
        public void ShouldRemoveOldImages_WhenContainersWereFoundButNotInRunningOrRestartingState()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0.2, new Matchlist(), new Matchlist("dead,exited"), 0); // 200MB

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                        Size = 100 * 1024 * 1024 // 100 MB
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
                        Size = 200 * 1024 * 1024 // 200 MB
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
                        Size = 300 * 1024 * 1024 // 300 MB
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

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 2);
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "C"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "B"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 100MB (1/1)
        // Image: B () 3 days 200MB (1/3)
        [Fact]
        public void ShouldNotRemoveAnyImage_WhenAllImagesHaveRunningOrRestartingContainer()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                        Size = 100 * 1024 * 1024 // 100 MB
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
                        Size = 200 * 1024 * 1024 // 200 MB
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

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 0);
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 100MB (0/1)
        //  - Image: C () 1 days 100MB (1/1)
        //   - Image: D () 1 days 100MB (0/0)
        // Image: B () 3 days 100MB (1/3)
        [Fact]
        public void ShouldNotRemoveImage_WhenChildImagesHaveRunningOrRestartingContainer()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                        Size = 100 * 1024 * 1024 // 100 MB
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
                                Size = 100 * 1024 * 1024 // 100 MB
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
                                        Size = 100 * 1024 * 1024 // 100 MB
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
                        Size = 100 * 1024 * 1024 // 100 MB
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
            
            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 1);
            Assert.Equal(imagesToBeRecycled[0].InspectResponse.ID, "D");
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 1 days 100MB (0/0)
        //  - Image: X () 1 days 100MB (0/0)
        //   - Image: Y () 1 days 100MB (0/0)
        // Image: B () 10 days 100MB (0/0)
        //  - Image: M () 7 days 100MB (0/0)
        //  - Image: N () 4 days 100MB (0/0)
        // Image: C () 9 days 100MB (0/0)
        //  - Image: J () 5 days 100MB (0/0)
        //   - Image: L () 5 days 100MB (0/0)
        //  - Image: K () 3 days 100MB (0/0)
        //
        // Expected deleting order should be: M,L,J,N,B,K,C,Y,X,A
        [Fact]
        public void ShouldRemoveOldImages_InDependencyOrder_HaveNoRunningOrRestartingContainers_All()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0, new Matchlist(), new Matchlist("dead,exited"), 0);

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-1), // 1 day
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "X",
                                Created = DateTime.UtcNow.AddDays(-1), // 1 day
                                Size = 100 * 1024 * 1024 // 100 MB
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "Y",
                                        Created = DateTime.UtcNow.AddDays(-1), // 1 days
                                        Size = 100 * 1024 * 1024 // 100 MB
                                    },                 
                                }
                            },
                        },
                    },
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "B",
                        Created = DateTime.UtcNow.AddDays(-10), // 10 days
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "M",
                                Created = DateTime.UtcNow.AddDays(-7), // 7 days
                                Size = 100 * 1024 * 1024 // 100 MB
                            },                
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "N",
                                Created = DateTime.UtcNow.AddDays(-4), // 4 days
                                Size = 100 * 1024 * 1024 // 100 MB
                            },                
                        }, 
                    },                
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-9), // 9 days
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "J",
                                Created = DateTime.UtcNow.AddDays(-5), // 5 day
                                Size = 100 * 1024 * 1024 // 100 MB
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "L",
                                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                                        Size = 100 * 1024 * 1024 // 100 MB
                                    },               
                                }
                            },
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "K",
                                Created = DateTime.UtcNow.AddDays(-3), // 3 days
                                Size = 100 * 1024 * 1024 // 100 MB
                            },                  
                        }, 
                    },
                },       
            };

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 10);

            // Expected order: M,L,J,N,B,K,C,Y,X,A
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "M"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "L"));
            Assert.True(string.Equals(imagesToBeRecycled[2].InspectResponse.ID, "J"));
            Assert.True(string.Equals(imagesToBeRecycled[3].InspectResponse.ID, "N"));
            Assert.True(string.Equals(imagesToBeRecycled[4].InspectResponse.ID, "B"));
            Assert.True(string.Equals(imagesToBeRecycled[5].InspectResponse.ID, "K"));
            Assert.True(string.Equals(imagesToBeRecycled[6].InspectResponse.ID, "C"));
            Assert.True(string.Equals(imagesToBeRecycled[7].InspectResponse.ID, "Y"));
            Assert.True(string.Equals(imagesToBeRecycled[8].InspectResponse.ID, "X"));
            Assert.True(string.Equals(imagesToBeRecycled[9].InspectResponse.ID, "A"));
        }

        // Image: ID (Tag) Created (Running/Containers)
        // Image: A () 10 days 100MB (0/0)
        //  - Image: X () 8 days 200MB (0/0)
        //   - Image: Y () 5 days 300MB (0/0)
        // Image: B () 14 days 100MB (0/0)
        //  - Image: M () 5 days 200MB (0/0)
        //  - Image: N () 11 days 300MB (0/0)
        // Image: C () 30 days 100MB (0/0)
        //  - Image: J () 20 days 200MB (0/0)
        //   - Image: L () 10 days 300MB (0/0)
        //  - Image: K () 4 days 200MB (0/0)
        //
        // When size limit set to be 500MB, expected deleting order should be: N,L,J,Y,X
        [Fact]
        public void ShouldRemoveOldImages_InDependencyOrder_HaveNoRunningOrRestartingContainers_Partial()
        {
            var strategy = new ByDiskSpaceRecyclingStrategy(0.5, new Matchlist(), new Matchlist("dead,exited"), 0); // 500MB

            var images = new List<DockerImageNode>() {
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "A",
                        Created = DateTime.UtcNow.AddDays(-10), // 10 days
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "X",
                                Created = DateTime.UtcNow.AddDays(-8), // 8 days
                                Size = 200 * 1024 * 1024 // 200 MB
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "Y",
                                        Created = DateTime.UtcNow.AddDays(-5), // 5 days
                                        Size = 300 * 1024 * 1024 // 300 MB
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
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "M",
                                Created = DateTime.UtcNow.AddDays(-5), // 5 days
                                Size = 200 * 1024 * 1024 // 200 MB
                            },                
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "N",
                                Created = DateTime.UtcNow.AddDays(-11), // 11 days
                                Size = 300 * 1024 * 1024 // 300 MB
                            },                
                        }, 
                    },                
                },
                new DockerImageNode() {
                    InspectResponse = new ImageInspectResponse() {
                        ID = "C",
                        Created = DateTime.UtcNow.AddDays(-30), // 30 days
                        Size = 100 * 1024 * 1024 // 100 MB
                    },
                    Children = new List<DockerImageNode>() {
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "J",
                                Created = DateTime.UtcNow.AddDays(-20), // 20 day
                                Size = 200 * 1024 * 1024 // 200 MB
                            },
                            Children = new List<DockerImageNode>() {
                                new DockerImageNode() {
                                    InspectResponse = new ImageInspectResponse() {
                                        ID = "L",
                                        Created = DateTime.UtcNow.AddDays(-10), // 10 days
                                        Size = 300 * 1024 * 1024 // 300 MB
                                    },               
                                }
                            },
                        },
                        new DockerImageNode() {
                            InspectResponse = new ImageInspectResponse() {
                                ID = "K",
                                Created = DateTime.UtcNow.AddDays(-4), // 4 days
                                Size = 200 * 1024 * 1024 // 200 MB
                            },                  
                        }, 
                    },
                },       
            };

            _fixParentRef(images);
            var imagesToBeRecycled = strategy.GetImagesToBeRecycledInOrder(images);

            Assert.True(imagesToBeRecycled.Count == 5);
            // Expected order: N,L,J,Y,X
            Assert.True(string.Equals(imagesToBeRecycled[0].InspectResponse.ID, "N"));
            Assert.True(string.Equals(imagesToBeRecycled[1].InspectResponse.ID, "L"));
            Assert.True(string.Equals(imagesToBeRecycled[2].InspectResponse.ID, "J"));
            Assert.True(string.Equals(imagesToBeRecycled[3].InspectResponse.ID, "Y"));
            Assert.True(string.Equals(imagesToBeRecycled[4].InspectResponse.ID, "X"));
        }    
    }
}
