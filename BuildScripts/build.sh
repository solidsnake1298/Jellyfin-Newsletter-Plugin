#!/bin/bash
TAG=${1}

IMAGE="pypi_dotnet"
echo "Using image: '${IMAGE}:${TAG}.0'"
if ! docker images -a | grep "${IMAGE}" | grep "${TAG}.0"; then
    echo "Generating '${IMAGE}:${TAG}.0' image"
    docker build -t ${IMAGE}:${TAG}.0 --build-arg IMAGE_TAG=10 ./BuildScripts/
else
    echo "Image already exists!"
fi

docker run --name dotnet -it -v .:/Development ${IMAGE}:${TAG}.0 /Development/BuildScripts/dotnetbuild.sh "${2}"
docker rm dotnet
