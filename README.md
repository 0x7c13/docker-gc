# docker-gc 
[![Build Status](https://travis-ci.org/JasonStein/docker-gc.svg?branch=master)](https://travis-ci.org/JasonStein/docker-gc)

docker-gc is a microservice to cleanup docker images automatically based on recycling strategy. Dockerfile and helm charts are provided for easy installation.

## Why do I need docker-gc?
When you have a host machine running docker containers, whenever your docker container uses host docker (by exposing docker socket), it leaves marks and you will need to cleanup images produced by those containers eventually. Things will get worse especially when you have numbers of docker build agents running in a cluster that use heavily on host docker. In this scenario, a GC service is needed, not only to cleanup unused images but also to keep as much base images/layers as possible for caching purposes. If this makes sense to you, you will simply need it. However, this also works on your dev-box if your disk space is limited and you are producing/using tons of docker images frequently.

## How it works?
docker-gc is a service runs in a container and it talks to host's docker daemon. It analysis host's docker registry every 60 minutes (configurable) and delete oldest/least used images. The way it works is by generating a dependency tree and start deleting only from leaf images to keep as much base/parent images as possible (oldest leaf first deletion). If one image is old enough and never used by any container, it will be removed. If an image is old enough but recently used by a container, it will be preserved. There are two cleanup strategies available and everything is configurable. Feel free to take a look at the source code to understand how this algorithm works.

## Installation

Run docker-gc container standalone using default configs:
```
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock jackil/docker-gc
```
Run docker-gc container with your own configs:
```
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock \
	        -e DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES='120' \
	        -e DOCKERGC_RECYCLING_STRATEGY='ByDate' \
	        -e DOCKERGC_DAYS_BEFORE_DELETION='30' \
	        jackil/docker-gc
```

## Recycling Strategies
docker-gc will cleanup images older than pre-configured interval if no running containers were found or no containers were found in pre-configed blacklist states. Images only used by containers in blacklist states will be removed, containers in blacklist states will also be stopped and removed as well. However, docker-gc still waits for a given days before actually deleting the image in this case, which is totally configurable.
    
#### 1. ByDate
When recycling strategy set to [ByDate], DockerGC will cleanup images older than pre-configured interval.
    
#### 2. ByDiskSpace
When recycling strategy set to [ByDiskSpace], DockerGC will cleanup images when disk usage of all docker images is over threshold. It will delete oldest images first.


### Example of Configurations [Environment variables]

    -DOCKERGC_DOCKER_ENDPOINT unix:///var/run/docker.sock
    -DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES 60                   // docker-gc runs every 60 minutes

    -DOCKERGC_CONTAINER_STATE_BLACKLIST dead,exited              // docker-gc will not going to delete if
    -DOCKERGC_WAIT_FOR_CONTAINERS_IN_BLACKLIST_STATE_FOR_DAYS 7  // container dead or exited within 7 days
                                                                 // you can also add more states based on your need
    -DOCKERGC_RECYCLING_STRATEGY ByDate
    -DOCKERGC_DAYS_BEFORE_DELETION 14                            // remove images/layers older then 14d if possible
     OR
    -DOCKERGC_RECYCLING_STRATEGY ByDiskSpace
    -DOCKERGC_SIZE_LIMIT_IN_GIGABYTE 100                         // remove images/layers when total disk space used
                                                                 // by all images is greater then 100 GB
    -STATSD_LOGGER_ENABLED False                                
    -STATSD_HOST localhost
    -STATSD_PORT 8125

Note: When DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES set to -1, docker-gc will perform one-time cleanup (Exited after finished)


### Helm Chart for Kubernetes Deployment

Why do I need docker-gc for Kubernetes???

- Kubernetes comes with built-in image and container GC, however, it only deals with the images/containers deployed by k8s but not the ones that get used/generated by containers which talk directly to host docker. Imagine you have a deployment of docker builders in Kubernetes which uses host docker to build stuff.

An example of helm config:

    strategy: ByDate
    executionIntervalInMinutes: 60
    dockerEndpoint: unix:///var/run/docker.sock
    daysBeforeDeletion: 30
    containerStateBlacklist: dead,exited
    waitForContainersInBlacklistStateForDays: 7
   
Note: When executionIntervalInMinutes set to -1, docker-gc will perform one-time cleanup.

For custom configurations, please head to values.yaml.

