#!/bin/bash
# Run from project root.

set -e

docker build -t twitchax/termy:debug --build-arg config=Debug .
docker run -it --rm --privileged -p 5000:80 -p 5001:443 -v /Users/twitchax/OneDrive/Projects/Termy/.hidden:/etc/secrets twitchax/termy:debug