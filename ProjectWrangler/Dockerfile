# Build a small image for .NET 9
# Largely inspired by https://www.thorsten-hans.com/how-to-build-smaller-and-secure-docker-images-for-net5
# Revised with advises in https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/ for multi-arch
# And https://github.com/dotnet/dotnet-docker/blob/e5a690412442a5c6f4ea7980d14d3f354e6b4651/samples/aspnetapp/Dockerfile.alpine

#
# Arguments
#
# The project to build, binary name is extracted from the file (without extension)
ARG PROJECT_FILE=ProjectWrangler.csproj

#
# Builder (note BUILDPLATFORM is a built-in argument)
#
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS builder
ARG PROJECT_FILE
ARG TARGETARCH
ARG BUILDPLATFORM
WORKDIR /src
# Retore in a single layer
COPY $PROJECT_FILE .
RUN dotnet restore \
  --arch $TARGETARCH \
  --runtime linux-musl-$TARGETARCH
# Publish in another one
COPY . .
RUN dotnet publish \
    --output /app/publish \
    --no-restore \
    --arch $TARGETARCH \
    --self-contained true \
    /p:PublishSingleFile=true

#
# Runtime
#
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine
ARG PROJECT_FILE
# Enable globalization + musl compat
# https://github.com/dotnet/dotnet-docker/blob/e5a690412442a5c6f4ea7980d14d3f354e6b4651/samples/enable-globalization.md
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apk add --no-cache \
    icu-data-full \
    icu-libs \
    gcompat \
    tzdata
WORKDIR /app
COPY --from=builder /app/publish .
RUN chown -R $APP_UID /app
USER $APP_UID
RUN ln -s /app/$(basename $PROJECT_FILE .csproj) /app/__entrypoint
ENTRYPOINT ["/app/__entrypoint"]