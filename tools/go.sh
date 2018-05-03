#!/bin/bash
# Run from project root.

set -e

kubectl delete deploy/termy

docker build -t twitchax/termy .
docker push twitchax/termy

kubectl create -f termy.yml

kubectl expose deployment termy --type=LoadBalancer --name=termy