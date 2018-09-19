#!/bin/bash
# Run from project root.

# set -e

kubectl delete deploy/termy --namespace=termy

docker build -t twitchax/termy .
docker push twitchax/termy

kubectl apply -f assets/termy.yml