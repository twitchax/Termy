#!/bin/bash

set -e

docker build . -t twitchax/termy
docker run -it --rm --privileged -p 5000:80 twitchax/termy