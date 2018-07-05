# docker-gc 
[![Build Status](https://travis-ci.org/JasonStein/docker-gc.svg?branch=master)](https://travis-ci.org/JasonStein/docker-gc)

docker-gc is a microservice to cleanup docker images smartly based on recycling strategy.

## Why do I need docker-gc?
When you have a host machine running docker containers, whenever your docker container uses host docker (by exposing docker socket), it leaves marks and you will need to cleanup images produced by those containers eventually. Things will get worse especially when you have numbers of docker build agents running in a cluster that use heavily on host docker. In this scenario, a GC service is needed, not only to cleanup unused images but also to keep as much base images/layers as possible for caching purposes. If this makes sense to you, you will simply need it. However, this also works on your dev-box if your disk space is limited and you are producing/using tons of docker images frequently.

## How it works?
docker-gc is a service runs in a container and it talks to host's docker daemon. It analyses host's docker registry every 60 minutes (configurable) and delete oldest/least used images. The way it works is by generating a dependency tree and start deleting only from leaf images to keep as much base/parent images as possible (oldest leaf first deletion). If one image is old enough and never used by any container, it will be removed. If an image is old enough but recently used by a container, it will be preserved. There are two cleanup strategies available and everything is configurable. Feel free to take a look at the source code to understand how this algorithm works.

## Installation

Run docker-gc container standalone using default configs:
```
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock jackil/docker-gc
```
Run docker-gc container with your own configs (ByDate):
```
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock \
            -e DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES='120' \
            -e DOCKERGC_RECYCLING_STRATEGY='ByDate' \
            -e DOCKERGC_IMAGE_DELETION_ORDER='ByImageCreationDate' \
            -e DOCKERGC_DAYS_BEFORE_DELETION='30' \
            jackil/docker-gc
```
Run docker-gc container with your own configs (ByDiskSpace):
```
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock \
            -e DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES='120' \
            -e DOCKERGC_IMAGE_DELETION_ORDER='ByImageLastTouchDate' \
            -e DOCKERGC_RECYCLING_STRATEGY='ByDiskSpace' \
            -e DOCKERGC_SIZE_LIMIT_IN_GIGABYTE=$(df --output=size /var/lib/docker | awk 'FNR == 2 {print int($1*0.7/1024/1024)}') \
            jackil/docker-gc
```
Note: the df command above will give you the number of 70% total disk space of default docker drive in GB

## Recycling Strategies
docker-gc will cleanup images older than pre-configured interval if no running containers were found or no containers were found in pre-configed blacklist states. Images only used by containers in blacklist states will be removed, containers in blacklist states will also be stopped and removed as well.

#### 1. ByDate
When recycling strategy set to [ByDate], docker-gc will delete images older than pre-configured interval based on either image creation date
or image last touch date.

#### 2. ByDiskSpace
When recycling strategy set to [ByDiskSpace], docker-gc will delete images if disk usage of all docker images is over threshold and it will keep disk space used by docker images below threshold. docker-gc will delete old images first by comparing either image creation date or last touch date base on your choice.

#### Image Deletion Order
There are two deletion orders: ByImageCreationDate, ByImageLastTouchDate. Both [ByDate] and [ByDiskSpace] strategies use this value as decision/sort factor. When image deletion order set to [ByImageCreationDate], docker-gc will use image creation date as sort/decision factor upon image deletion. When image deletion order set to [ByImageLastTouchDate], docker-gc will use image last touch date as sort/decision factor upon image deletion. All container events (created, started, stopped, killed etc.) will be treated as "touch" to the image it uses and when docker-gc started, it keeps track of all container events even if container gets killed. The last touch date will be set to UTC now if no containers were found when docker-gc started to prevent usable images from getting deleted.

### Example of Configurations [Environment variables]

    -DOCKERGC_DOCKER_ENDPOINT unix:///var/run/docker.sock
    -DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES 60                   // docker-gc runs every 60 minutes

    -DOCKERGC_CONTAINER_STATE_BLACKLIST dead,exited              // docker-gc will not delete image if used by
                                                                 // containers not in black list states

    -DOCKERGC_IMAGE_DELETION_ORDER ByImageLastTouchDate          // use image last touch date as sort/decision factor

    -DOCKERGC_RECYCLING_STRATEGY ByDate
    -DOCKERGC_DAYS_BEFORE_DELETION 14                            // remove images/layers older than 14d if possible
     OR
    -DOCKERGC_RECYCLING_STRATEGY ByDiskSpace
    -DOCKERGC_SIZE_LIMIT_IN_GIGABYTE 100                         // remove images/layers when total disk space used
                                                                 // by all images is greater than 100 GB
    -DOCKERGC_STATSD_LOGGER_ENABLED False
    -DOCKERGC_STATSD_HOST localhost
    -DOCKERGC_STATSD_PORT 8125

    -ENV DOCKERGC_DOCKER_CLIENT_TIMEOUT_IN_SECONDS 180           // docker client timeout

Note: When DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES set to -1, docker-gc will perform one-time cleanup (Exited after finished)


### Helm Chart for Kubernetes Deployment

Why do I need docker-gc for Kubernetes???

- Kubernetes comes with built-in image and container GC, however, it only deals with the images/containers deployed by k8s but not the ones that get used/generated by containers which talk directly to host docker. Imagine you have a deployment of docker builders in Kubernetes which uses host docker to build stuff.

## How to install?

Examples:
```
helm install --set config.strategy=ByDiskSpace --set config.sizeLimitInGigabyte=15 --set config.executionIntervalInMinutes=720 --set config.imageDeletionOrder=ByImageLastTouchDate jackil-docker-gc
```
or
```
helm install --set config.strategy=ByDate --set config.daysBeforeDeletion=30 --set config.executionIntervalInMinutes=720 --set config.imageDeletionOrder=ByImageLastTouchDate jackil-docker-gc
```

Note: When executionIntervalInMinutes set to -1, docker-gc will perform one-time cleanup.

| Parameter                             | Description                                 | Default                                 |
| ------------------------------------- | ------------------------------------------- | --------------------------------------- |
| `config.dockerEndpoint`               | docker daemon endpoint                      | unix:///var/run/docker.sock             |
| `config.executionIntervalInMinutes`   | grace period in minutes before gc occurs    | 60                                      |
| `config.dockerClientTimeoutInSeconds` | docker client timeout                       | 180                                     |
| `config.imageDeletionOrder`           | image deletion order                        | ByImageLastTouchDate                    |
| `config.strategy`                     | image deletion strategy                     | ByDate                                  |
| `config.daysBeforeDeletion`           | date setting for ByDate strategy            | 30                                      |
| `config.sizeLimitInGigabyte`          | threshold setting for ByDiskSpace strategy  | Not set                                 |
| `config.containerStateBlacklist`      | state list for gc to safely stop and remove | dead,exited                             |
| `statsd.enabled`                      | enable statsd counter log                   | false                                   |
| `statsd.host`                         | statsd host (localhost)                     | Not set                                 |
| `statsd.port`                         | statsd port (8125)                          | Not set                                 |

For custom configurations, please head to values.yaml.

### Run as Fleet service

docker-gc.service is an example of using it with fleet.


