#!/bin/bash
# Run from project root.

set -e

docker build -t twitchax/termy-terminal-host -f src/Terminal/Dockerfile src/Terminal
docker run -it -p 5000:80 -p 5001:443 twitchax/termy-terminal-host