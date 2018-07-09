FROM microsoft/dotnet:2.0-sdk AS build-env
WORKDIR /docker-gc

COPY DockerGC ./

RUN dotnet publish -c Release -o out

# build runtime image
FROM microsoft/dotnet:2.0-runtime 
WORKDIR /docker-gc
COPY --from=build-env /docker-gc/out ./

ENV DOCKERGC_DOCKER_ENDPOINT unix:///var/run/docker.sock
ENV DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES 720
ENV DOCKERGC_DOCKER_CLIENT_TIMEOUT_IN_SECONDS 180

# Image deletion sort/decision factor: ByImageCreationDate or ByImageLastTouchDate
ENV DOCKERGC_IMAGE_DELETION_ORDER ByImageLastTouchDate

ENV DOCKERGC_RECYCLING_STRATEGY ByDate
ENV DOCKERGC_DAYS_BEFORE_DELETION 30
# ENV DOCKERGC_RECYCLING_STRATEGY ByDiskSpace
# ENV DOCKERGC_SIZE_LIMIT_IN_GIGABYTE 100

# Blacklist of container state
# docker-gc will delete image if used by containers in these states
# Containers in these states will be removed before image deletion
# Docker container states are: created,restarting,running,paused,exited,dead,removing
ENV DOCKERGC_CONTAINER_STATE_BLACKLIST dead,exited

# Whitelist of images
# Example: ENV DOCKERGC_IMAGE_WHITELIST docker-gc:latest,microsoft*,ubuntu*,*logger*
# ENV DOCKERGC_IMAGE_WHITELIST docker-gc:latest

ENV DOCKERGC_STATSD_LOGGER_ENABLED False
# ENV DOCKERGC_STATSD_HOST localhost
# ENV DOCKERGC_STATSD_PORT 8125

ENTRYPOINT ["dotnet", "DockerGC.dll"]