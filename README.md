#DockerGC
##Introduction
docker-gc is a microservice to cleanup docker images automatically based on recycling strategy. Dockerfile and helm charts are provided for easy installation.

##Installation
You can run this command to install chart:
```
$ helm install --name #My_Release_Name# docker-gc --set config.strategy=ByDiskSpace --set config.sizeLimitInGigabyte=15
```
Or run docker-gc container standalone:
```
$ docker build -t docker-gc .
$ docker run -d -v /var/run/docker.sock:/var/run/docker.sock docker-gc
```

##Recycling Strategy
    DockerGC will cleanup images older than pre-configured interval if no running containers were found or no containers were found in preconfiged blacklist states. Images used by containers in blacklist states will be removed containers in blacklist states will also be stopped and removed as well.
    
#### 1. ByDate
    When recycling strategy set to [ByDate], DockerGC will cleanup images older than pre-configured interval.
    
#### 2. ByDiskSpace
    When recycling strategy set to [ByDiskSpace], DockerGC will cleanup images when disk usage of all docker images is over threshold.


###Example of Configurations [Environment variables]

    -DOCKERGC_DOCKER_ENDPOINT unix:///var/run/docker.sock
    -DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES 60

    -DOCKERGC_CONTAINER_STATE_BLACKLIST dead,exited
    -DOCKERGC_WAIT_FOR_CONTAINERS_IN_BLACKLIST_STATE_IN_DAYS 7

    -DOCKERGC_RECYCLING_STRATEGY ByDate
    -DOCKERGC_DAYS_BEFORE_DELETION 30
     OR
    -DOCKERGC_RECYCLING_STRATEGY ByDiskSpace
    -DOCKERGC_SIZE_LIMIT_IN_GIGABYTE 100

    -STATSD_LOGGER_ENABLED false
    -STATSD_HOST Statsd
    -STATSD_PORT 8125

Note: When DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES set to -1, docker-gc will only perform one-time cleanup.