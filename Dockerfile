FROM microsoft/dotnet:2.0-sdk

COPY DockerGC /docker-gc
WORKDIR /docker-gc

ENV DOCKERGC_DOCKER_ENDPOINT unix:///var/run/docker.sock
ENV DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES 60

ENV DOCKERGC_RECYCLING_STRATEGY ByDate
ENV DOCKERGC_DAYS_BEFORE_DELETION 14
# ENV DOCKERGC_RECYCLING_STRATEGY ByDiskSpace
# ENV DOCKERGC_SIZE_LIMIT_IN_GIGABYTE 100

# Blacklist of container state
# DockerGC will delete image if used by containers in these states
# Containers in these states will be removed before image deletion
# Docker container states are: created,restarting,running,paused,exited,dead,removing
ENV DOCKERGC_CONTAINER_STATE_BLACKLIST dead,exited

# DockerGC will wait for containers in blacklist state for given amount of time
# before taking action when this valvue is set to be greater then zero
ENV DOCKERGC_WAIT_FOR_CONTAINERS_IN_BLACKLIST_STATE_FOR_DAYS 7

# Whitelist of images
# Example: ENV DOCKERGC_IMAGE_WHITELIST docker-gc:latest,microsoft*,ubuntu*,*logger*
# ENV DOCKERGC_IMAGE_WHITELIST docker-gc:latest

ENV STATSD_LOGGER_ENABLED False
# ENV STATSD_HOST localhost
# ENV STATSD_PORT 8125

RUN ["dotnet", "build", "-c", "Release"]

ENTRYPOINT ["dotnet", "run", "-c", "Release"]
